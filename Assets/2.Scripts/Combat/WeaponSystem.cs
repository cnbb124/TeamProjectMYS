using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// Sequential: 트리거 한 번에 총구(발사구) 하나씩 교대 발사 (1→2→3→1→...).
// Random:     트리거 한 번에 랜덤 총구(발사구) 하나.
// Simultaneous: 트리거 한 번에 모든 총구(발사구) 동시 발사.
public enum BulletFireMode { Sequential, Random, Simultaneous }
public enum MissileFireMode { Sequential, Random, Simultaneous }


// ================================================================
// [WeaponSystem — 외부 참조 / 사용 가이드]
// ================================================================
// Unit 공통 컴포넌트. Player / Enemy 모두 사용.
// 총알/미사일 발사 로직 전담. 입력 감지는 Player/AI에서 Shoot()으로 위임.
// 발사 위치는 파츠 프리팹의 WeaponFirePos 컴포넌트로 동적 관리.
//
// ================================================================
// [HUD / UI팀 외부 참조용 (읽기 전용)]
// ================================================================
// missileSlots     : 전체 미사일 슬롯 목록 (List<MissileSlot>)
// CurMissileSlot   : 현재 선택된 슬롯 (MissileSlot). 없으면 null.
// curMissileType   : 현재 선택된 미사일 종류 (MISSILE_TYPE)
// SimultaneousFire : 동시 발사 수 (등록된 발사 위치 수)
// lockOnSystem     : 락온 시스템 참조 (IsLocked, LockedTarget 등)
//
//   예시)
//   MissileSlot slot = unit.weaponSystem.CurMissileSlot;
//   if (slot != null) hudManager.SetMissileAmmo(slot.curAmmo, slot.maxAmmo);
//
// ================================================================
// [이펙트팀 참조 — 머즐플래시 호출 위치]
// ================================================================
// 총알  : ShootBulletFrom()    내부 (_useBulletMuzzle=true 일 때만 재생)
//   → 풀에서 꺼낸 Bullet의 bulletData.muzzleEffectType 사용 (BulletData 미설정 시 VFX_BULLET_MUZZLE 폴백)
// 미사일: ShootMissileFrom() 내부 (_useMissileMuzzle=true 일 때만 재생)
//   → 풀에서 꺼낸 Missile의 missileData.muzzleEffectType 사용 (MissileData 미설정 시 VFX_MISSILE_MUZZLE 폴백)
//   유닛에 부착되어 유닛과 같이 움직임. 위치/회전은 호출 순간 총구 기준.
//   ※ 탄종(SO)에 머즐을 등록하면 WeaponSystem 코드 안 건드리고 머즐 종류 변경 가능.
//
// ================================================================
// [발사 위치 등록 경로]
// ================================================================
// 파츠 장착 → UnitParts.SpawnPartPrefab()
//   → GetComponentsInChildren<WeaponFirePos>()
//   → WeaponSystem.RegisterFirePos(posType, transform)
//
// ================================================================
// [외부 호출용 주요 메서드]
// ================================================================
// Init()                                     초기화. Player.Start()에서 호출.
// Shoot(PROJECTILE_TYPE type)                발사. Player/AI에서 호출.
// RegisterFirePos(WEAPON_POS_TYPE, Transform)    파츠 장착 시 UnitParts가 호출
// UnregisterFirePos(WEAPON_POS_TYPE, Transform)  파츠 해제 시 UnitParts가 호출
// SwitchMissileNext() / SwitchMissilePrev()  슬롯 전환
// SwitchToSlot(int slotIndex)                슬롯 직접 지정
// EquipMissile(int, MISSILE_TYPE, int, MissileData)  슬롯에 미사일 장착
// HasMissileAmmo(MISSILE_TYPE)               잔탄 여부 확인
// AddMissileAmmo(MISSILE_TYPE, int)          잔탄 추가
// EquipBullet(BulletData)                    총알 데이터 장착 (curBulletData 교체, 무한탄이라 슬롯/잔탄 없음)
// ================================================================
[RequireComponent(typeof(LockOnSystem))]
public class WeaponSystem : MonoBehaviour
{
	// ================== [레퍼런스] ==================
	private Unit _unit;
	private PoolManager _pool;
	private SoundManager _sound;
	private VFXManager _vfx;
	[HideInInspector]
	//[Header("락온 시스템")]
	public LockOnSystem lockOnSystem;


