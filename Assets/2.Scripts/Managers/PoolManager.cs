// ================================================================
// [외부 참조 가이드]
// ================================================================
// ▶ 투사체 / 전투팀 참조용
//   GetBullet()                    : Bullet 꺼내기
//   GetMissile()                   : Missile 꺼내기
//   GetLaser()                     : Laser 꺼내기
//   GetProjectile(PROJECTILE_TYPE) : 타입으로 투사체 꺼내기 (Player/Enemy 공용)
//   Get(POOL_TYPE)                 : 일반 오브젝트 꺼내기 (적, 아이템 등)
//   Return(GameObject)             : 오브젝트 반납 (비활성화)
//
//   예시)
//   Bullet b = PoolManager.Instance.GetBullet();
//   b.Init(firePos.position, firePos.forward, this);
//   // 투사체 자체 반납: PoolManager.Instance.Return(gameObject);
//
// ▶ 씬 전환 / 게임오버 참조용
//   DisableAllProjectiles() : 씬 전환·게임오버 시 GameManager에서 호출
//   DisableAll(POOL_TYPE)   : 특정 타입 전체 비활성화
// ================================================================

using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// PoolEntry
// 인스펙터에서 풀 종류 / 프리팹 / 초기 사이즈를 설정하는 직렬화 클래스.
// PoolManager.poolConfigs 배열에 항목을 추가해서 등록.
// =====================================================================
[System.Serializable]
public class PoolEntry
{
    [Tooltip("풀 종류. enum_Types.cs의 POOL_TYPE 값 선택.")]
    public POOL_TYPE poolType;

    [Tooltip("생성할 프리팹. 인스펙터에서 연결.")]
    public GameObject prefab;

    [Tooltip("게임 시작 시 미리 생성할 개수. 부족하면 자동 확장 + 경고 로그.")]
    public int initialSize = 20;
}

