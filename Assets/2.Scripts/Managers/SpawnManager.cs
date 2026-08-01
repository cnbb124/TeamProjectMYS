using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

// ================================================================
// [SpawnManager — 웨이브 기반 적 스폰]
// ================================================================
// 씬에 1개 배치. DontDestroyOnLoad 없음 (씬 전용).
//
// 스폰: WaveData.SpawnEntry의 poolType을 스폰포인트에 InstantiateRoomObject로 네트워크 스폰.
//   spawnPointIndex -1이면 defaultSpawnPoints(랜덤 풀) 중 랜덤, 0 이상이면 fixedSpawnPoints(지정 포인트)의 그 인덱스.
//   (씬 배치형은 SetActive가 멀티 동기화 안 돼 제거 — 배치 적은 스폰포인트로 대체)
//
// 웨이브 진행:
//   Start() → StartWave(0) → 스폰 완료 → 적 전멸 대기
//   → nextWaveDelay → StartWave(1) → ... → 마지막 웨이브 클리어 → GameManager.StageClear()
//
// 보스 웨이브:
//   GameManager.onBossSpawn 이벤트 발생 시 bossWave 별도 실행.
//   진행 중이던 일반 웨이브는 중단됨.
//
// 멀티(소유권 범위):
//   적/드랍은 '룸 종속' — InstantiateRoomObject로 스폰해 방장이 나가도 파괴되지 않고
//   소유권(IsMine)이 새 방장에게 자동 이전됨. (PhotonNetwork.Instantiate는 '유저 종속'이라
//   만든 사람이 나가면 CleanupCacheOnLeave로 전부 파괴됨 — 적에는 쓰면 안 됨)
//   웨이브 진행도도 룸 종속 상태라 Room Custom Property에 기록해, 방장 교체 시 새 방장이 이어받음.
// ================================================================
public class SpawnManager : MonoBehaviourPunCallbacks
{

	// ================================================================
	// 싱글톤 없음 — 씬 종속 오브젝트라 전역 접근점을 두면 안 됨.
	// static 참조는 씬을 넘어 살아남는데 이 오브젝트는 씬과 함께 사라지므로,
	// 이전 씬의 파괴된 인스턴스가 남아 새 씬의 자기 자신을 중복으로 판정하고 파괴할 수 있음.
	// 웨이브 시작은 자기 Start에서, 보스 웨이브는 GameManager.onBossSpawn 구독으로 처리하므로
	// 외부에서 이 매니저를 찾아야 할 이유가 없음.
	// ================================================================

	// ================================================================
	// 인스펙터
	// ================================================================
	[Header("웨이브 설정")]
	[Tooltip("순서대로 실행될 WaveData 목록. 전부 소진 시 StageClear 호출.")]
	public WaveData[] waves;
	[Tooltip("랜덤 스폰 풀 — SpawnEntry.spawnPointIndex가 -1일 때 이 중에서 랜덤(셔플) 선택함.")]
	public Transform[] defaultSpawnPoints;
	[Tooltip("지정 스폰 포인트 — SpawnEntry.spawnPointIndex가 0 이상일 때 이 배열의 인덱스로 고정 스폰함. " +
		"랜덤 풀과 분리돼 있어, 여기 둔 중간보스(EnemyStation 등) 자리에는 랜덤 몹이 안 나옴.")]
	public Transform[] fixedSpawnPoints;

	[Header("보스 웨이브")]
	[Tooltip("GameManager.onBossSpawn 이벤트 발생 시 실행할 WaveData. null이면 스킵.")]
	public WaveData bossWave;
	[Tooltip("보스 등장 워프 VFX 재생 시간(초). 이 시간만큼 연출을 보여준 뒤 보스를 스폰함.\n" +
		"0이면 연출 없이 바로 스폰.")]
	[SerializeField]
	private float _bossWarpSequenceTime;
	[SerializeField]
	[Tooltip("보스 등장 워프 VFX 스케일 배율. 0 이하면 프리팹 원본 크기 그대로 사용함.")]
	private float _bossWarpSequenceScale;
	[SerializeField]
	[Tooltip("보스 등장 워프 VFX 출력 위치")]
	private Transform _bossWarpVFXPoint;
	// ================================================================
	// 룸 종속 상태 키 (방장 교체 시 새 방장이 이어받을 기준점)
	// ================================================================
	// 웨이브 진행도는 특정 플레이어가 아니라 '방'에 속한 상태라 Room Custom Property에 둠.
	private const string WaveIndexPropertyKey = "SpawnWaveIndex";
	private const string BossWavePropertyKey = "SpawnBossWave";