	// ================== [총알 설정] ==================
	[Space(5)]
	[Header("<size=22>[무기 시스템]</size>")]
	[Header("<size=18>총알 설정</size>")]
	[Tooltip("발사 쿨다운 (초). 이전 발사 후 이 시간이 지나야 다음 발사 허용.")]
	public float bulletFireCooldown = 0.1f;
	private float _lastBulletFireTime = 0f;

	[Tooltip("장착된 총알의 데이터(SO). curBulletPoolType으로 풀 종류 지정 (변형탄 대응).")]
	public BulletData curBulletData;

	[Header("<size=14>SFX/VFX 관련 설정</size>")]
	[Tooltip("총알 발사 시 머즐플래시 재생 여부. 플레이어 ON, 적은 유닛 유형에 따라 설정.")]
	[SerializeField]
	private bool _useBulletMuzzle = true;
	[Tooltip("총알 발사 시 사운드 재생 여부.")]
	[SerializeField]
	private bool _useBulletSound = true;
	[Tooltip("미사일 발사 시 머즐플래시 재생 여부. 플레이어 OFF (베이 발사), 터렛/사일로형 적 ON.")]
	[SerializeField]
	private bool _useMissileMuzzle = true;
	[Tooltip("미사일 발사 시 사운드 재생 여부. 플레이어 OFF, 터렛/사일로형 적 ON.")]
	[SerializeField]
	private bool _useMissileSound = true;

	[Tooltip("총알 발사 머즐플래시 재생 시간 설정")]
	[SerializeField]
	private float _bulletMuzzleFlashVFXPlayTime = 0.2f;
	[Tooltip("미사일 발사 머즐플래시 재생 시간 설정")]
	[SerializeField]
	private float _missileMuzzleFlashVFXPlayTime = 0.2f;

	[Header("<size=14>발사 모드 설정</size>")]
	[Tooltip("Sequential: 총구 하나씩 교대 발사 (1→2→3→1→...).\n" +
			 "Random: 매 발사마다 랜덤 총구 하나.\n" +
			 "Simultaneous: 모든 총구 동시 발사.")]
	public BulletFireMode bulletFireMode = BulletFireMode.Sequential;

	[Tooltip("Sequential: 발사구 하나씩 교대 발사 (1→2→3→1→...).\n" +
			 "Random: 매 발사마다 랜덤 발사구 하나.\n" +
			 "Simultaneous: 모든 발사구 동시 발사.")]
	public MissileFireMode missileFireMode = MissileFireMode.Sequential;

	// LAUNCHER_BULLET 파츠가 RegisterFirePos로 등록.
	private List<Transform> _bulletFirePositions = new List<Transform>();
	// Sequential 모드 전용 교대 발사 인덱스.
	private int _bulletFireIndex = 0;

	[Header("<size=14>총구 좌표 프리펩 직접 설정용(Enemy)</size>")]
	[Header("자동 설정 (WeaponFirePos 컴포넌트 있는 것들 좌표 받아옴)")]
	[Tooltip("ON: Awake 시 자식 오브젝트의 WeaponFirePos 컴포넌트를 자동 수집.\n" +
			 "플레이어(UnitParts 사용)는 OFF 유지.")]
	public bool autoDetectFirePositions = false;

	[Header("수동 설정 (UnitParts 없이 직접 지정할 총구 좌표)")]
	[Tooltip("UnitParts 없이 총알 발사 위치 직접 지정.")]
	[SerializeField]
	private List<Transform> _fixedBulletFirePositions = new List<Transform>();

	[Tooltip("UnitParts 없이 미사일 발사 위치 직접 지정.")]
	[SerializeField]
	private List<Transform> _fixedMissileFirePositions = new List<Transform>();


	// ================== [미사일 설정] ==================
	[Header("<size=18>미사일 설정</size>")]
	[Tooltip("발사 쿨다운 (초). 이전 발사 후 이 시간이 지나야 다음 발사 허용.")]
	public float missileFireCooldown = 2f;
	private float _lastMissileFireTime = 0f;

	[Space(5)]
	[Header("<size=14>미사일 슬롯 (보유 타입+잔탄)</size>")]
	[Tooltip("보유 미사일 타입 목록. 인벤토리/상점에서 AddMissileSlot()으로 추가.")]
	public List<MissileSlot> missileSlots = new List<MissileSlot>();

	// LAUNCHER_MISSILE 파츠가 RegisterFirePos로 등록.
	private List<Transform> _missileFirePositions = new List<Transform>();
	// Sequential 모드 전용 교대 발사 인덱스.
	private int _missileFireIndex = 0;

