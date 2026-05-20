using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class PoolManager : MonoBehaviour
{

	private static PoolManager instance = null;

	public static PoolManager Instance
	{
		get
		{
			if (instance == null)
			{
				instance = FindObjectOfType<PoolManager>();
				if (instance == null)
				{
					Debug.LogError("씬에 PoolMnager누락! 하이어라키에 풀매니저 필요");
				}
			}
			return instance;
		}
	}


	// ==================풀 사이즈====================게임초반 렉완화용

	[Header("풀 초기 사이즈")]
	[Tooltip("총알 풀 초기 생성 개수. 부족 시 자동 확장")]
	public int bulletPoolSize = 30;

	[Tooltip("미사일 풀 초기 생성 개수. 부족 시 자동 확장")]
	public int missilePoolSize = 10;

	[Tooltip("레이저 풀 초기 생성 개수. 부족 시 자동 확장")]
	public int laserPoolSize = 5;

	[Tooltip("분열 미사일 풀 초기 생성 개수. 부족 시 자동 확장")]
	public int clusterMissilePoolSize = 10;

	[Tooltip("핵?미사일 풀 초기 생성 개수. 부족 시 자동 확장")]
	public int dumbMissilePoolSize = 10;

	[Tooltip("적 건쉽 풀 초기 생성 개수. 부족 시 자동 확장")]
	public int enemy_GunshipPoolSize = 10;

	[Tooltip("적 드랍쉽 풀 초기 생성 개수. 부족 시 자동 확장")]
	public int enmey_DropshipPoolSize = 5;

	[Tooltip("적 미사일쉽 풀 초기 생성 개수. 부족 시 자동 확장")]
	public int enmey_MissileShipPoolSize = 5;

	//======================오브젝트 풀===================
	private List<Bullet> bulletPool = new List<Bullet>();
	private List<Missile> missilePool = new List<Missile>();
	private List<Laser> laserPool = new List<Laser>();
	private List<ClusterMissile> clusterMissilePool = new List<ClusterMissile>();
	private List<DumbMissile> dumbMissilePool = new List<DumbMissile>();
	//에너미 풀링용
	//private List<>

	//아이템 풀링용


	////////////////프리펩 연결용 레퍼런스들////////////////////////

	[Header("Projectiles Prefabs(사격 투사체 프리펩연결)")]
	public Bullet bulletPrefab;
	public Missile missilePrefab;
	public Laser laserPrefab;
	public ClusterMissile clusterMissilePrefab;
	public DumbMissile dumbMissilePrefab;

	[Header("Enemy Prefabs(적 프리펩 연결)")]
	public GameObject enemy_DropshipPrefab;
	public GameObject enemy_GunshipPrefab;
	public GameObject enemy_MissileshipPrefab;

	[Header("Item Prefabs(아이템 프리펩 연결)")]
	public GameObject Item_;

	//이펙트도 추가할것.

	private void Awake()
	{
		// 싱글톤 기본 세팅 (씬이 넘어가도 파괴되지 않게 유지)
		if (instance == null)
		{
			instance = this;
			DontDestroyOnLoad(gameObject);

			// 풀 미리 생성
			for (int i = 0; i < bulletPoolSize; i++)
			{
				Bullet bullet = Instantiate(bulletPrefab);
				bullet.gameObject.SetActive(false);
				bulletPool.Add(bullet);
			}
			for (int i = 0; i < missilePoolSize; i++)
			{
				Missile missile = Instantiate(missilePrefab);
				missile.gameObject.SetActive(false);
				missilePool.Add(missile);
			}
			for (int i = 0; i < laserPoolSize; i++)
			{
				Laser laser = Instantiate(laserPrefab);
				laser.gameObject.SetActive(false);
				laserPool.Add(laser);
			}
			for (int i = 0; i < clusterMissilePoolSize; i++)
			{
				ClusterMissile cm = Instantiate(clusterMissilePrefab);
				cm.gameObject.SetActive(false);
				clusterMissilePool.Add(cm);
			}
			for (int i = 0; i < dumbMissilePoolSize; i++)
			{
				DumbMissile dm = Instantiate(dumbMissilePrefab);
				dm.gameObject.SetActive(false);
				dumbMissilePool.Add(dm);
			}
		}
		else if (instance != this)
		{
			Debug.LogWarning("중복된 PoolManager 발견. 파괴 후 실행");
			Destroy(gameObject);
		}
	}


	//투사체 반환필요시?
	public Projectile GetProjectile(PROJECTILE_TYPE shootType)
	{
		switch (shootType)
		{
			case PROJECTILE_TYPE.BULLET:
				return GetBullet();
			case PROJECTILE_TYPE.MISSILE:
				return GetMissile();
			case PROJECTILE_TYPE.LASER:
				return GetLaser();
		}
		return null;
	}

	//게임오버나 씬전환 등 전체비활성화시
	public void DisableAllProjectiles()
	{
		foreach (Bullet bullet in bulletPool)
		{
			bullet.gameObject.SetActive(false);
		}
		foreach (Missile missile in missilePool)
		{
			missile.gameObject.SetActive(false);
		}
		foreach (Laser laser in laserPool)
		{
			laser.gameObject.SetActive(false);
		}
		foreach (ClusterMissile cm in clusterMissilePool)
		{
			cm.gameObject.SetActive(false);
		}
		foreach (DumbMissile dm in dumbMissilePool)
		{
			dm.gameObject.SetActive(false);
		}
	}

	///////////////유닛이나 기타등에서 호출할 Get함수들/////////////////
	public Bullet GetBullet()
	{
		foreach (Bullet bullet in bulletPool)
		{
			if (!bullet.gameObject.activeInHierarchy)
			{
				bullet.gameObject.SetActive(true);
				return bullet;
			}
		}
		Bullet newBullet = Instantiate(bulletPrefab);
		newBullet.projectileType = PROJECTILE_TYPE.BULLET;
		newBullet.dmgType = DAMAGE_TYPE.BULLET;
		bulletPool.Add(newBullet);
		Debug.LogWarning($"[PoolManager] Bullet 풀 확장. 현재 크기: {bulletPool.Count}");
		newBullet.gameObject.SetActive(true);
		return newBullet;
	}

	public Missile GetMissile()
	{
		foreach (Missile missile in missilePool)
		{
			if (!missile.gameObject.activeInHierarchy)
			{
				missile.gameObject.SetActive(true);
				return missile;
			}
		}
		Missile newMissile = Instantiate(missilePrefab);
		newMissile.projectileType = PROJECTILE_TYPE.MISSILE;
		newMissile.dmgType = DAMAGE_TYPE.EXPLOSION;
		missilePool.Add(newMissile);
		Debug.LogWarning($"[PoolManager] Missile 풀 확장. 현재 크기: {missilePool.Count}");
		newMissile.gameObject.SetActive(true);
		return newMissile;
	}

	public Laser GetLaser()
	{
		foreach (Laser laser in laserPool)
		{
			if (!laser.gameObject.activeInHierarchy)
			{
				laser.gameObject.SetActive(true);
				return laser;
			}
		}
		Laser newLaser = Instantiate(laserPrefab);
		newLaser.projectileType = PROJECTILE_TYPE.LASER;
		newLaser.dmgType = DAMAGE_TYPE.LASER;
		laserPool.Add(newLaser);
		Debug.LogWarning($"[PoolManager] Laser 풀 확장. 현재 크기: {laserPool.Count}");
		newLaser.gameObject.SetActive(true);
		return newLaser;
	}

	/// <summary>
	/// 분열 미사일 풀에서 꺼내기. 없으면 자동 확장.
	/// 꺼낸 후 반드시 Init() 호출할 것.
	/// </summary>
	public ClusterMissile GetClusterMissile()
	{
		foreach (ClusterMissile cm in clusterMissilePool)
		{
			if (!cm.gameObject.activeInHierarchy)
			{
				cm.gameObject.SetActive(true);
				return cm;
			}
		}
		ClusterMissile newCm = Instantiate(clusterMissilePrefab);
		newCm.projectileType = PROJECTILE_TYPE.MISSILE;
		newCm.dmgType = DAMAGE_TYPE.EXPLOSION;
		clusterMissilePool.Add(newCm);
		Debug.LogWarning($"[PoolManager] ClusterMissile 풀 확장. 현재 크기: {clusterMissilePool.Count}");
		newCm.gameObject.SetActive(true);
		return newCm;
	}

	/// <summary>
	/// 무유도 미사일 풀에서 꺼내기. 없으면 자동 확장.
	/// 꺼낸 후 반드시 Init() 호출할 것.
	/// </summary>
	public DumbMissile GetDumbMissile()
	{
		foreach (DumbMissile dm in dumbMissilePool)
		{
			if (!dm.gameObject.activeInHierarchy)
			{
				dm.gameObject.SetActive(true);
				return dm;
			}
		}
		DumbMissile newDm = Instantiate(dumbMissilePrefab);
		newDm.projectileType = PROJECTILE_TYPE.MISSILE;
		newDm.dmgType = DAMAGE_TYPE.EXPLOSION;
		dumbMissilePool.Add(newDm);
		Debug.LogWarning($"[PoolManager] DumbMissile 풀 확장. 현재 크기: {dumbMissilePool.Count}");
		newDm.gameObject.SetActive(true);
		return newDm;
	}

	public void ReturnProjectile(Projectile projectile)
	{
		projectile.gameObject.SetActive(false);
	}

	public void DisableBullet() { }
	public void DisableMissile() { }
	public void DisableLaser() { }
}