	// ================================================================
	// 내부 상태
	// ================================================================
	private int _currentWaveIndex = 0;
	// 지금 돌고 있는 게 보스 웨이브인지. 보스 웨이브를 클리어하면 남은 일반 웨이브는 무시하고 즉시 스테이지 클리어함.
	private bool _bossWaveActive = false;

	// StopWaves 이후로 잠금. 스폰 재시작을 막음.
	// 현재는 GameManager가 클리어 시점부터 킬카운트를 안 세서 실제로 걸릴 일이 없음 — 예비용.
	private bool _wavesStopped = false;
	// 현재 웨이브에서 스폰된 적 목록 (클리어 판정용)
	private List<Enemy> _waveEnemies = new List<Enemy>();
	private Transform[] _shuffleBuffer;
	// ================================================================
	// 초기화
	// ================================================================
	private void Start()
	{
		if (GameManager.Instance != null)
		{
			GameManager.Instance.onBossSpawn += OnBossSpawnTriggered;
		}

		if (waves != null && waves.Length > 0)
		{
			StartWave(0);
		}
	}

	private void OnDestroy()
	{
		if (GameManager.Instance != null)
		{
			GameManager.Instance.onBossSpawn -= OnBossSpawnTriggered;
		}
	}

	// ================================================================
	// 웨이브 시작
	// ================================================================
	public void StartWave(int waveIndex)
	{
		// 적 스폰/웨이브는 Master 권위 — 나머지 클라는 웨이브를 안 돌리고 네트워크로 적을 받는다.
		// (오프라인/싱글은 IsMasterClient=true라 기존과 동일하게 동작)
		if (!PhotonNetwork.IsMasterClient || _wavesStopped)
		{
			return;
		}
		if (waves == null || waveIndex < 0 || waveIndex >= waves.Length)
		{
			return;
		}
		_currentWaveIndex = waveIndex;
		_bossWaveActive = false;
		PublishWaveState(waveIndex, false);
		_waveEnemies.Clear();
		StopAllCoroutines();
		StartCoroutine(SpawnWaveRoutine(waves[waveIndex]));
	}

	private IEnumerator SpawnWaveRoutine(WaveData wave)
	{
		if (wave == null || wave.entries == null)
		{
			Debug.Log("[SpawnManager] WaveData 또는 entries가 비어있어 스폰을 스킵함");
			yield break;
		}

		// 보스 웨이브는 등장 연출(워프)을 먼저 재생하고, 연출이 끝난 뒤 실제로 스폰함.
		// (스폰 후에 틀면 보스가 이미 튀어나온 상태에서 워프가 뒤늦게 터져 앞뒤가 안 맞음)
		if (wave == bossWave)
		{
			yield return StartCoroutine(PlayBossWarpSequence());
		}

		foreach (WaveData.SpawnEntry entry in wave.entries)
		{
			if (entry.delay > 0f)
			{
				// 일시정지 인지 대기 — 프리즈 동안 시간이 안 흐름(WaitForSeconds는 플래그 정지 무시).
				yield return GameManager.WaitGameplaySeconds(entry.delay);
			}

			yield return StartCoroutine(SpawnRandom(entry));
		}

		yield return StartCoroutine(WaitForWaveClear(wave));
	}



	// 보스 등장 워프 연출. 보스가 실제로 나올 지점에서 VFX를 틀고 그 시간만큼 대기함.
	// 멀티: 스폰 코루틴은 방장만 돌기 때문에 그냥 재생하면 방장 화면에서만 보임 —
	//       RPC로 남 클라에도 알려 각 클라가 자기 로컬 VFX 풀에서 재생함.
	private IEnumerator PlayBossWarpSequence()
	{
		if (_bossWarpSequenceTime <= 0f)
		{
			yield break;
		}

		// 워프 VFX는 전용 지점(_bossWarpVFXPoint)에서 재생 — 보스마다 크기가 달라 스폰 위치와 별개로 잡아둠.
		// 미지정이면 SpawnManager 자신 위치로 폴백.
		Transform warpPoint = _bossWarpVFXPoint != null ? _bossWarpVFXPoint : transform;
		BroadcastBossWarpVfx(warpPoint.position, warpPoint.rotation);

		// 일시정지 인지 대기 — 연출 도중 ESC로 멈춰도 시간이 안 흐르게(다른 대기와 동일 규칙).
		yield return GameManager.WaitGameplaySeconds(_bossWarpSequenceTime);
	}