	// 발사 위치별 LauncherAnim 캐시 (컴포넌트 없는 파츠는 등록 안 됨)
	private Dictionary<Transform, LauncherAnim> _launcherAnims = new Dictionary<Transform, LauncherAnim>();

	// 현재 선택 슬롯 인덱스
	private int _curSlotIndex = 0;

	// lockOnSystem.maxMultiLockCount의 인스펙터 기본값. CLUSTER 슬롯 해제 시 복원용 (Awake에서 캡처)
	private int _defaultMaxMultiLockCount;

	// 외부 참조용 (HUD / AmmoUI). 슬롯 전환 시 자동 갱신, 확인용.
	//[HideInInspector]
	[Header("<size=14>현재 장착된 미사일 (외부참조 및 확인용)</size>")]
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
		lockOnSystem = GetComponent<LockOnSystem>();

		foreach (Transform pos in _fixedBulletFirePositions)
		{
			if (pos != null)
			{
				_bulletFirePositions.Add(pos);
			}
		}

		foreach (Transform pos in _fixedMissileFirePositions)
		{
			if (pos != null)
			{
				_missileFirePositions.Add(pos);
			}
		}

		if (autoDetectFirePositions)
		{
			WeaponFirePos[] found = GetComponentsInChildren<WeaponFirePos>();
			foreach (WeaponFirePos fp in found)
			{
				if (fp.posType == WEAPON_POS_TYPE.BULLET)
				{
					_bulletFirePositions.Add(fp.transform);
				}
				else if (fp.posType == WEAPON_POS_TYPE.MISSILE)
				{
					_missileFirePositions.Add(fp.transform);
				}
			}
		}

		if (lockOnSystem != null)
		{
			_defaultMaxMultiLockCount = lockOnSystem.maxMultiLockCount;
		}

