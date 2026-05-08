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

	[Tooltip("적 건쉽 풀 초기 생성 개수. 부족 시 자동 확장")]
	public int enemy_GunshipPoolSize = 10;

	[Tooltip("적 드랍쉽 풀 초기 생성 개수. 부족 시 자동 확장")]
	public int enmey_DropshipPoolSize = 5;

	[Tooltip("적 미사일쉽 풀 초기 생성 개수. 부족 시 자동 확장")]
	public int enmey_MissileShipPoolSize = 5;
	
	//======================오브젝트 풀===================
	//차후 필요한만큼 추가
	private List<Bullet> bulletPool = new List<Bullet>();
	private List<Missile> missilePool = new List<Missile>();
	private List<Laser> laserPool = new List<Laser>();
	//에너미 풀링용
	//private List<>

	//아이템 풀링용


	//
	////////////////프리펩 연결용 레퍼런스들////////////////////////

	[Header("Projectiles Prefabs(사격 투사체 프리펩연결)")]
	//차후 종류추가시 더 추가할것.
	public Bullet bulletPrefab;
	public Missile missilePrefab;
	public Laser laserPrefab;

	[Header("Enemy Prefabs(적 프리펩 연결)")]
	public GameObject enemy_DropshipPrefab;
	public GameObject enemy_GunshipPrefab;
	public GameObject enemy_MissileshipPrefab;

	[Header("Item Prefabs(아이템 프리펩 연결)")]
	public GameObject Item_;

	

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
			
		}
		else if (instance != this)
		{

			Debug.LogWarning("중복된 SoundManager 발견. 파괴 후 실행");
			Destroy(gameObject);
		}
		
	}

	//투사체 반환필요시?
	public Projectile GetProjectile(PROJECTILE_TYPE shootType)
	{
		switch(shootType)
		{
			case PROJECTILE_TYPE.BULLET:
				GetBullet();
				break;

			case PROJECTILE_TYPE.MISSILE:
				GetMissile();
				break;
				
			case PROJECTILE_TYPE.LASER:
				GetLaser();
				break;
		}
		return null;
	}

	//게임오버나 씬전환 등 전체비활성화시
	public void DisableAllProjectiles()
	{
		foreach (Bullet b in bulletPool)
		{
			b.gameObject.SetActive(false);
		}
		foreach (Missile m in missilePool)
		{
			m.gameObject.SetActive(false);
		}
		foreach (Laser l in laserPool)
		{
			l.gameObject.SetActive(false);
		}
		
	}

	///////////////유닛이나 기타등에서 호출할 Get함수들/////////////////
	public Bullet GetBullet()
	{
		//먼저 불릿풀체크
		foreach(Bullet bullet in bulletPool)
		{
			//하이어라키에 비활성화가있으면
			if(!bullet.gameObject.activeInHierarchy)
			{
				//재활용
				bullet.gameObject.SetActive(true);
				
				return bullet;
			}
		}
		//풀다돌았는데 없을경우
		//새로생성
		//종류추가시 enum새로 만들고 배열로바꾸고 해당번호로 추가.
		Bullet newBullet = Instantiate(bulletPrefab);
		//투사체종류,데미지타입설정
		newBullet.projectileType = PROJECTILE_TYPE.BULLET;
		newBullet.dmgType = DAMAGE_TYPE.BULLET;
		//풀에추가
		bulletPool.Add(newBullet);
		Debug.LogWarning($"[PoolManager] Bullet 풀 확장. 현재 크기: {bulletPool.Count}");
		//활성화&Init
		newBullet.gameObject.SetActive(true);
		
		//반환
		return newBullet;
	}

	public Missile GetMissile()
	{
		foreach (Missile missile in missilePool)
		{
			if(!missile.gameObject.activeInHierarchy)
			{
				missile.gameObject.SetActive(true);
				
				return missile;
			}
			
		}
		//다돌았는데없을경우
		Missile newMissile = Instantiate(missilePrefab);
		//투사체종류,데미지타입설정
		newMissile.projectileType = PROJECTILE_TYPE.MISSILE;
		newMissile.dmgType = DAMAGE_TYPE.EXPLOSION;
		//풀에추가
		missilePool.Add(newMissile);
		Debug.LogWarning($"[PoolManager] Missile 풀 확장. 현재 크기: {missilePool.Count}");
		//활성화&Init
		newMissile.gameObject.SetActive(true);
		
		//반환
		return newMissile;

	}

	public Laser GetLaser()
	{
		foreach(Laser laser in laserPool)
		{
			if(!laser.gameObject.activeInHierarchy)
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

	



	public void ReturnProjectile(Projectile projectile)
	{
		projectile.gameObject.SetActive(false); // 매니저가 처리
	}
	public void DisableBullet()
	{

	}
	public void DisableMissile()
	{

	}
	public void DisableLaser()
	{

	}




}