	// 워프 VFX를 전원에게 재생시킴. 싱글/룸 밖은 로컬만, 멀티는 남 클라에 RPC로 전파(방장은 로컬 재생).
	private void BroadcastBossWarpVfx(Vector3 position, Quaternion rotation)
	{
		PlayBossSpawnWarpVfxLocal(position, rotation);

		if (PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode)
		{
			photonView.RPC(nameof(RpcBossWarpVfx), RpcTarget.Others, position, rotation);
		}
	}

	// 남 클라 수신 — 방장이 보낸 워프 위치/회전에서 로컬 VFX 재생.
	[PunRPC]
	private void RpcBossWarpVfx(Vector3 position, Quaternion rotation)
	{
		PlayBossSpawnWarpVfxLocal(position, rotation);
	}

	// 각 클라 로컬에서 워프 VFX 재생.
	private void PlayBossSpawnWarpVfxLocal(Vector3 position, Quaternion rotation)
	{
		if (VFXManager.Instance == null)
		{
			return;
		}
		// scale은 0이면 VFXManager가 "안 넘김"으로 보고 프리팹 기본 스케일을 씀.
		Vector3 scale = _bossWarpSequenceScale > 0f
			? Vector3.one * _bossWarpSequenceScale
			: Vector3.zero;
		VFXManager.Instance.PlayEffectAtPosition(EFFECT_TYPE.VFX_BOSS_WARP_OUT, position, rotation, _bossWarpSequenceTime, scale);
	}