		// 멀티에서 "쐈다" 전파용. PhotonView 없으면(싱글) null → 로컬 발사만.
		_photonView = GetComponent<PhotonView>();
	}

	// 멀티 발사 복제용. 투사체는 네트워크 오브젝트가 아니라 각 클라가 로컬 풀에서 생성하므로,
	// "쐈다"는 사실만 RPC로 전파하고 받는 쪽이 자기 풀에서 같은 종류를 발사한다(총알은 다수+연출이라 이 방식).
	private PhotonView _photonView;

	private void Start()
	{
		_pool = PoolManager.Instance;
		_sound = SoundManager.Instance;
		_vfx = VFXManager.Instance;
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
		switch (type)
		{
			case PROJECTILE_TYPE.BULLET:
				if (Time.time < _lastBulletFireTime + bulletFireCooldown)
				{
					return;
				}
				_lastBulletFireTime = Time.time;
				ShootAllBullets();
				break;

			case PROJECTILE_TYPE.MISSILE:
				if (Time.time < _lastMissileFireTime + missileFireCooldown)
				{
					return;
				}
				_lastMissileFireTime = Time.time;
				ShootAllMissiles();
				break;

		}

		// 로컬(소유자)이 실제로 발사했으면 남 클라에게 "쐈다"를 전파 → 각자 로컬 풀에서 같은 종류 발사.
		// 싱글(PhotonView 없음)이거나 룸 밖이면 전파 안 함. 남 소유 유닛은 여기 안 옴(입력/AI가 IsMine 게이트).
		if (_photonView != null && _photonView.IsMine && PhotonNetwork.InRoom)
		{
			_photonView.RPC(nameof(RpcShoot), RpcTarget.Others, (int)type);
		}
	}

	// 남 클라에서 수신 — 쿨다운/재전파 없이 로컬 풀에서만 발사(복제).
	[PunRPC]
	private void RpcShoot(int type)
	{
		switch ((PROJECTILE_TYPE)type)
		{
			case PROJECTILE_TYPE.BULLET:   ShootAllBullets();  break;
			case PROJECTILE_TYPE.MISSILE:  ShootAllMissiles(); break;
		}
	}

	/// <summary>
	/// 현재 락온 모드 기준으로 발사 가능한 락온 상태인지 확인.
	/// NONE(DUMB 미사일) = 항상 true. SINGLE = IsLocked. MULTI = MultiLockedTargets 1개 이상.
	/// Player는 락온 없이도 자유 발사 허용 — Enemy AI(EnemyShip)에서만 발사 전에 이 체크를 거침.
	/// </summary>
	public bool HasValidLockOn()
	{
		switch (lockOnSystem.currentLockMode)
		{
			case LOCK_ON_MODE.SINGLE:
				return lockOnSystem.IsLocked;
			case LOCK_ON_MODE.MULTI:
				return lockOnSystem.MultiLockedTargets.Count > 0;
			default:
				return true;
		}
	}


	// ================== [개별 발사 로직] ==================

	/// <summary>
	/// 총알 — bulletFireMode에 따라 발사.
	/// Sequential: 총구 하나씩 교대. Random: 랜덤 총구 하나. Simultaneous: 전체 동시.
	/// </summary>
	private void ShootAllBullets()
	{
		if (_bulletFirePositions.Count == 0)
		{
			return;
		}

		switch (bulletFireMode)
		{
			case BulletFireMode.Sequential:
				ShootBulletFrom(_bulletFirePositions[_bulletFireIndex]);
				_bulletFireIndex = (_bulletFireIndex + 1) % _bulletFirePositions.Count;
				break;

			case BulletFireMode.Random:
				ShootBulletFrom(_bulletFirePositions[Random.Range(0, _bulletFirePositions.Count)]);
				break;

			case BulletFireMode.Simultaneous:
				for (int i = 0; i < _bulletFirePositions.Count; i++)
				{
					ShootBulletFrom(_bulletFirePositions[i]);
				}
				break;
		}
	}

	/// <summary>
	/// 지정 위치에서 총알 1발 발사.
	/// 먼저 풀에서 Bullet을 꺼낸 뒤, 그 Bullet 자신의 bulletData(프리팹에 미리 연결된 SO)에서
	/// 머즐플래시/발사음을 가져와 재생 — WeaponSystem에 따로 등록 안 해도 프리팹 데이터만으로 일치되게 함.
	/// curBulletData는 풀 종류(어떤 프리팹을 꺼낼지) 결정용으로만 남음.
	/// </summary>
	private void ShootBulletFrom(Transform firePos)
	{
		if (_launcherAnims.TryGetValue(firePos, out LauncherAnim bulletAnim))
		{
			bulletAnim.PlayFire();
		}

		Bullet newBullet = _pool.GetProjectile(GetBulletPoolType()) as Bullet;

		// [데이터 주입] 장착한 curBulletData를 총알에 주입 → 프리팹 박힌 값 대신 이 데이터로 스탯/머즐/사운드 결정.
		// (미사일 주입과 동일. curBulletData가 없으면(미장착) 프리팹 값 유지.)
		if (newBullet != null && curBulletData != null)
		{
			newBullet.bulletData = curBulletData;
		}

		BulletData data = newBullet.bulletData;

		if (_useBulletMuzzle)
		{
			EFFECT_TYPE muzzleType = (data != null) ? data.muzzleEffectType : EFFECT_TYPE.VFX_BULLET_MUZZLE;
			_vfx.PlayEffectAtUnit(muzzleType, _unit.transform, firePos.position, firePos.rotation, _bulletMuzzleFlashVFXPlayTime);
		}
		if (_useBulletSound)
		{
			SOUND_TYPE soundType = (data != null) ? data.shootSoundType : SOUND_TYPE.SFX_NONE;
			_sound.PlaySFX3DAtUnit(soundType, _unit.transform, firePos);
		}

		newBullet.Init(firePos.position, firePos.forward, _unit);
	}

	/// <summary>
	/// 발사할 총알 풀 종류 결정. curBulletData.curBulletPoolType이 설정돼있으면 그 값,
	/// curBulletData가 null이면(에디터 미설정) 기존 기본 풀(POOL_TYPE.PROJECTILE_BULLET)로 폴백.
	/// </summary>
	private POOL_TYPE GetBulletPoolType()
	{
		if (curBulletData != null)
		{
			return curBulletData.curProjectilePoolType;
		}

		return POOL_TYPE.PROJECTILE_BULLET;
	}

	/// <summary>
	/// 총알 데이터 장착. EquipMissile()과 동일한 역할이지만, 총알은 무한탄이라 슬롯/잔탄 없이 curBulletData만 교체.
	/// 인벤토리 등 외부 시스템에서 탄종 변경 시 호출.
	/// </summary>
	public void EquipBullet(BulletData data)
	{
		curBulletData = data;
	}

	/// <summary>
	/// 미사일 — missileFireMode에 따라 발사.
	/// Sequential: 발사구 하나씩 교대. Random: 랜덤 발사구 하나. Simultaneous: 전체 동시.
	/// </summary>
	private void ShootAllMissiles()
	{
		MissileSlot curSlot = CurMissileSlot;
		if (curSlot == null || curSlot.curAmmo <= 0 || _missileFirePositions.Count == 0)
		{
			return;
		}

		switch (missileFireMode)
		{
			case MissileFireMode.Sequential:
				ShootMissileFrom(_missileFirePositions[_missileFireIndex]);
				curSlot.curAmmo--;
				_missileFireIndex = (_missileFireIndex + 1) % _missileFirePositions.Count;
				break;

			case MissileFireMode.Random:
				ShootMissileFrom(_missileFirePositions[Random.Range(0, _missileFirePositions.Count)]);
				curSlot.curAmmo--;
				break;

			case MissileFireMode.Simultaneous:
			{
				int fireCount = Mathf.Min(_missileFirePositions.Count, curSlot.curAmmo);
				for (int i = 0; i < fireCount; i++)
				{
					ShootMissileFrom(_missileFirePositions[i]);
					curSlot.curAmmo--;
				}
				break;
			}
		}
	}

	/// <summary>
	/// 지정 위치에서 미사일 1발 발사.
	/// 풀에서 꺼낼 프리팹은 missileData.curProjectilePoolType으로 결정(변형탄 대응).
	/// missileData가 비어있으면(에디터 미설정) curMissileType 기준 기본 풀로 폴백.
	/// 먼저 풀에서 꺼낸 뒤 그 missileData(프리팹에 미리 연결된 SO)에서 머즐/발사음을 가져와 재생
	/// — WeaponSystem에 따로 등록 안 해도 프리팹 데이터만으로 일치되게 함.
	/// </summary>
	private void ShootMissileFrom(Transform firePos)
	{
		if (_launcherAnims.TryGetValue(firePos, out LauncherAnim missileAnim))
		{
			missileAnim.PlayFire();
		}

		Projectile proj = _pool.GetProjectile(GetMissilePoolType(CurMissileSlot));

		// [데이터 주입] 발사 슬롯의 MissileData를 미사일에 주입 → 미사일이 프리팹 박힌 값 대신 이 데이터로
		// 스탯/이펙트/사운드를 결정(발사 주체별로 다른 데이터 적용 가능). 슬롯 데이터가 없으면(미설정) 프리팹 값 유지.
		// 자탄(ClusterMissile.Split이 소환)은 WeaponSystem을 안 거치므로 여기 영향 없음 — 각자 childrenMissileData 사용.
		if (proj is Missile injectMissile && CurMissileSlot != null && CurMissileSlot.missileData != null)
		{
			injectMissile.missileData = CurMissileSlot.missileData;
		}

		MissileData data = (proj as Missile)?.missileData;

		if (_useMissileMuzzle)
		{
			EFFECT_TYPE muzzleType = (data != null) ? data.muzzleEffectType : EFFECT_TYPE.VFX_MISSILE_MUZZLE;
			_vfx.PlayEffectAtUnit(muzzleType, _unit.transform, firePos.position, firePos.rotation, _missileMuzzleFlashVFXPlayTime);
		}
		if (_useMissileSound)
		{
			SOUND_TYPE soundType = (data != null) ? data.shootSoundType : SOUND_TYPE.SFX_NONE;
			//_sound.PlaySFX3DAtPosition(soundType, _unit.transform.position);
            _sound.PlaySFX3DAtUnit(soundType, _unit.transform, firePos);

        }

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
	/// 발사할 풀 종류 결정. missileData.curProjectilePoolType 설정돼있으면 그 값 사용,
	/// missileData가 null이면(에디터 작업 전 임시 상태) curMissileType 기준 기존 기본 풀로 폴백.
	/// </summary>
	private POOL_TYPE GetMissilePoolType(MissileSlot curSlot)
	{
		if (curSlot != null && curSlot.missileData != null)
		{
			return curSlot.missileData.curProjectilePoolType;
		}

		switch (curMissileType)
		{
			case MISSILE_TYPE.CLUSTER:
				return POOL_TYPE.PROJECTILE_MISSILE_CLUSTER;
			case MISSILE_TYPE.DUMB:
				return POOL_TYPE.PROJECTILE_MISSILE_DUMB;
			default:
				return POOL_TYPE.PROJECTILE_MISSILE;
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
				if (_missileFireIndex >= _missileFirePositions.Count)
				{
					_missileFireIndex = 0;
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
