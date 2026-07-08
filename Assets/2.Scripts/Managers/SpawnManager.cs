using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
// ================================================================
public class SpawnManager : MonoBehaviour
{
	// ================================================================
	// 싱글톤 (씬 전용 — DontDestroyOnLoad 없음)
	// ================================================================
	private static SpawnManager instance;
	public static SpawnManager Instance
	{
		get
		{
			if (instance == null)
			{
				instance = FindObjectOfType<SpawnManager>();
				if (instance == null)
				{
					Debug.LogError("[SpawnManager] 씬에 SpawnManager 없음! 하이어라키에 추가 필요");
				}
			}
			return instance;
		}
	}

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
		if (waves == null || waveIndex < 0 || waveIndex >= waves.Length)
		{
			return;
		}
		_currentWaveIndex = waveIndex;
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

			GameObject go = PoolManager.Instance.Get(entry.poolType);
			if (go == null)
			{
				continue;
			}
			go.transform.SetPositionAndRotation(pos, Quaternion.identity);
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
			GameManager.Instance?.StageClear();
		}
	}

	// ================================================================
	// 보스 웨이브
	// ================================================================
	private void OnBossSpawnTriggered()
	{
		if (bossWave == null)
		{
			return;
		}
		_waveEnemies.Clear();
		StopAllCoroutines();
		StartCoroutine(SpawnWaveRoutine(bossWave));
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
