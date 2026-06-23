using UnityEngine;



// 한 웨이브의 구성 정의.
// SpawnManager.waves[]에 순서대로 연결.
[CreateAssetMenu(fileName = "New WaveData", menuName = "Create Data/WaveData" )]
public class WaveData : ScriptableObject
{
    [System.Serializable]
    public class SpawnEntry
    {
        [Header("<size=18>둘 중 하나만 적용</size>")]
        [Tooltip("씬에 미리 배치후 활성화 VS 풀 매니저 스폰")]
        public SpawnMethod method = SpawnMethod.PoolSpawn;

        [Header("<size=14>풀 스폰 전용(Enemy)</size>")]
        [Tooltip("스폰할 적의 PoolManager.POOL_TYPE. PoolManager.poolConfigs에 해당 타입+프리팹이 등록돼있어야 함.")]
        public POOL_TYPE poolType;
        
        
        //[Tooltip("참고용 프리팹 표시 — 실제 스폰에는 사용 안 됨(poolType이 실제 스폰 기준). " +
        //         "PoolManager.poolConfigs에 등록한 프리팹과 같은 걸로 맞춰둘 것.")]
        //public GameObject enemyPrefab;
        [Tooltip("스폰 지점. 등록안하면 SpawnManager의 defaultSpawnPoints 중 랜덤 선택.")]
        public Transform spawnPoint;
        [Tooltip("스폰할 수")]
        public int count = 1;
        [Tooltip("연속 스폰 간격 (초)")]
        public float interval = 0.5f;

        [Header("<size=14>씬 배치형 전용</size>")]
        [Tooltip("씬에 미리 배치되어 하이어라키에 있는 비활성화된 적 오브젝트를 Drag&Drop")]
        public GameObject[] scenePlacedEnemies;

        [Header("<size=18>공통</size>")]
        [Tooltip("웨이브 시작 후 Entries간의 처리까지 지연 (초)")]
        public float delay = 0f;
    }

    [Header("<size=22>웨이브 Enemy 목록</size>")]
    [Tooltip("이 웨이브에서 처리할 스폰 항목 목록. 순서대로 처리됨.")]
    public SpawnEntry[] entries;
    [Tooltip("웨이브 클리어 후 다음 웨이브 시작까지 대기 시간 (초)")]
    public float nextWaveDelay = 3f;
}