	private IEnumerator SpawnRandom(WaveData.SpawnEntry entry)
	{
		if (PoolManager.Instance == null)
		{
			yield break;
		}

		// spawnPointIndex가 유효하면 fixedSpawnPoints의 그 지점 하나에 고정 스폰, 아니면(-1/범위밖) defaultSpawnPoints 중 랜덤(셔플).
		// 두 배열은 분리돼 있어, 랜덤 스폰이 지정 포인트(중간보스 자리 등)를 절대 고르지 않음.
		bool useSpecificPoint = entry.spawnPointIndex >= 0
			&& fixedSpawnPoints != null
			&& entry.spawnPointIndex < fixedSpawnPoints.Length;

		if (!useSpecificPoint && defaultSpawnPoints != null && defaultSpawnPoints.Length > 0)
		{
			if (_shuffleBuffer == null || _shuffleBuffer.Length != defaultSpawnPoints.Length)
			{
				_shuffleBuffer = new Transform[defaultSpawnPoints.Length];
			}

			System.Array.Copy(defaultSpawnPoints, _shuffleBuffer, defaultSpawnPoints.Length);
			for (int s = _shuffleBuffer.Length - 1; s > 0; s--)
			{
				int r = Random.Range(0, s + 1);
				(_shuffleBuffer[s], _shuffleBuffer[r]) = (_shuffleBuffer[r], _shuffleBuffer[s]);
			}
		}

		for (int i = 0; i < entry.count; i++)
		{
			// 일시정지 중엔 스폰하지 않음 — 프리즈가 풀릴 때까지 대기(프리즈 중 스폰/풀 확장 방지).
			while (GameManager.Instance != null && GameManager.Instance.IsGameplayFrozen)
			{
				yield return null;
			}

			// 위치+회전 모두 스폰포인트 Transform 기준. 포인트를 돌려놓으면 적도 그 방향으로 등장함.
			// (지정 포인트는 그 하나, 랜덤은 셔플 버퍼에서 순회. 폴백은 SpawnManager 자신)
			Transform spawnPoint = useSpecificPoint
				? fixedSpawnPoints[entry.spawnPointIndex]
				: (_shuffleBuffer != null ? _shuffleBuffer[i % _shuffleBuffer.Length] : transform);
			Vector3 pos = spawnPoint.position;
			Quaternion rot = spawnPoint.rotation;

			// Master 권위 네트워크 스폰 — 모든 클라에 같은 적이 생성됨(PhotonPoolAdapter가 로컬 풀로 라우팅).
			// prefabId = POOL_TYPE 이름(어댑터가 파싱해 풀에서 꺼냄). 위치/회전은 Instantiate가 설정.
			// 적은 '룸 종속'이라 RoomObject로 만듦 — 방장이 나가도 살아남고 소유권이 새 방장에게 넘어감.
			// 마지막 인자로 '이 적이 태어난 씬'을 같이 보냄 — 다른 씬에 있는 클라가 이걸 보고 숨김(EnemySceneVisibility).
			// 방장의 '현재 씬'이 아니라 '스폰 시점 씬'인 이유: 방장이 스테이션으로 돌아가도 스테이지 적은 그대로 보여야 함.
			GameObject go = PhotonNetwork.InstantiateRoomObject(
				entry.poolType.ToString(), pos, rot, 0,
				new object[] { SceneManager.GetActiveScene().name });
			if (go == null)
			{
				continue;
			}
			Enemy enemy = go.GetComponent<Enemy>();
			if (enemy != null)
			{
				// 풀 재사용 시 OnEnable이 재배치 이전에 먼저 도니, 새 위치 기준으로 순찰 앵커 갱신
				enemy.RefreshSpawnAnchor();

				_waveEnemies.Add(enemy);
			}
			if (i < entry.count - 1 && entry.interval > 0f)
			{
				yield return GameManager.WaitGameplaySeconds(entry.interval);
			}
		}
	}
	// ================================================================
	// 클리어 대기
	// ================================================================
	private IEnumerator WaitForWaveClear(WaveData wave)
	{
		// 등록된 적이 없으면 즉시 클리어
		if (_waveEnemies.Count == 0)
		{
			yield return GameManager.WaitGameplaySeconds(wave.nextWaveDelay);
			AdvanceWave(wave);
			yield break;
		}

		// 0.5초마다 전멸 여부 체크
		while (true)
		{
			yield return new WaitForSeconds(0.5f);

			// 파괴된(null) 유닛 제거
			_waveEnemies.RemoveAll(e => e == null);

			bool allDead = true;
			foreach (Enemy e in _waveEnemies)
			{
				if (e.CurState != UNIT_STATE.DIE)
				{
					allDead = false;
					break;
				}
			}

			if (allDead)
			{
				break;
			}
		}

		yield return GameManager.WaitGameplaySeconds(wave.nextWaveDelay);
		AdvanceWave(wave);
	}

	/// <summary>
	/// 새 웨이브 소환을 멈춤. 스테이지 클리어 시퀀스가 시작될 때 호출됨.
	/// 이미 나와 있는 적은 건드리지 않음 — 정리는 GameManager가 함.
	/// </summary>
	public void StopWaves()
	{
		_wavesStopped = true;
		StopAllCoroutines();
		_bossWaveActive = false;
	}

	private void AdvanceWave(WaveData wave)
	{
		// 보스 웨이브를 클리어했으면 남은 일반 웨이브는 전부 무시하고 즉시 스테이지 클리어.
		if (_bossWaveActive)
		{
			_bossWaveActive = false;
			BroadcastStageClear();
			return;
		}

		int nextIndex = _currentWaveIndex + 1;
		if (nextIndex < waves.Length)
		{
			StartWave(nextIndex);
		}
		else
		{
			BroadcastStageClear();
		}
	}

	// 클리어는 '방'에서 일어난 사건이라 전원이 같이 받아야 함.
	// 웨이브 코루틴은 방장만 돌아서 여기도 방장만 도달함 — 그냥 StageClear()를 부르면 방장 화면에서만 클리어되고
	// 게스트는 스테이지에 갇힘. 그래서 이벤트로 전원에게 알리고 각자 로컬에서 StageClear()를 실행함.
	private void BroadcastStageClear()
	{
		// 로컬(방장/싱글)에서 먼저 처리하고, 멀티면 남 클라에도 RPC로 전파.
		GameManager.Instance?.StageClear();

		if (PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode)
		{
			photonView.RPC(nameof(RpcStageClear), RpcTarget.Others);
		}
	}

