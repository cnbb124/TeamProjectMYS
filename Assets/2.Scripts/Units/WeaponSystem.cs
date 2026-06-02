using System.Collections.Generic;
using UnityEngine;


// =====================================================================
// WeaponSystem : MonoBehaviour
// Unit 공통 컴포넌트. Player / Enemy 모두 사용.
// - 총알/레이저/미사일 발사 로직 전담.
// - 슬롯 관리, 잔탄 관리, 발사 모드 관리.
// - 입력 감지는 Player에서. Shoot() 호출로 위임.
// - Enemy 는 AI 에서 Shoot() 직접 호출.
// =====================================================================
public class WeaponSystem : MonoBehaviour
{
	// ================== [레퍼런스] ==================
	private Unit _unit;
	private PoolManager _pool;
	private SoundManager _sound;

	[Header("락온 시스템 (Player 전용)")]
	[Tooltip("Player 전용. Enemy 는 null 로 둘 것.")]
	public MissileLockOnSystem lockOnSystem;


	// ================== [총알 설정] ==================
	[Space(5)]
	[Header("<size=18>[무기 시스템]</size>")]
	[Header("총알 설정")]
	[Tooltip("총알 발사 간격 (초)")]
	public float fireDelay = 0.1f;
	private float _lastFireTime = 0f;
	// 총알 교대 발사 인덱스 (0=Left, 1=Right)
	private int _bulletFireIndex = 0;


	// ================== [미사일 설정] ==================
	[Space(5)]
	[Header("미사일 슬롯")]
	[Tooltip("인벤토리/장비창에서 장착한 미사일 타입 목록")]
	public MISSILE_TYPE[] equippedMissiles = new MISSILE_TYPE[3]
	{
		MISSILE_TYPE.HOMING,
		MISSILE_TYPE.CLUSTER,
		MISSILE_TYPE.DUMB
	};

	[Header("현재 미사일 타입")]
	public MISSILE_TYPE curMissileType = MISSILE_TYPE.HOMING;

	[Header("발사 모드 (DOUBLE=동시 / SINGLE=교대)")]
	public MISSILE_FIRE_MODE missileFireMode = MISSILE_FIRE_MODE.DOUBLE;

	// 장착 여부 - UpdateEquipStatus()에서 매 프레임 자동 갱신
	public bool isMissile_EquippedLeft = false;
	public bool isMissile_EquippedRight = false;

	[Space(5)]
	[Header("잔탄 목록")]
	[Tooltip("인스펙터에서 미사일 종류별 잔탄/최대치 설정")]
	public List<MissileAmmoInfo> missileAmmoList = new List<MissileAmmoInfo>();

	// 슬롯 순환 인덱스
	private int _missileSlotIndex = 0;
	// 교대 발사용 인덱스 (0=Left, 1=Right)
	private int _missileFireIndex = 0;


	// ================== [초기화] ==================

	private void Awake()
	{
		_unit = GetComponent<Unit>();
	}

	private void Start()
	{
		_pool = PoolManager.Instance;
		_sound = SoundManager.Instance;
	}

	/// <summary>
	/// 1번 슬롯으로 초기화. Unit.Start()에서 호출.
	/// </summary>
	public void Init()
	{
		if (equippedMissiles.Length > 0)
		{
			curMissileType = equippedMissiles[0];
		}
	}


	// ================== [발사 메인] ==================

