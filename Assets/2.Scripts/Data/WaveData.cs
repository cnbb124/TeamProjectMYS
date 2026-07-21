using UnityEngine;



// 한 웨이브의 구성 정의.
// SpawnManager.waves[]에 순서대로 연결.
[CreateAssetMenu(fileName = "New WaveData", menuName = "Create Data/WaveData" )]
public class WaveData : ScriptableObject
{
    [System.Serializable]
    public class SpawnEntry
    {
       

        [Header("<size=14>풀 설정 (Enemy)</size>")]
        [Tooltip("스폰할 적의 PoolManager.POOL_TYPE. PoolManager.poolConfigs에 해당 타입+프리팹이 등록돼있어야 함.")]
        public POOL_TYPE poolType;
        
        
        //[Tooltip("참고용 프리팹 표시 — 실제 스폰에는 사용 안 됨(poolType이 실제 스폰 기준). " +
        //         "PoolManager.poolConfigs에 등록한 프리팹과 같은 걸로 맞춰둘 것.")]
        //public GameObject enemyPrefab;
        [Tooltip("스폰 지점 = SpawnManager.defaultSpawnPoints의 인덱스(0부터). -1이면 그 목록 중 랜덤.")]
        public int spawnPointIndex = -1;
        [Tooltip("스폰할 수")]
        public int count = 1;
        [Tooltip("연속 스폰 간격 (초)")]
        public float interval = 0.5f;

     

        [Header("<size=14>각 개체 소환 딜레이</size>")]
        [Tooltip("웨이브 시작 후 Entries간의 처리까지 지연 (초)")]
        public float delay = 0f;
    }

    [Header("<size=18>웨이브 Enemy 목록</size>")]
    [Tooltip("이 웨이브에서 처리할 스폰 항목 목록. 순서대로 처리됨.")]
    public SpawnEntry[] entries;
    [Tooltip("웨이브 클리어 후 다음 웨이브 시작까지 대기 시간 (초)")]
    public float nextWaveDelay = 3f;
}
