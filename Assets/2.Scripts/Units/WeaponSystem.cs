using System.Collections.Generic;
using UnityEngine;


// ================================================================
// [외부 참조 가이드]
// ================================================================
// ▶ HUD팀 참조용 (읽기 전용으로 사용할 것)
//   missileSlots        : 전체 미사일 슬롯 목록 (List<MissileSlot>)
//   CurMissileSlot      : 현재 선택된 슬롯 (MissileSlot). 없으면 null.
//   curMissileType      : 현재 선택된 미사일 종류 (MISSILE_TYPE)
//   SimultaneousFire    : 동시 발사 수 (등록된 발사 위치 수)
//   lockOnSystem        : 락온 시스템 참조 (IsLocked, LockedTarget 등)
//
//   예시)
//   MissileSlot slot = unit.weaponSystem.CurMissileSlot;
//   if (slot != null) hudManager.SetMissileAmmo(slot.curAmmo, slot.maxAmmo);
//
// ▶ 이펙트팀 참조용
//   ShootBullet() 내부 : 머즐플래시 호출 위치
//   → VFXManager.Instance.PlayEffectAtUnit(EFFECT_TYPE.VFX_BULLET_MUZZLE, _unit.transform, curFirePos.position, curFirePos.rotation, 0.2f)
//   → 유닛(_unit.transform)에 부착되어 유닛과 같이 움직임. 위치/회전은 호출 순간 총구(curFirePos) 기준.
// ================================================================

// =====================================================================
// WeaponSystem : MonoBehaviour
// Unit 공통 컴포넌트. Player / Enemy 모두 사용.
// - 총알/레이저/미사일 발사 로직 전담.
// - 발사 위치는 파츠 프리팹의 WeaponFirePos 컴포넌트로 동적 관리.
//   UnitParts가 파츠 장착/해제 시 RegisterFirePos/UnregisterFirePos 호출.
// - 입력 감지는 Player에서. Shoot() 호출로 위임.
// - Enemy 는 AI 에서 Shoot() 직접 호출.
//
// [발사 위치 등록 경로]
// 파츠 프리팹 장착 → UnitParts.SpawnPartPrefab()
//   → GetComponentsInChildren<WeaponFirePos>()
//   → WeaponSystem.RegisterFirePos(posType, transform)
// =====================================================================
public class WeaponSystem : MonoBehaviour
{
	// ================== [레퍼런스] ==================
	private Unit _unit;
	private PoolManager _pool;
	private SoundManager _sound;

	[Header("락온 시스템")]
	public LockOnSystem lockOnSystem;


	// ================== [총알 설정] ==================
	[Space(5)]
	[Header("<size=18>[무기 시스템]</size>")]
	[Header("총알 설정")]
	[Tooltip("총알 발사 간격 (초)")]
	public float fireBulletDelay = 0.1f;
	private float _lastFireBulletTime = 0f;

	[Tooltip("장착된 총알의 데이터(SO). curBulletPoolType으로 풀 종류 지정 (변형탄 대응).")]
	public BulletData curBulletData;
	[Header("미사일 설정")]
	public float fireMissileDelay = 2f;
	private float _lastFireMissileTime = 0f;

	// LAUNCHER_BULLET 파츠가 RegisterFirePos로 등록. 순서대로 교대 발사.
	private List<Transform> _bulletFirePositions = new List<Transform>();
	private int _bulletFireIndex = 0;

	[Header("테스트용 파츠없이 사용할 총구좌표")]
	[Tooltip("UnitParts 없이 테스트용으로 발사 위치를 직접 지정. Awake 시 _bulletFirePositions에 합류됨.")]
	[SerializeField] private List<Transform> testBulletFirePositions = new List<Transform>();

	[Tooltip("UnitParts 없이 테스트용으로 미사일 발사 위치를 직접 지정. Awake 시 _missileFirePositions에 합류됨.")]
	[SerializeField] private List<Transform> testMissileFirePositions = new List<Transform>();


	// ================== [레이저 설정] ==================
	// LAUNCHER_LASER 파츠가 RegisterFirePos로 등록. 마지막 등록 위치 사용.
	private Transform _laserFirePos = null;


