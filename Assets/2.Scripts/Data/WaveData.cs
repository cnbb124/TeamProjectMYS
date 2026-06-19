using UnityEngine;

// 스폰 방식.
// ScenePlaced : 씬에 미리 배치된 오브젝트 SetActive(true).
// RandomSpawn : 지정 스폰포인트에 프리팹 Instantiate.
public enum SpawnMethod { ScenePlaced, RandomSpawn }

// 한 웨이브의 구성 정의.
// SpawnManager.waves[]에 순서대로 연결.
[CreateAssetMenu(menuName = "Data/WaveData", fileName = "WaveData")]
public class WaveData : ScriptableObject
{
    [System.Serializable]
    public class SpawnEntry
    {
        [Tooltip("씬 배치형(SetActive) vs 랜덤 스폰(Instantiate)")]
        public SpawnMethod method = SpawnMethod.RandomSpawn;

        [Header("RandomSpawn 전용")]
        [Tooltip("스폰할 적 프리팹")]
        public GameObject enemyPrefab;
        [Tooltip("스폰 지점. null이면 SpawnManager의 defaultSpawnPoints 중 랜덤 선택.")]
        public Transform spawnPoint;
        [Tooltip("스폰할 수")]
        public int count = 1;
        [Tooltip("연속 스폰 간격 (초)")]
        public float interval = 0.5f;

        [Header("ScenePlaced 전용")]
        [Tooltip("씬에 미리 배치되어 비활성화된 적 오브젝트 목록")]
        public GameObject[] scenePlacedEnemies;

        [Header("공통")]
        [Tooltip("웨이브 시작 후 이 항목 처리까지 지연 (초)")]
        public float delay = 0f;
    }

    [Tooltip("이 웨이브에서 처리할 스폰 항목 목록. 순서대로 처리됨.")]
    public SpawnEntry[] entries;
    [Tooltip("웨이브 클리어 후 다음 웨이브 시작까지 대기 시간 (초)")]
    public float nextWaveDelay = 3f;
}