	// 남 클라 수신 — 방장이 클리어했으면 각자 로컬에서도 스테이지 클리어 처리.
	[PunRPC]
	private void RpcStageClear()
	{
		GameManager.Instance?.StageClear();
	}

	// ================================================================
	// 보스 웨이브
	// ================================================================
	private void OnBossSpawnTriggered()
	{
		// 보스 스폰도 Master 권위.
		if (!PhotonNetwork.IsMasterClient || bossWave == null || _wavesStopped)
		{
			return;
		}
		// 보스 웨이브 진입 — 클리어하면 남은 일반 웨이브는 무시하고 스테이지 클리어로 감(AdvanceWave 참고).
		// _currentWaveIndex는 방장 승계 시 기준점으로만 남겨둠.
		_bossWaveActive = true;
		PublishWaveState(_currentWaveIndex, true);
		_waveEnemies.Clear();
		StopAllCoroutines();
		StartCoroutine(SpawnWaveRoutine(bossWave));
	}

	// ================================================================
	// 방장 승계
	// ================================================================
	// 방장이 나가면 새 방장이 웨이브를 이어받음.
	// 적/드랍은 룸 종속(InstantiateRoomObject)이라 파괴되지 않고 IsMine만 새 방장으로 넘어오므로,
	// 여기서 다시 스폰하면 중복됨 → 재스폰 안 하고 '살아있는 적의 전멸 감시'만 인계받아 다음 웨이브로 진행함.
	// 한계: 이전 방장이 스폰 도중(예: 10마리 중 3마리)에 나갔다면 남은 스폰분은 유실됨(부분 웨이브로 진행).
	public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
	{
		if (!PhotonNetwork.IsMasterClient)
		{
			return;
		}
		ResumeWaveAsNewMaster();
	}

	private void ResumeWaveAsNewMaster()
	{
		bool isBossWave = ReadRoomBool(BossWavePropertyKey);
		_currentWaveIndex = ReadRoomInt(WaveIndexPropertyKey, _currentWaveIndex);
		// 보스 웨이브 중에 방장이 바뀌어도 "클리어하면 스테이지 클리어" 규칙이 유지되도록 플래그도 인계.
		_bossWaveActive = isBossWave;

		WaveData activeWave = isBossWave ? bossWave : GetNormalWave(_currentWaveIndex);
		if (activeWave == null)
		{
			return;
		}

		// 룸 종속으로 살아남은 적을 인계받아 클리어 판정 대상으로 삼음.
		_waveEnemies.Clear();
		Enemy[] aliveEnemies = FindObjectsOfType<Enemy>();
		foreach (Enemy enemy in aliveEnemies)
		{
			if (enemy != null && enemy.CurState != UNIT_STATE.DIE)
			{
				_waveEnemies.Add(enemy);
			}
		}

		StopAllCoroutines();
		StartCoroutine(WaitForWaveClear(activeWave));
	}

	// 현재 웨이브 진행도를 룸에 기록(방장만 호출) — 새 방장이 이걸 읽고 이어받음.
	private void PublishWaveState(int waveIndex, bool isBossWave)
	{
		if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
		{
			return;
		}
		PhotonHashtable waveState = new PhotonHashtable
		{
			{ WaveIndexPropertyKey, waveIndex },
			{ BossWavePropertyKey, isBossWave }
		};
		PhotonNetwork.CurrentRoom.SetCustomProperties(waveState);
	}

	private WaveData GetNormalWave(int waveIndex)
	{
		if (waves == null || waveIndex < 0 || waveIndex >= waves.Length)
		{
			return null;
		}
		return waves[waveIndex];
	}

	private int ReadRoomInt(string key, int fallback)
	{
		if (PhotonNetwork.CurrentRoom == null ||
			!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value))
		{
			return fallback;
		}
		return value is int intValue ? intValue : fallback;
	}

	private bool ReadRoomBool(string key)
	{
		if (PhotonNetwork.CurrentRoom == null ||
			!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value))
		{
			return false;
		}
		return value is bool boolValue && boolValue;
	}
}