	// ================== [미사일 설정] ==================
	[Space(5)]
	[Header("미사일 슬롯 (보유 타입+잔탄)")]
	[Tooltip("보유 미사일 타입 목록. 인벤토리/상점에서 AddMissileSlot()으로 추가.")]
	public List<MissileSlot> missileSlots = new List<MissileSlot>();

	// LAUNCHER_MISSILE 파츠가 RegisterFirePos로 등록.
	private List<Transform> _missileFirePositions = new List<Transform>();

	// 발사 위치별 LauncherAnim 캐시 (컴포넌트 없는 파츠는 등록 안 됨)
	private Dictionary<Transform, LauncherAnim> _launcherAnims = new Dictionary<Transform, LauncherAnim>();

	// 현재 선택 슬롯 인덱스
	private int _curSlotIndex = 0;

	// lockOnSystem.maxMultiLockCount의 인스펙터 기본값. CLUSTER 슬롯 해제 시 복원용 (Awake에서 캡처)
	private int _defaultMaxMultiLockCount;

	// 외부 참조용 (HUD / AmmoUI). 슬롯 전환 시 자동 갱신, 확인용.
	//[HideInInspector]
	[Header("현재 장착된 미사일, 외부참조 및 확인용")]
	public MISSILE_TYPE curMissileType = MISSILE_TYPE.HOMING;

	/// <summary>
	/// 현재 선택된 미사일 슬롯. 슬롯이 없으면 null.
	/// </summary>
	public MissileSlot CurMissileSlot
	{
		get
		{
			if (missileSlots == null || missileSlots.Count == 0 || _curSlotIndex >= missileSlots.Count)
			{
				return null;
			}
			return missileSlots[_curSlotIndex];
		}
	}

	/// <summary>
	/// 현재 동시 발사 수 (미사일 파이어 포지션 수).
	/// </summary>
	public int SimultaneousFire { get { return _missileFirePositions.Count; } }


	// ================== [초기화] ==================

	private void Awake()
	{
		_unit = GetComponent<Unit>();

		// 테스트용 발사 위치를 정식 발사 위치 목록에 합류
		foreach (Transform pos in testBulletFirePositions)
		{
			if (pos != null)
			{
				_bulletFirePositions.Add(pos);
			}
		}

		foreach (Transform pos in testMissileFirePositions)
		{
			if (pos != null)
			{
				_missileFirePositions.Add(pos);
			}
		}

		if (lockOnSystem != null)
		{
			_defaultMaxMultiLockCount = lockOnSystem.maxMultiLockCount;
		}
	}

	private void Start()
	{
		_pool = PoolManager.Instance;
		_sound = SoundManager.Instance;
	}

	/// <summary>
	/// 첫 번째 슬롯으로 초기화. Player.Start()에서 호출.
	/// </summary>
	public void Init()
	{
		if (missileSlots == null || missileSlots.Count == 0)
		{
			return;
		}
		_curSlotIndex = 0;
		SetCurMissileType(missileSlots[0].type);
	}

	/// <summary>
	/// 슬롯 전환 시 호출. curMissileType 갱신 + CLUSTER 슬롯이면 락온 최대 동시 락온 개수를
	/// missileData(ClusterMisslleData).splitCount로 동기화, 아니면 인스펙터 기본값으로 복원.
	/// </summary>
	private void SetCurMissileType(MISSILE_TYPE type)
	{
		curMissileType = type;

		if (lockOnSystem == null)
		{
			return;
		}

		MissileSlot curSlot = CurMissileSlot;
		if (curSlot != null && curSlot.missileData is ClusterMisslleData clusterData)
		{
			lockOnSystem.maxMultiLockCount = clusterData.splitCount;
		}
		else
		{
			lockOnSystem.maxMultiLockCount = _defaultMaxMultiLockCount;
		}
	}


	// ================== [발사 메인] ==================

