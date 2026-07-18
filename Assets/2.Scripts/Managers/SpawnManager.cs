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
// 스폰 방식 (WaveData.SpawnEntry.method):
//   ScenePlaced : 씬에 미리 배치·비활성화된 적 SetActive(true)
//   RandomSpawn : 지정 스폰포인트에 프리팹 Instantiate
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
public class SpawnManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
	// 스테이지 클리어 알림용(WaitingRoomUI=71, MapSelectorUI=72와 겹치지 않는 코드).
	private const byte StageClearEventCode = 73;

	// ================================================================
	// 싱글톤 (씬 전용 — DontDestroyOnLoad 없음)
	// ================================================================
	private static SpawnManager instance;
	// Awake에서만 세팅됨. Awake 전엔 null이므로 최초 접근은 Start부터 할 것.
	// (예전엔 여기서 FindObjectOfType으로 찾아줬는데, 그게 매니저 자신의 Awake보다 먼저
	//  instance를 채워버려서 Awake의 초기화 블록이 통째로 스킵되는 버그를 만들었음)
	public static SpawnManager Instance => instance;

	private void Awake()
	{
		if (instance == null)
		{
			instance = this;
		}
		else if (instance != this)
		{
			Debug.LogWarning("[SpawnManager] 중복 감지. 파괴 후 기존 유지");
			Destroy(gameObject);
		}
	}

	// ================================================================
	// 인스펙터
	// ================================================================
	[Header("웨이브 설정")]
	[Tooltip("순서대로 실행될 WaveData 목록. 전부 소진 시 StageClear 호출.")]
	public WaveData[] waves;
	[Tooltip("RandomSpawn SpawnEntry에 spawnPoint 미지정 시 랜덤 선택할 기본 스폰포인트.")]
	public Transform[] defaultSpawnPoints;

	[Header("보스 웨이브")]
	[Tooltip("GameManager.onBossSpawn 이벤트 발생 시 실행할 WaveData. null이면 스킵.")]
	public WaveData bossWave;

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
		instance = null;
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
		if (!PhotonNetwork.IsMasterClient)
		{
			return;
		}
		if (waves == null || waveIndex < 0 || waveIndex >= waves.Length)
		{
			return;
		}
		_currentWaveIndex = waveIndex;
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

		foreach (WaveData.SpawnEntry entry in wave.entries)
		{
			if (entry.delay > 0f)
			{
				// 일시정지 인지 대기 — 프리즈 동안 시간이 안 흐름(WaitForSeconds는 플래그 정지 무시).
				yield return GameManager.WaitGameplaySeconds(entry.delay);
			}

			if (entry.method == SpawnMethod.ScenePlaced)
			{
				SpawnScenePlaced(entry);
			}
			else
			{
				yield return StartCoroutine(SpawnRandom(entry));
			}
		}

		yield return StartCoroutine(WaitForWaveClear(wave));
	}

	// ================================================================
	// 스폰 처리
	// ================================================================
	private void SpawnScenePlaced(WaveData.SpawnEntry entry)
	{
		if (entry.scenePlacedEnemies == null)
		{
			return;
		}
		foreach (GameObject go in entry.scenePlacedEnemies)
		{
			if (go == null)
			{
				continue;
			}
			go.SetActive(true);
			Enemy enemy = go.GetComponent<Enemy>();
			if (enemy != null)
			{
				_waveEnemies.Add(enemy);
			}
		}
	}

	private IEnumerator SpawnRandom(WaveData.SpawnEntry entry)
	{
		if (PoolManager.Instance == null)
		{
			yield break;
		}

		if (entry.spawnPoint == null && defaultSpawnPoints != null && defaultSpawnPoints.Length > 0)
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

			Vector3 pos = (entry.spawnPoint == null && _shuffleBuffer != null)
				? _shuffleBuffer[i % _shuffleBuffer.Length].position
				: GetSpawnPosition(entry.spawnPoint);

			// Master 권위 네트워크 스폰 — 모든 클라에 같은 적이 생성됨(PhotonPoolAdapter가 로컬 풀로 라우팅).
			// prefabId = POOL_TYPE 이름(어댑터가 파싱해 풀에서 꺼냄). 위치는 Instantiate가 설정.
			// 적은 '룸 종속'이라 RoomObject로 만듦 — 방장이 나가도 살아남고 소유권이 새 방장에게 넘어감.
			// 마지막 인자로 '이 적이 태어난 씬'을 같이 보냄 — 다른 씬에 있는 클라가 이걸 보고 숨김(EnemySceneVisibility).
			// 방장의 '현재 씬'이 아니라 '스폰 시점 씬'인 이유: 방장이 스테이션으로 돌아가도 스테이지 적은 그대로 보여야 함.
			GameObject go = PhotonNetwork.InstantiateRoomObject(
				entry.poolType.ToString(), pos, Quaternion.identity, 0,
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

	private void AdvanceWave(WaveData wave)
	{
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
		// 싱글(오프라인)이거나 룸 밖이면 그냥 로컬 처리.
		if (!PhotonNetwork.InRoom || PhotonNetwork.OfflineMode)
		{
			GameManager.Instance?.StageClear();
			return;
		}

		RaiseEventOptions eventOptions = new RaiseEventOptions
		{
			Receivers = ReceiverGroup.All
		};
		PhotonNetwork.RaiseEvent(StageClearEventCode, null, eventOptions, SendOptions.SendReliable);
	}

	public void OnEvent(EventData photonEvent)
	{
		if (photonEvent.Code != StageClearEventCode || !PhotonNetwork.InRoom)
		{
			return;
		}

		// 방장이 보낸 것만 신뢰(비방장 위조 차단).
		Photon.Realtime.Player masterClient = PhotonNetwork.MasterClient;
		if (masterClient == null || photonEvent.Sender != masterClient.ActorNumber)
		{
			return;
		}

		GameManager.Instance?.StageClear();
	}

	// ================================================================
	// 보스 웨이브
	// ================================================================
	private void OnBossSpawnTriggered()
	{
		// 보스 스폰도 Master 권위.
		if (!PhotonNetwork.IsMasterClient || bossWave == null)
		{
			return;
		}
		// _currentWaveIndex는 그대로 둠(보스 클리어 후 AdvanceWave가 이어갈 기준점).
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

	// ================================================================
	// 유틸
	// ================================================================
	private Vector3 GetSpawnPosition(Transform spawnPoint)
	{
		if (spawnPoint != null)
		{
			return spawnPoint.position;
		}
		if (defaultSpawnPoints != null && defaultSpawnPoints.Length > 0)
		{
			return defaultSpawnPoints[Random.Range(0, defaultSpawnPoints.Length)].position;
		}
		return transform.position;
	}
}