	/// <summary>
	/// 발사 메인 진입점. Unit.Shoot() 에서 호출. 애니+사운드+투사체 생성 전담.
	/// </summary>
	public void Shoot(PROJECTILE_TYPE type)
	{
		SOUND_TYPE soundType = _unit.GetPlaySoundType(type);

		switch (type)
		{
			case PROJECTILE_TYPE.BULLET:
				// 발사 딜레이 체크
				if (Time.time < _lastFireTime + fireDelay)
				{
					return;
				}
				_lastFireTime = Time.time;
				_unit.PlayAnim(ANIM_TYPE.SHOOT_BULLET);
				_sound.PlaySFX3DAtPosition(soundType, _unit.transform.position, 0.7f, 1.2f);
				ShootBullet();
				break;

			case PROJECTILE_TYPE.LASER:
				_unit.PlayAnim(ANIM_TYPE.SHOOT_LASER);
				_sound.PlaySFX3DAtPosition(soundType, _unit.transform.position);
				ShootLaser();
				break;

			case PROJECTILE_TYPE.MISSILE:
				if (isMissile_EquippedLeft && isMissile_EquippedRight)
				{
					_unit.PlayAnim(ANIM_TYPE.SHOOT_MISSILE_BOTH);
				}
				else if (isMissile_EquippedLeft)
				{
					_unit.PlayAnim(ANIM_TYPE.SHOOT_MISSILE_L);
				}
				else if (isMissile_EquippedRight)
				{
					_unit.PlayAnim(ANIM_TYPE.SHOOT_MISSILE_R);
				}
				if (isMissile_EquippedLeft)
				{
					_sound.PlaySFX3DAtPosition(soundType, _unit.transform.position);
					ShootMissile(FIREPOS_TYPE.MISSILE_LEFT);
				}
				if (isMissile_EquippedRight)
				{
					_sound.PlaySFX3DAtPosition(soundType, _unit.transform.position);
					ShootMissile(FIREPOS_TYPE.MISSILE_RIGHT);
				}
				break;
		}
	}


	// ================== [개별 발사 로직] ==================

	/// <summary>
	/// 총알 - 좌우 교대 발사
	/// </summary>
	private void ShootBullet()
	{
		FIREPOS_TYPE[] bulletTypes = { FIREPOS_TYPE.BULLET_LEFT, FIREPOS_TYPE.BULLET_RIGHT };
		Transform curFirePos = _unit.GetFirePos(bulletTypes[_bulletFireIndex]);
		if (curFirePos == null)
		{
			return;
		}
		_bulletFireIndex = (_bulletFireIndex + 1) % bulletTypes.Length;

		Bullet newBullet = _pool.GetBullet();
		newBullet.Init(curFirePos.position, curFirePos.forward, _unit);
	}

	/// <summary>
	/// 레이저 - 중앙 고정 발사
	/// </summary>
	private void ShootLaser()
	{
		Transform curFirePos = _unit.GetFirePos(FIREPOS_TYPE.LASER);
		if (curFirePos == null)
		{
			return;
		}

		Laser newLaser = _pool.GetLaser();
		newLaser.Init(curFirePos.position, curFirePos.forward, _unit);
	}

	/// <summary>
	/// 미사일 - 지정 총구에서 발사. 잔탄 소모 및 교대 인덱스 처리 포함.
	/// </summary>
	private void ShootMissile(FIREPOS_TYPE firePosType)
	{
		Transform curFirePos = _unit.GetFirePos(firePosType);
		if (curFirePos == null)
		{
			return;
		}

		// 투사체 생성 전 잔탄 1 소모
		RemoveMissileAmmo(curMissileType);

		// 교대 모드 - 오른쪽 발사 불가 시 인덱스 리셋
		if (missileFireMode == MISSILE_FIRE_MODE.SINGLE && !isMissile_EquippedRight)
		{
			ResetMissileFireIndex();
		}
		// 교대 모드 - 다음 발사를 위한 인덱스 전환
		if (missileFireMode == MISSILE_FIRE_MODE.SINGLE)
		{
			AdvanceMissileFireIndex();
		}

		switch (curMissileType)
		{
			case MISSILE_TYPE.HOMING:
				Missile newMissile = _pool.GetMissile();
				if (lockOnSystem != null && lockOnSystem.IsLocked)
				{
					newMissile.Init(curFirePos.position, curFirePos.forward, _unit, lockOnSystem.LockedTarget);
				}
				else
				{
					newMissile.Init(curFirePos.position, curFirePos.forward, _unit);
				}
				break;

			case MISSILE_TYPE.CLUSTER:
				// ClusterMissile cm = _pool.GetClusterMissile();
				// cm.Init(curFirePos.position, curFirePos.forward, _unit);
				break;

			case MISSILE_TYPE.DUMB:
				// DumbMissile dm = _pool.GetDumbMissile();
				// dm.Init(curFirePos.position, curFirePos.forward, _unit);
				break;
		}
	}


	// ================== [슬롯 전환] ==================