	/// <summary>
	/// 발사 메인 진입점. Unit.Shoot() 에서 호출.
	/// </summary>
	public void Shoot(PROJECTILE_TYPE type)
	{
		SOUND_TYPE soundType = _unit.GetPlaySoundType(type);

		switch (type)
		{
			case PROJECTILE_TYPE.BULLET:
				if (Time.time < _lastFireBulletTime + fireBulletDelay)
				{
					return;
				}
				_lastFireBulletTime = Time.time;
				
				ShootBullet(soundType);
				break;

			case PROJECTILE_TYPE.LASER:
				_sound.PlaySFX3DAtUnit(soundType, _unit.transform, _laserFirePos);
				ShootLaser();
				break;

			case PROJECTILE_TYPE.MISSILE:
				if (Time.time < _lastFireMissileTime + fireMissileDelay)
				{
					return;
				}
				_lastFireMissileTime = Time.time;
				ShootAllMissiles(soundType);
				break;

		}
	}


	// ================== [개별 발사 로직] ==================

	/// <summary>
	/// 총알 — 등록된 발사 위치를 순서대로 교대 발사.
	/// </summary>
	private void ShootBullet(SOUND_TYPE soundType)
	{
		if (_bulletFirePositions.Count == 0)
		{
			return;
		}

		Transform curFirePos = _bulletFirePositions[_bulletFireIndex];
		_bulletFireIndex = (_bulletFireIndex + 1) % _bulletFirePositions.Count;

		if (_launcherAnims.TryGetValue(curFirePos, out LauncherAnim bulletAnim))
		{
			bulletAnim.PlayFire();
		}
		VFXManager.Instance.PlayEffectAtUnit(EFFECT_TYPE.VFX_BULLET_MUZZLE, _unit.transform, curFirePos.position, curFirePos.rotation, 0.2f);
		_sound.PlaySFX3DAtUnit(soundType, _unit.transform, curFirePos);
		Bullet newBullet = _pool.GetProjectile(GetBulletPoolType()) as Bullet;
		newBullet.Init(curFirePos.position, curFirePos.forward, _unit);
	}

	/// <summary>
	/// 발사할 총알 풀 종류 결정. curBulletData.curBulletPoolType이 설정돼있으면 그 값,
	/// curBulletData가 null이면(에디터 미설정) 기존 기본 풀(POOL_TYPE.BULLET)로 폴백.
	/// </summary>
	private POOL_TYPE GetBulletPoolType()
	{
		if (curBulletData != null)
		{
			return curBulletData.curBulletPoolType;
		}

		return POOL_TYPE.BULLET;
	}

	/// <summary>
	/// 레이저 — 등록된 레이저 발사 위치에서 발사.
	/// </summary>
	private void ShootLaser()
	{
		if (_laserFirePos == null)
		{
			return;
		}

		Laser newLaser = _pool.GetLaser();
		newLaser.Init(_laserFirePos.position, _laserFirePos.forward, _unit);
	}

	/// <summary>
	/// 미사일 — 잔탄과 발사위치 수 중 작은 값만큼 동시 발사.
	/// </summary>
	private void ShootAllMissiles(SOUND_TYPE soundType)
	{
		MissileSlot curSlot = CurMissileSlot;
		if (curSlot == null || curSlot.curAmmo <= 0 || _missileFirePositions.Count == 0)
		{
			return;
		}

		int actualFire = Mathf.Min(_missileFirePositions.Count, curSlot.curAmmo);

		for (int i = 0; i < actualFire; i++)
		{
			_sound.PlaySFX3DAtPosition(soundType, _unit.transform.position);
			ShootMissileFrom(_missileFirePositions[i]);
			curSlot.curAmmo--;
		}
	}

