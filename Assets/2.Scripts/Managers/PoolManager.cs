using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// PoolEntry
// 인스펙터에서 풀 종류 / 프리팹 / 초기 사이즈를 설정하는 직렬화 클래스.
// PoolManager.poolConfigs 배열에 항목을 추가해 새 풀을 등록.
// =====================================================================
[System.Serializable]
public class PoolEntry
{
    [Tooltip("풀 종류. enum_Types.cs의 POOL_TYPE 값 선택.")]
    public POOL_TYPE poolType;

    [Tooltip("생성할 프리팹. 인스펙터에서 드래그앤드롭.")]
    public GameObject prefab;

    [Tooltip("씬 시작 시 미리 생성할 오브젝트 수. 부족하면 자동 확장 + 경고 로그.")]
    public int initialSize = 20;
}

// =====================================================================
// PoolManager
//
// 역할:
//   1. 씬 시작 시 poolConfigs 배열을 순회해 풀 초기화
//   2. Get(POOL_TYPE) 으로 비활성 오브젝트 꺼내기
//   3. Return(GameObject) 으로 반납 (비활성화)
//   4. 풀 소진 시 자동 확장 + 경고 로그
//   5. DisableAllProjectiles(): 씬 전환/게임오버 시 투사체 일괄 비활성화
//
// ※ 새 풀 추가 방법 (코드 수정 불필요):
//   1. enum_Types.cs → POOL_TYPE에 값 추가
//   2. 인스펙터 poolConfigs 배열에 항목 추가, 프리팹 연결, 사이즈 설정
//
// 투사체 전용 래퍼: GetBullet / GetMissile / GetLaser / GetClusterMissile / GetDumbMissile
//   → 기존 코드와 호환 유지 (내부적으로 Get(POOL_TYPE) 호출)
// 적 / 아이템 / 이펙트: Get(POOL_TYPE.ENEMY_GUNSHIP) 등 범용 메서드 직접 사용
// =====================================================================
public class PoolManager : MonoBehaviour
{
    // =====================================================================
    // 싱글톤
    // =====================================================================
    private static PoolManager instance = null;
    public static PoolManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<PoolManager>();
                if (instance == null)
                    Debug.LogError("[PoolManager] 씬에 PoolManager 없음! 하이어라키에 추가 필요");
            }
            return instance;
        }
    }

    // =====================================================================
    // 인스펙터 설정
    // =====================================================================
    [Header("━━━━━━ 풀 설정 ━━━━━━")]
    [Tooltip("배열 항목 추가 → POOL_TYPE / 프리팹 / 초기 사이즈 설정으로 새 풀 등록")]
    public PoolEntry[] poolConfigs;

    // =====================================================================
    // 런타임 풀 (Awake에서 poolConfigs 기반으로 구성)
    // =====================================================================
    private Dictionary<POOL_TYPE, List<GameObject>> _pools
        = new Dictionary<POOL_TYPE, List<GameObject>>();

    // DisableAllProjectiles() 대상 타입 목록 (투사체 전용)
    private static readonly POOL_TYPE[] _projectileTypes =
    {
        POOL_TYPE.BULLET,
        POOL_TYPE.MISSILE,
        POOL_TYPE.LASER,
        POOL_TYPE.CLUSTER_MISSILE,
        POOL_TYPE.DUMB_MISSILE,
    };

    // =====================================================================
    // 초기화
    // =====================================================================
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitPools();
        }
        else if (instance != this)
        {
            Debug.LogWarning("[PoolManager] 중복 감지. 기존 인스턴스 유지, 이 오브젝트 파괴");
            Destroy(gameObject);
        }
    }

    /// <summary>poolConfigs 순회 → 각 풀 초기화.</summary>
    private void InitPools()
    {
        foreach (var config in poolConfigs)
        {
            if (config.prefab == null)
            {
                Debug.LogError($"[PoolManager] {config.poolType} prefab이 비어있음! 인스펙터에서 연결 필요");
                continue;
            }

            var pool = new List<GameObject>(config.initialSize);
            for (int i = 0; i < config.initialSize; i++)
            {
                GameObject obj = Instantiate(config.prefab);
                obj.SetActive(false);
                pool.Add(obj);
            }
            _pools[config.poolType] = pool;
        }
    }

    // =====================================================================
    // 범용 Get / Return
    // 적, 아이템, 이펙트 등 투사체 외 타입은 이 메서드로 사용
    // =====================================================================

    /// <summary>
    /// 해당 POOL_TYPE의 비활성 오브젝트 반환.
    /// 풀 소진 시 자동 확장 후 반환.
    /// </summary>
    public GameObject Get(POOL_TYPE poolType)
    {
        if (!_pools.TryGetValue(poolType, out var pool))
        {
            Debug.LogError($"[PoolManager] 풀 없음: {poolType}. poolConfigs에 등록 필요.");
            return null;
        }

        // 비활성 오브젝트 탐색
        foreach (var obj in pool)
        {
            if (!obj.activeInHierarchy)
            {
                obj.SetActive(true);
                return obj;
            }
        }

        // 풀 소진 → 자동 확장
        PoolEntry config = System.Array.Find(poolConfigs, c => c.poolType == poolType);
        GameObject newObj = Instantiate(config.prefab);
        pool.Add(newObj);
        Debug.LogWarning($"[PoolManager] {poolType} 풀 소진. 자동 확장. 현재 크기: {pool.Count}");
        newObj.SetActive(true);
        return newObj;
    }

    /// <summary>오브젝트를 풀로 반납 (비활성화). 모든 타입 공용.</summary>
    public void Return(GameObject obj)
    {
        obj.SetActive(false);
    }

    // =====================================================================
    // 투사체 전용 래퍼 (기존 코드 호환 유지)
    // 내부적으로 Get(POOL_TYPE) 호출 후 컴포넌트 캐스팅
    // =====================================================================

    /// <summary>
    /// PROJECTILE_TYPE → 해당 Projectile 하위 컴포넌트 반환.
    /// Player/Enemy 발사 로직에서 사용.
    /// </summary>
    public Projectile GetProjectile(PROJECTILE_TYPE shootType)
    {
        switch (shootType)
        {
            case PROJECTILE_TYPE.BULLET:  return GetBullet();
            case PROJECTILE_TYPE.MISSILE: return GetMissile();
            case PROJECTILE_TYPE.LASER:   return GetLaser();
            default:
                Debug.LogWarning($"[PoolManager] GetProjectile: 미처리 타입 {shootType}");
                return null;
        }
    }

    // 클러스터/덤 미사일은 별도 Get 메서드 유지
    // (PROJECTILE_TYPE에 없고 발사 로직이 따로 있어서)
    public Bullet         GetBullet()          => Get(POOL_TYPE.BULLET)?.GetComponent<Bullet>();
    public Missile        GetMissile()         => Get(POOL_TYPE.MISSILE)?.GetComponent<Missile>();
    public Laser          GetLaser()           => Get(POOL_TYPE.LASER)?.GetComponent<Laser>();

    /// <summary>
    /// 클러스터 미사일. 폭발 시 MultiLockedTargets 수만큼 HOMING 분열 발사 예정.
    /// 꺼낸 후 반드시 Init() 호출.
    /// </summary>
    public ClusterMissile GetClusterMissile()  => Get(POOL_TYPE.CLUSTER_MISSILE)?.GetComponent<ClusterMissile>();

    /// <summary>
    /// 덤 미사일. 직진 후 대범위 폭발 예정.
    /// 꺼낸 후 반드시 Init() 호출.
    /// </summary>
    public DumbMissile    GetDumbMissile()     => Get(POOL_TYPE.DUMB_MISSILE)?.GetComponent<DumbMissile>();

    /// <summary>Projectile.ReturnToPool()에서 호출. 기존 인터페이스 유지.</summary>
    public void ReturnProjectile(Projectile projectile)
    {
        projectile.gameObject.SetActive(false);
    }

    // =====================================================================
    // 씬 전환 / 게임오버 시 일괄 비활성화
    // =====================================================================

    /// <summary>
    /// 씬 전환/게임오버 시 GameManager에서 호출.
    /// 투사체 타입(_projectileTypes)만 일괄 비활성화.
    /// </summary>
    public void DisableAllProjectiles()
    {
        foreach (var type in _projectileTypes)
            DisableAll(type);
    }

    /// <summary>특정 POOL_TYPE 전체 비활성화.</summary>
    public void DisableAll(POOL_TYPE poolType)
    {
        if (_pools.TryGetValue(poolType, out var pool))
            foreach (var obj in pool)
                obj.SetActive(false);
    }

    // 개별 비활성화 (필요 시 외부에서 직접 호출)
    public void DisableBullet()  => DisableAll(POOL_TYPE.BULLET);
    public void DisableMissile() => DisableAll(POOL_TYPE.MISSILE);
    public void DisableLaser()   => DisableAll(POOL_TYPE.LASER);
}