// =====================================================================
// PoolManager
//
// 역할:
//   1. 게임 시작 시 poolConfigs 배열을 순회하여 풀 초기화
//   2. Get(POOL_TYPE) 으로 비활성 오브젝트 꺼내기
//   3. Return(GameObject) 으로 반납 (비활성화)
//   4. 풀 부족 시 자동 확장 + 경고 로그
//   5. DisableAllProjectiles(): 씬전환/게임오버 시 투사체 전체 비활성화
//
// 투사체 전용 캐시(_projectilePools):
//   초기화 시점에 GetComponent<Projectile>()을 미리 수행해 저장.
//   GetBullet/GetMissile 등 호출 시 매번 GetComponent 하지 않아도 됨.
//
// 추가 방법 (신규 타입 등록 시):
//   1. enum_Types.cs 의 POOL_TYPE에 값 추가
//   2. 인스펙터 poolConfigs 배열에 항목 추가, 프리팹 연결, 사이즈 설정
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
    [Header("======= 풀 설정=======")]
    [Tooltip("배열 항목 추가 후 POOL_TYPE / 프리팹 / 초기 사이즈 설정으로 풀 등록")]
    public PoolEntry[] poolConfigs;

    // =====================================================================
    // 내부 풀 딕셔너리
    // =====================================================================
    // 전체 타입 공통 (GameObject 기준)
    private Dictionary<POOL_TYPE, List<GameObject>> _pools = new Dictionary<POOL_TYPE, List<GameObject>>();

    // 투사체 전용 캐싱(기존 InitPool에서 계속 GetBullet
    private Dictionary<POOL_TYPE, List<Projectile>> _projectilePools = new Dictionary<POOL_TYPE, List<Projectile>>();

    // DisableAllProjectiles() 대상 목록 (투사체만)
    private static POOL_TYPE[] _projectileTypes =
    {
        POOL_TYPE.BULLET,
        POOL_TYPE.MISSILE,
        POOL_TYPE.LASER,
        POOL_TYPE.CLUSTER_MISSILE_BASE,
        POOL_TYPE.CLUSTER_MISSILE_CHILDREN,
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
            Debug.LogWarning("[PoolManager] 중복 감지. 기존 인스턴스 유지, 새 오브젝트 파괴");
            Destroy(gameObject);
        }
    }

    /// <summary>poolConfigs 순회 후 각 풀 초기화</summary>
    private void InitPools()
    {
        foreach (var config in poolConfigs)
        {
            if (config.prefab == null)
            {
                Debug.LogError($"[PoolManager] {config.poolType} prefab 미연결! 인스펙터 확인 필요");
                continue;
            }

            bool isProjectile = System.Array.IndexOf(_projectileTypes, config.poolType) >= 0;

            var pool = new List<GameObject>(config.initialSize);

            if (isProjectile)
                _projectilePools[config.poolType] = new List<Projectile>(config.initialSize);

            for (int i = 0; i < config.initialSize; i++)
            {
                // this.transform 지정 , 씬전환 시 소멸 방지
                GameObject obj = Instantiate(config.prefab, this.transform);
                obj.SetActive(false);
                pool.Add(obj);

                // 투사체 타입이면 Projectile 컴포넌트를 초기화 시점에 캐싱
                // 다른 아이템등 많이 사용될것들도 차후 추가 캐싱
                if (isProjectile)
                {
                    Projectile proj = obj.GetComponent<Projectile>();
                    if (proj != null)
                        _projectilePools[config.poolType].Add(proj);
                }

                //if(isItem)어쩌구
            }
            _pools[config.poolType] = pool;
        }
    }

    // =====================================================================
    // 범용 Get / Return
    // 적, 아이템, 이펙트 등 GameObject 반환이 필요한 경우 직접 사용
    // =====================================================================

    /// <summary>
    /// 해당 POOL_TYPE의 비활성 오브젝트 반환.
    /// 풀 부족 시 자동 확장 후 반환.
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

        // 풀 부족 → 자동 확장
        PoolEntry config = System.Array.Find(poolConfigs, c => c.poolType == poolType);
        if (config == null)
        {
            Debug.LogError($"[PoolManager] {poolType} config null - poolConfigs 확인");
            return null;
        }

        // this.transform 지정 → DontDestroyOnLoad 씬에 귀속
        GameObject newObj = Instantiate(config.prefab, this.transform);
        pool.Add(newObj);

        // 투사체 타입이면 Projectile 캐시에도 추가
        bool isProjectile = System.Array.IndexOf(_projectileTypes, poolType) >= 0;
        if (isProjectile && _projectilePools.ContainsKey(poolType))
        {
            Projectile proj = newObj.GetComponent<Projectile>();
            if (proj != null)
                _projectilePools[poolType].Add(proj);
        }

        Debug.LogWarning($"[PoolManager] {poolType} 자동 확장. 현재 수: {pool.Count}");
        newObj.SetActive(true);
        return newObj;
    }

    /// <summary>오브젝트를 풀로 반납 (비활성화). 모든 타입 공용.</summary>
    public void Return(GameObject obj)
    {
        obj.SetActive(false);
    }

    // =====================================================================
    // 투사체 전용 Get (GetComponent 캐싱 적용)
    // 발사 빈도가 높으므로 매 호출 시 GetComponent 하지 않음.
    // 기존 GetBullet/GetMissile 등과 호출 방법 동일.
    // =====================================================================

    /// <summary>
    /// 투사체 캐시에서 비활성 Projectile 반환.
    /// 캐시 소진 시 Get()으로 자동 확장.
    /// </summary>
    private Projectile GetCachedProjectile(POOL_TYPE poolType)
    {
        if (_projectilePools.TryGetValue(poolType, out var projPool))
        {
            foreach (var proj in projPool)
            {
                if (proj != null && !proj.gameObject.activeInHierarchy)
                {
                    proj.gameObject.SetActive(true);
                    return proj;
                }
            }
        }

        // 캐시 소진 → Get()으로 확장 (내부에서 캐시도 같이 추가됨)
        GameObject newObj = Get(poolType);
        if (newObj == null) return null;
        return newObj.GetComponent<Projectile>();
    }

	// 기존 호출부 변경 없이 사용 가능
	public Bullet GetBullet()
	{
		return GetCachedProjectile(POOL_TYPE.BULLET) as Bullet;
	}

	public Missile GetMissile()
	{
		return GetCachedProjectile(POOL_TYPE.MISSILE) as Missile;
	}

	public Laser GetLaser()
	{
		return GetCachedProjectile(POOL_TYPE.LASER) as Laser;
	}

	/// <summary>
	/// 클러스터 미사일. 꺼낸 후 반드시 Init() 호출.
	/// </summary>
	public ClusterMissile GetClusterMissile()
	{
		return GetCachedProjectile(POOL_TYPE.CLUSTER_MISSILE_BASE) as ClusterMissile;
	}
	/// <summary>
	/// 클러스터 미사일 분열 자탄. 꺼낸 후 반드시 Init() 호출.
	/// </summary>
	public Missile GetClusterMissileChild()
	{
		return GetCachedProjectile(POOL_TYPE.CLUSTER_MISSILE_CHILDREN) as Missile;
	}
	/// <summary>
	/// 유도없는 미사일. 꺼낸 후 반드시 Init() 호출.
	/// </summary>
	public DumbMissile GetDumbMissile()
	{
		return GetCachedProjectile(POOL_TYPE.DUMB_MISSILE) as DumbMissile;
	}


    /// <summary>POOL_TYPE으로 투사체 꺼내기. 변형탄(MissileData.curMissilePoolType) 등 직접 풀 지정용.</summary>
    public Projectile GetProjectile(POOL_TYPE poolType)
	{
		return GetCachedProjectile(poolType);
	}

	/// <summary>PROJECTILE_TYPE으로 투사체 꺼내기. Player/Enemy 공용.</summary>
	public Projectile GetProjectile(PROJECTILE_TYPE shootType)
    {
        switch (shootType)
        {
            case PROJECTILE_TYPE.BULLET:  return GetBullet();
            case PROJECTILE_TYPE.MISSILE: return GetMissile();
            case PROJECTILE_TYPE.LASER:   return GetLaser();
            default:
                Debug.LogWarning($"[PoolManager] GetProjectile: 미지원 타입 {shootType}");
                return null;
        }
    }

    /// <summary>Projectile.ReturnToPool()에서 호출. 비활성화로 반납.</summary>
    public void ReturnProjectile(Projectile projectile)
    {
        projectile.gameObject.SetActive(false);
    }

    // =====================================================================
    // 씬 전환 / 게임오버 시 투사체 전체 비활성화
    // =====================================================================

    /// <summary>
    /// 씬 전환/게임오버 시 GameManager에서 호출.
    /// 투사체(_projectileTypes) 전체 비활성화.
    /// </summary>
    public void DisableAllProjectiles()
    {
        foreach (var type in _projectileTypes)
            DisableProjectiles(type);
    }

    /// <summary>특정 POOL_TYPE 전체 비활성화.</summary>
    public void DisableProjectiles(POOL_TYPE poolType)
    {
        if (_pools.TryGetValue(poolType, out var pool))
        {
            foreach (var obj in pool)
            {
                obj.SetActive(false);
            }
        }
    }

	// 개별 비활성화 (필요 시 외부에서 직접 호출)
	public void DisableBullet()
	{
		DisableProjectiles(POOL_TYPE.BULLET);
	}

	public void DisableMissile()
	{
		DisableProjectiles(POOL_TYPE.MISSILE);
	}

	public void DisableLaser()
	{
		DisableProjectiles(POOL_TYPE.LASER);
	}
}