    /// <summary>
    /// 지정 위치에서 미사일 1발 발사.
    /// 풀에서 꺼낼 프리팹은 missileData.curMissilePoolType으로 결정(변형탄 대응).
    /// missileData가 비어있으면(에디터 미설정) curMissileType 기준 기본 풀로 폴백.
    /// </summary>
    private void ShootMissileFrom(Transform firePos)
	{
		if (_launcherAnims.TryGetValue(firePos, out LauncherAnim missileAnim))
		{
			missileAnim.PlayFire();
		}

		Projectile proj = _pool.GetProjectile(GetMissilePoolType(CurMissileSlot));

		switch (curMissileType)
		{
			case MISSILE_TYPE.HOMING:
				Missile newMissile = proj as Missile;
				if (lockOnSystem != null && lockOnSystem.IsLocked)
				{
					newMissile.Init(firePos.position, firePos.forward, _unit, lockOnSystem.LockedTarget);
				}
				else
				{
					newMissile.Init(firePos.position, firePos.forward, _unit);
				}
				break;

			case MISSILE_TYPE.CLUSTER:
				ClusterMissile cm = proj as ClusterMissile;
				if (lockOnSystem != null && lockOnSystem.currentLockMode == LOCK_ON_MODE.SINGLE && lockOnSystem.IsLocked)
				{
					// 단일 락온 모드 - 자탄 전부 한 타겟에 집중 (Split()의 라운드로빈이 자동으로 처리)
					cm.Init(firePos.position, firePos.forward, _unit, new List<Transform> { lockOnSystem.LockedTarget });
				}
				else if (lockOnSystem != null && lockOnSystem.MultiLockedTargets.Count > 0)
				{
					// 락온이 풀려도 자탄이 원래 타겟을 추적하도록 복사본 전달
					cm.Init(firePos.position, firePos.forward, _unit, new List<Transform>(lockOnSystem.MultiLockedTargets));
				}
				else
				{
					cm.Init(firePos.position, firePos.forward, _unit);
				}
				break;

			case MISSILE_TYPE.DUMB:
				DumbMissile newDm = proj as DumbMissile;
				newDm.Init(firePos.position, firePos.forward, _unit);

				break;
		}
	}

    /// <summary>
    /// 발사할 풀 종류 결정. missileData.curMissilePoolType 설정돼있으면 그 값 사용,
    /// missileData가 null이면(에디터 작업 전 임시 상태) curMissileType 기준 기존 기본 풀로 폴백.
    /// </summary>
    private POOL_TYPE GetMissilePoolType(MissileSlot curSlot)
	{
		if (curSlot != null && curSlot.missileData != null)
		{
			return curSlot.missileData.curMissilePoolType;
		}

		switch (curMissileType)
		{
			case MISSILE_TYPE.CLUSTER:
				return POOL_TYPE.CLUSTER_MISSILE_BASE;
			case MISSILE_TYPE.DUMB:
				return POOL_TYPE.DUMB_MISSILE;
			default:
				return POOL_TYPE.MISSILE;
		}
	}


	// ================== [발사 위치 등록/해제 — UnitParts에서 호출] ==================

	/// <summary>
	/// 파츠 장착 시 WeaponFirePos 컴포넌트 타입에 따라 발사 위치 등록.
	/// </summary>
	public void RegisterFirePos(WEAPON_POS_TYPE posType, Transform pos)
	{
		switch (posType)
		{
			case WEAPON_POS_TYPE.BULLET:
				_bulletFirePositions.Add(pos);
				LauncherAnim bulletAnim = pos.GetComponentInParent<LauncherAnim>();
				if (bulletAnim != null)
				{
					_launcherAnims[pos] = bulletAnim;
				}
				break;
			case WEAPON_POS_TYPE.MISSILE:
				_missileFirePositions.Add(pos);
				LauncherAnim missileAnim = pos.GetComponentInParent<LauncherAnim>();
				if (missileAnim != null)
				{
					_launcherAnims[pos] = missileAnim;
				}
				break;
			case WEAPON_POS_TYPE.LASER:
				_laserFirePos = pos;
				break;

		}
	}

	/// <summary>
	/// 파츠 해제 시 발사 위치 제거.
	/// </summary>
	public void UnregisterFirePos(WEAPON_POS_TYPE posType, Transform pos)
	{
		switch (posType)
		{
			case WEAPON_POS_TYPE.BULLET:
				_bulletFirePositions.Remove(pos);
				_launcherAnims.Remove(pos);
				if (_bulletFireIndex >= _bulletFirePositions.Count)
				{
					_bulletFireIndex = 0;
				}
				break;
			case WEAPON_POS_TYPE.MISSILE:
				_missileFirePositions.Remove(pos);
				_launcherAnims.Remove(pos);
				break;
			case WEAPON_POS_TYPE.LASER:
				if (_laserFirePos == pos)
				{
					_laserFirePos = null;
				}
				break;

		}
	}