	/// <summary>
	/// 다음 미사일 슬롯으로 전환.
	/// </summary>
	public void SwitchMissileNext()
	{
		if (equippedMissiles.Length <= 0)
		{
			return;
		}
		_missileSlotIndex = (_missileSlotIndex + 1) % equippedMissiles.Length;
		curMissileType = equippedMissiles[_missileSlotIndex];
		Debug.Log("[WeaponSystem] Missile slot -> " + _missileSlotIndex + " : " + curMissileType);
	}

	/// <summary>
	/// 이전 미사일 슬롯으로 전환.
	/// </summary>
	public void SwitchMissilePrev()
	{
		if (equippedMissiles.Length <= 0)
		{
			return;
		}
		_missileSlotIndex = (_missileSlotIndex - 1 + equippedMissiles.Length) % equippedMissiles.Length;
		curMissileType = equippedMissiles[_missileSlotIndex];
		Debug.Log("[WeaponSystem] Missile slot <- " + _missileSlotIndex + " : " + curMissileType);
	}

	/// <summary>
	/// 발사 모드 토글 (DOUBLE <-> SINGLE).
	/// </summary>
	public void ToggleFireMode()
	{
		if (missileFireMode == MISSILE_FIRE_MODE.DOUBLE)
		{
			missileFireMode = MISSILE_FIRE_MODE.SINGLE;
		}
		else
		{
			missileFireMode = MISSILE_FIRE_MODE.DOUBLE;
		}
		Debug.Log("[WeaponSystem] FireMode: " + missileFireMode);
	}


	// ================== [장착 상태 갱신] ==================

	/// <summary>
	/// 잔탄과 발사 모드에 따라 좌/우 총구 활성화 상태를 갱신.
	/// Shoot()의 if문을 제어하는 스위치 역할.
	/// Player.Update()에서 매 프레임 호출.
	/// </summary>
	public void UpdateEquipStatus()
	{
		MissileAmmoInfo info = missileAmmoList.Find(x => x.missileType == curMissileType);
		int ammo = info != null ? info.curAmmo : 0;

		if (ammo <= 0)
		{
			isMissile_EquippedLeft = false;
			isMissile_EquippedRight = false;
			return;
		}

		if (missileFireMode == MISSILE_FIRE_MODE.DOUBLE)
		{
			isMissile_EquippedLeft = ammo > 0;
			isMissile_EquippedRight = ammo > 1;
		}
		else if (missileFireMode == MISSILE_FIRE_MODE.SINGLE)
		{
			if (ammo == 1)
			{
				isMissile_EquippedLeft = true;
				isMissile_EquippedRight = false;
				_missileFireIndex = 0;
			}
			else
			{
				isMissile_EquippedLeft = (_missileFireIndex == 0);
				isMissile_EquippedRight = (_missileFireIndex == 1);
			}
		}
	}


	// ================== [교대 발사 인덱스] ==================

	public void AdvanceMissileFireIndex()
	{
		_missileFireIndex = (_missileFireIndex + 1) % 2;
	}

	public void ResetMissileFireIndex()
	{
		_missileFireIndex = 0;
	}


	// ================== [잔탄 관리] ==================

	public bool HasMissileAmmo(MISSILE_TYPE type)
	{
		MissileAmmoInfo info = missileAmmoList.Find(x => x.missileType == type);
		return info != null && info.curAmmo > 0;
	}

	public void RemoveMissileAmmo(MISSILE_TYPE type)
	{
		MissileAmmoInfo info = missileAmmoList.Find(x => x.missileType == type);
		if (info != null && info.curAmmo > 0)
		{
			info.curAmmo--;
		}
	}

	public void AddMissileAmmo(MISSILE_TYPE type, int amount)
	{
		MissileAmmoInfo info = missileAmmoList.Find(x => x.missileType == type);
		if (info != null)
		{
			info.curAmmo = Mathf.Min(info.curAmmo + amount, info.maxAmmo);
		}
	}

	public void IncreaseMaxMissileAmmo(MISSILE_TYPE type, int amount)
	{
		MissileAmmoInfo info = missileAmmoList.Find(x => x.missileType == type);
		if (info != null)
		{
			info.maxAmmo += amount;
		}
	}

	public void DecreaseMaxMissileAmmo(MISSILE_TYPE type, int amount)
	{
		MissileAmmoInfo info = missileAmmoList.Find(x => x.missileType == type);
		if (info != null)
		{
			info.maxAmmo -= amount;
		}
	}
}