	// ================== [슬롯 전환] ==================

	public void SwitchMissileNext()
	{
		if (missileSlots.Count <= 1)
		{
			return;
		}
		_curSlotIndex = (_curSlotIndex + 1) % missileSlots.Count;
		SetCurMissileType(missileSlots[_curSlotIndex].type);
		Debug.Log("[WeaponSystem] Missile slot -> " + _curSlotIndex + " : " + curMissileType);
	}

	public void SwitchMissilePrev()
	{
		if (missileSlots.Count <= 1)
		{
			return;
		}
		_curSlotIndex = (_curSlotIndex - 1 + missileSlots.Count) % missileSlots.Count;
		SetCurMissileType(missileSlots[_curSlotIndex].type);
		Debug.Log("[WeaponSystem] Missile slot <- " + _curSlotIndex + " : " + curMissileType);
	}

	public void SwitchToSlot(int slotIndex)
	{
		if (missileSlots == null || slotIndex < 0 || slotIndex >= missileSlots.Count)
		{
			return;
		}
		_curSlotIndex = slotIndex;
		SetCurMissileType(missileSlots[_curSlotIndex].type);
		Debug.Log("[WeaponSystem] Missile slot direct -> " + _curSlotIndex + " : " + curMissileType);
	}


	// ================== [미사일 슬롯 관리] ==================

	public void AddMissileSlot()
	{
		missileSlots.Add(new MissileSlot());
	}

	public void RemoveMissileSlot()
	{
		if (missileSlots.Count == 0)
		{
			return;
		}
		missileSlots.RemoveAt(missileSlots.Count - 1);
		if (_curSlotIndex >= missileSlots.Count)
		{
			_curSlotIndex = Mathf.Max(0, missileSlots.Count - 1);
		}
		if (missileSlots.Count > 0)
		{
			SetCurMissileType(missileSlots[_curSlotIndex].type);
		}
	}

	public void EquipMissile(int slotIndex, MISSILE_TYPE type, int maxAmmo, MissileData data)
	{
		if (missileSlots == null || slotIndex < 0 || slotIndex >= missileSlots.Count)
		{
			return;
		}
		missileSlots[slotIndex].type = type;
		missileSlots[slotIndex].maxAmmo = maxAmmo;
		missileSlots[slotIndex].curAmmo = maxAmmo;
		missileSlots[slotIndex].missileData = data;

		if (slotIndex == _curSlotIndex)
		{
			SetCurMissileType(type);
		}
	}


	// ================== [잔탄 관리] ==================

	public bool HasMissileAmmo(MISSILE_TYPE type)
	{
		for (int i = 0; i < missileSlots.Count; i++)
		{
			if (missileSlots[i].type == type && missileSlots[i].HasAmmo)
			{
				return true;
			}
		}
		return false;
	}

	public void AddMissileAmmo(MISSILE_TYPE type, int amount)
	{
		for (int i = 0; i < missileSlots.Count; i++)
		{
			if (missileSlots[i].type == type)
			{
				missileSlots[i].curAmmo = Mathf.Min(missileSlots[i].curAmmo + amount, missileSlots[i].maxAmmo);
				return;
			}
		}
	}

	public void IncreaseMaxMissileAmmo(MISSILE_TYPE type, int amount)
	{
		for (int i = 0; i < missileSlots.Count; i++)
		{
			if (missileSlots[i].type == type)
			{
				missileSlots[i].maxAmmo += amount;
				return;
			}
		}
	}

	public void DecreaseMaxMissileAmmo(MISSILE_TYPE type, int amount)
	{
		for (int i = 0; i < missileSlots.Count; i++)
		{
			if (missileSlots[i].type == type)
			{
				missileSlots[i].maxAmmo = Mathf.Max(0, missileSlots[i].maxAmmo - amount);
				return;
			}
		}
	}
}
