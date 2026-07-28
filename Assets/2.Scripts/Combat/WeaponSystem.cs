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

	/// <summary>
	/// 총구들의 가운데 위치. 조준 HUD가 '총알이 실제로 지나가는 선'을 그리는 데 씀.
	/// 좌우 총구가 따로 있으면 총알도 두 갈래로 나가므로, 마커 하나로는 가운데가 최선임.
	/// 총구가 하나도 없으면(무기 미장착) false — 부르는 쪽에서 폴백할 것.
	/// </summary>
	public bool TryGetMuzzleCenter(out Vector3 center)
	{
		center = Vector3.zero;
		if (_bulletFirePositions.Count == 0)
		{
			return false;
		}

		int count = 0;
		foreach (Transform pos in _bulletFirePositions)
		{
			if (pos == null)
			{
				continue;   // 파츠가 파괴되면 리스트에 null이 남을 수 있음
			}
			center += pos.position;
			count++;
		}

		if (count == 0)
		{
			return false;
		}

		center /= count;
		return true;
	}


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
		// 실제로 쏜 탄종. 발사가 성사됐을 때만 채워지고, 그대로 RPC에 실려 남 클라가 같은 걸 쏘게 함.
		ITEM_ID firedDataId = ITEM_ID.NONE;
		// 실제로 노린 락온 타겟. 이것도 같이 보내야 남 클라의 유도미사일이 같은 궤적을 그림.
		int[] firedTargetRefs = EmptyTargetRefs;

		switch (type)
		{
			case PROJECTILE_TYPE.BULLET:
				if (Time.time < _lastBulletFireTime + bulletFireCooldown)
				{
					return;
				}
				_lastBulletFireTime = Time.time;
				ShootAllBullets(curBulletData, true); // 로컬 발사 = 데미지 권위 있음
				// 데이터 미설정 유닛(프리팹 값으로 쏘는 구형 세팅)은 가리킬 ID가 없어 복제 전파를 못 함.
				firedDataId = curBulletData != null ? curBulletData.id : ITEM_ID.NONE;
				break;

			case PROJECTILE_TYPE.MISSILE:
				if (Time.time < _lastMissileFireTime + missileFireCooldown)
				{
					return;
				}
				MissileSlot curSlot = CurMissileSlot;
				if (curSlot == null || curSlot.curAmmo <= 0)
				{
					return;
				}
				_lastMissileFireTime = Time.time;
				List<Transform> lockedTargets = CollectLockOnTargets(curSlot.missileData);
				ShootAllMissiles(curSlot.missileData, true, lockedTargets); // 로컬 발사 = 데미지 권위 있음
				// 데이터 미설정 슬롯(프리팹 값으로 쏘는 구형 세팅)은 가리킬 ID가 없어 복제 전파를 못 함.
				firedDataId = curSlot.missileData != null ? curSlot.missileData.id : ITEM_ID.NONE;
				firedTargetRefs = EncodeTargetRefs(lockedTargets);
				break;

		}

		// 로컬(소유자)이 실제로 발사했으면 남 클라에게 "무엇을 쐈는지"까지 전파.
		// 탄종 ID를 같이 보내는 이유: 플레이어는 받는 쪽 슬롯(_curSlotIndex)이 쏜 사람과 다름
		// (슬롯 전환이 입력이라 IsMine 게이트에 막혀 원격 복제본은 0번에 고정됨).
		// ID로 보내면 받는 쪽이 ItemDatabase에서 같은 SO를 찾아 쏘므로 슬롯 상태와 무관하게 일치함.
		// ID가 없으면(NONE) 전파는 하되 받는 쪽이 자기 데이터로 쏨 — 적처럼 프리팹에 탄종이 고정된 유닛은
		// 모든 클라가 같은 프리팹을 쓰므로 그게 정답임. 
		// 싱글(PhotonView 없음)이거나 룸 밖이면 전파 안 함. 남 소유 유닛은 여기 안 옴(입력/AI가 IsMine 게이트).
		if (_photonView != null && _photonView.IsMine && PhotonNetwork.InRoom)
		{
			_photonView.RPC(nameof(RpcShoot), RpcTarget.Others, (int)type, (int)firedDataId, firedTargetRefs);
		}
	}

	

	// 남 클라에서 수신 — 쿨다운/탄약/재전파 없이 로컬 풀에서만 발사(복제).
	// dataId가 있으면 쏜 사람과 똑같은 탄종 데이터를 찾아 씀(자기 슬롯은 안 봄).
	// NONE이면 데이터를 못 가리키는 유닛이므로 자기 것으로 폴백 — 적은 프리팹 고정이라 이게 맞음.
	// targetRefs로 쏜 사람이 노린 타겟을 그대로 복원 — 이게 없으면 원격 유도미사일이 타겟을 몰라 직진함.
	[PunRPC]
	private void RpcShoot(int type, int dataId, int[] targetRefs)
	{
		// 쏜 사람이 나와 다른 씬에 있으면 무시. Photon은 씬을 모르고 방 전체에 뿌리기 때문에,
		// 이 체크가 없으면 스테이지에서 쏜 총알이 로비/대기실 화면에도 생긴다(머즐VFX·사운드까지).
		// 함선 자체는 PlayerSceneVisibility가 숨기지만 그 함선이 뱉는 총알은 별개라 여기서 막아야 함.
		if (PlayerSceneVisibility.IsDifferentFromLocalScene(
				PlayerSceneVisibility.GetPlayerScene(_photonView != null ? _photonView.Owner : null)))
		{
			return;
		}

		ITEM_ID firedDataId = (ITEM_ID)dataId;
		ItemDatabase database = ItemDatabase.Instance;

		switch ((PROJECTILE_TYPE)type)
		{
			case PROJECTILE_TYPE.BULLET:
			{
				BulletData data = (firedDataId != ITEM_ID.NONE && database != null)
					? database.Get<BulletData>(firedDataId)
					: curBulletData;
				ShootAllBullets(data, false); // 복제 연출 = 데미지 권위 없음
				break;
			}
			case PROJECTILE_TYPE.MISSILE:
			{
				MissileData data = (firedDataId != ITEM_ID.NONE && database != null)
					? database.Get<MissileData>(firedDataId)
					: CurMissileSlot?.missileData;
				ShootAllMissiles(data, false, DecodeTargetRefs(targetRefs));
				break;
			}
		}
	}

	// ================== [락온 타겟 전송] ==================
	// 락온 타겟은 유닛 루트가 아니라 그 밑의 LockOnBox transform임(LockOnSystem이 그렇게 등록함).
	// 유닛 하나에 박스가 여러 개라(보스 4개, 미사일쉽 3개) ViewID만으론 어느 박스인지 못 가림.
	// → [ViewID, 박스인덱스] 짝으로 보냄. 인덱스는 GetComponentsInChildren<LockOnBox>(true) 순서라
	//   같은 프리팹이면 클라마다 동일하고, 파괴/비활성으로도 안 밀림(true = 비활성 포함).
	private static readonly int[] EmptyTargetRefs = new int[0];

	// 지금 락온 상태에서 이 탄종이 쓸 타겟 목록. 아래 ShootMissileFrom의 분기와 짝을 맞춰야 함.
	private List<Transform> CollectLockOnTargets(MissileData data)
	{
		if (lockOnSystem == null)
		{
			return null;
		}

		if (data is ClusterMisslleData)
		{
			if (lockOnSystem.currentLockMode == LOCK_ON_MODE.SINGLE && lockOnSystem.IsLocked)
			{
				// 단일 락온 모드 - 자탄 전부 한 타겟에 집중 (Split()의 라운드로빈이 자동으로 처리)
				return new List<Transform> { lockOnSystem.LockedTarget };
			}
			if (lockOnSystem.MultiLockedTargets.Count > 0)
			{
				// 락온이 풀려도 자탄이 원래 타겟을 추적하도록 복사본 전달
				return new List<Transform>(lockOnSystem.MultiLockedTargets);
			}
			return null;
		}

		return lockOnSystem.IsLocked ? new List<Transform> { lockOnSystem.LockedTarget } : null;
	}

	private int[] EncodeTargetRefs(List<Transform> targets)
	{
		if (targets == null || targets.Count == 0)
		{
			return EmptyTargetRefs;
		}

		int[] refs = new int[targets.Count * 2];
		for (int i = 0; i < targets.Count; i++)
		{
			refs[i * 2] = 0;       // ViewID 0 = 가리킬 대상 없음
			refs[i * 2 + 1] = -1;  // 박스 못 찾음
			Transform target = targets[i];
			if (target == null)
			{
				continue;
			}
			PhotonView targetView = target.GetComponentInParent<PhotonView>();
			if (targetView == null)
			{
				continue; // PhotonView 없는 대상(씬 배치 비네트워크 적 등)은 못 가리킴 → 원격은 직진
			}
			LockOnBox[] boxes = targetView.GetComponentsInChildren<LockOnBox>(true);
			refs[i * 2] = targetView.ViewID;
			refs[i * 2 + 1] = System.Array.FindIndex(boxes, box => box.transform == target);
		}
		return refs;
	}

	private List<Transform> DecodeTargetRefs(int[] refs)
	{
		if (refs == null || refs.Length < 2)
		{
			return null;
		}

		List<Transform> targets = new List<Transform>(refs.Length / 2);
		for (int i = 0; i + 1 < refs.Length; i += 2)
		{
			int viewId = refs[i];
			int boxIndex = refs[i + 1];
			if (viewId == 0)
			{
				continue;
			}
			PhotonView targetView = PhotonView.Find(viewId);
			if (targetView == null)
			{
				continue; // 이미 파괴됐거나 아직 못 받은 대상
			}
			LockOnBox[] boxes = targetView.GetComponentsInChildren<LockOnBox>(true);
			// 박스를 못 찾으면 유닛 루트로 폴백(조준점이 약간 다르지만 직진보다는 나음)
			targets.Add(boxIndex >= 0 && boxIndex < boxes.Length ? boxes[boxIndex].transform : targetView.transform);
		}
		return targets.Count > 0 ? targets : null;
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
	// hasAuthority: 이 발사가 데미지 권위를 갖는지. 로컬 발사(Shoot)=true, RpcShoot 복제=false.
	// data: 실제로 쏠 탄종. 로컬은 자기 curBulletData, 복제는 RPC로 받은 ID로 조회한 것.
	//       장착 상태는 클라마다 다를 수 있어서 참조하지 않고 넘겨받은 데이터만 씀.
	//       null이면(데이터 미설정 유닛) 프리팹에 박힌 값으로 폴백 — 기존 동작 유지.
	private void ShootAllBullets(BulletData data, bool hasAuthority)
	{
		if (_bulletFirePositions.Count == 0)
		{
			return;
		}

		switch (bulletFireMode)
		{
			case BulletFireMode.Sequential:
				ShootBulletFrom(_bulletFirePositions[_bulletFireIndex], data, hasAuthority);
				_bulletFireIndex = (_bulletFireIndex + 1) % _bulletFirePositions.Count;
				break;

			case BulletFireMode.Random:
				ShootBulletFrom(_bulletFirePositions[Random.Range(0, _bulletFirePositions.Count)], data, hasAuthority);
				break;

			case BulletFireMode.Simultaneous:
				for (int i = 0; i < _bulletFirePositions.Count; i++)
				{
					ShootBulletFrom(_bulletFirePositions[i], data, hasAuthority);
				}
				break;
		}
	}

	/// <summary>
	/// 지정 위치에서 총알 1발 발사.
	/// 꺼낼 프리팹(풀 종류)도 스탯/머즐/사운드도 넘겨받은 data가 결정함 —
	/// 로컬이든 복제든 같은 data를 쓰므로 양쪽에서 똑같은 총알이 나감.
	/// data가 null이면(데이터 미설정 유닛) 기본 풀에서 꺼내 프리팹에 박힌 값을 그대로 씀.
	/// </summary>
	private void ShootBulletFrom(Transform firePos, BulletData data, bool hasAuthority)
	{
		if (_launcherAnims.TryGetValue(firePos, out LauncherAnim bulletAnim))
		{
			bulletAnim.PlayFire();
		}

		Bullet newBullet = SpawnBullet(firePos.position, firePos.forward, data, hasAuthority);
		if (newBullet == null)
		{
			return;
		}

		// 총구 연출(머즐VFX/사운드)은 총구 Transform 기준이라 여기 남김 — SpawnBullet은 임의 위치용 순수 스폰 코어.
		BulletData effectiveData = newBullet.bulletData;
		if (_useBulletMuzzle)
		{
			EFFECT_TYPE muzzleType = effectiveData != null ? effectiveData.muzzleEffectType : EFFECT_TYPE.VFX_BULLET_MUZZLE;
			_vfx.PlayEffectAtUnit(muzzleType, _unit.transform, firePos.position, firePos.rotation, _bulletMuzzleFlashVFXPlayTime);
		}
		if (_useBulletSound)
		{
			SOUND_TYPE soundType = effectiveData != null ? effectiveData.shootSoundType : SOUND_TYPE.SFX_NONE;
			_sound.PlaySFX3DAtUnit(soundType, _unit.transform, firePos);
		}
	}

	/// <summary>
	/// [공용 총알 스폰 코어] 임의 위치/방향으로 총알 1발을 풀에서 꺼내 발사.
	/// 총구 Transform이 아닌 곳(보스 탄막 등)에서도 재사용하려고 ShootBulletFrom에서 분리함.
	/// speed가 0 이하면 bulletData 기본 속도, 0 초과면 그 값으로 덮어씀(탄막 포인트별 속도).
	/// hasAuthority: 로컬 발사=true(데미지 권위), 원격 복제=false(연출만).
	/// 총구 머즐VFX/사운드/런처 애니는 호출부가 담당 — 여기선 순수 스폰만.
	/// </summary>
	public Bullet SpawnBullet(Vector3 origin, Vector3 dir, BulletData data, bool hasAuthority, float speed = 0f)
	{
		if (_pool == null)
		{
			return null;
		}

		POOL_TYPE poolType = data != null ? data.curProjectilePoolType : POOL_TYPE.PROJECTILE_BULLET;
		Bullet newBullet = _pool.GetProjectile(poolType) as Bullet;
		if (newBullet == null)
		{
			return null;
		}

		// [데이터 주입] 프리팹에 박힌 값 대신 이 데이터로 스탯/피격VFX/사운드 결정.
		if (data != null)
		{
			newBullet.bulletData = data;
		}

		newBullet.Init(origin, dir, _unit);
		// 복제탄(원격)은 데미지 권위 없음 → 연출만. 로컬 발사만 실제 데미지 판정.
		newBullet.SetDamageAuthority(hasAuthority);
		if (speed > 0f)
		{
			newBullet.OverrideSpeed(speed);
		}
		return newBullet;
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
	// data: 실제로 쏠 미사일. 로컬은 자기 슬롯 것, 복제는 RPC로 받은 ID로 조회한 것.
	// hasAuthority: 로컬 발사(Shoot)=true, RpcShoot 복제=false. (총알과 동일)
	// 탄약은 쏜 본인만 소비함 — 복제본이 남의 탄약을 자기 로컬에서 깎으면
	// 복제본 잔탄이 먼저 0이 돼서 그 뒤로 복제 발사가 통째로 멈춤.
	// targets: 이 발사가 노린 락온 타겟. 로컬은 CollectLockOnTargets로 만든 것, 복제는 RPC로 받아 복원한 것.
	//          원격 복제본은 AI/락온이 안 돌아서 자기 lockOnSystem을 보면 타겟이 없음 → 반드시 넘겨받아야 함.
	private void ShootAllMissiles(MissileData data, bool hasAuthority, List<Transform> targets)
	{
		if (_missileFirePositions.Count == 0)
		{
			return;
		}

		MissileSlot ammoSlot = hasAuthority ? CurMissileSlot : null;

		switch (missileFireMode)
		{
			case MissileFireMode.Sequential:
				ShootMissileFrom(_missileFirePositions[_missileFireIndex], data, hasAuthority, targets);
				ConsumeMissileAmmo(ammoSlot);
				_missileFireIndex = (_missileFireIndex + 1) % _missileFirePositions.Count;
				break;

			case MissileFireMode.Random:
				ShootMissileFrom(_missileFirePositions[Random.Range(0, _missileFirePositions.Count)], data, hasAuthority, targets);
				ConsumeMissileAmmo(ammoSlot);
				break;

			case MissileFireMode.Simultaneous:
			{
				// 복제본은 탄약 개념이 없으므로 발사구 전체에서 쏨. 로컬은 잔탄만큼만.
				int fireCount = ammoSlot != null
					? Mathf.Min(_missileFirePositions.Count, ammoSlot.curAmmo)
					: _missileFirePositions.Count;
				for (int i = 0; i < fireCount; i++)
				{
					ShootMissileFrom(_missileFirePositions[i], data, hasAuthority, targets);
					ConsumeMissileAmmo(ammoSlot);
				}
				break;
			}
		}
	}

	// ammoSlot이 null이면(복제 발사) 아무것도 안 함.
	private void ConsumeMissileAmmo(MissileSlot ammoSlot)
	{
		if (ammoSlot != null)
		{
			ammoSlot.curAmmo--;
		}
	}

	/// <summary>
	/// 지정 위치에서 미사일 1발 발사.
	/// 꺼낼 프리팹(풀 종류)도 스탯/이펙트/사운드도 전부 넘겨받은 data가 결정함 —
	/// 로컬이든 복제든 같은 data를 쓰므로 양쪽에서 똑같은 미사일이 나감.
	/// 자탄(ClusterMissile.Split이 소환)은 여기를 안 거침 — 각자 childrenMissileData 사용.
	/// </summary>
	private void ShootMissileFrom(Transform firePos, MissileData data, bool hasAuthority, List<Transform> targets)
	{
		if (_launcherAnims.TryGetValue(firePos, out LauncherAnim missileAnim))
		{
			missileAnim.PlayFire();
		}

		Projectile proj = _pool.GetProjectile(GetMissilePoolType(data));
		if (proj == null)
		{
			return;
		}

		// [데이터 주입] 프리팹에 박힌 값 대신 이 데이터로 스탯/이펙트/사운드 결정. data가 없으면 프리팹 값 유지.
		if (proj is Missile injectMissile && data != null)
		{
			injectMissile.missileData = data;
		}

		MissileData effectiveData = (proj as Missile)?.missileData;

		if (_useMissileMuzzle)
		{
			EFFECT_TYPE muzzleType = effectiveData != null ? effectiveData.muzzleEffectType : EFFECT_TYPE.VFX_MISSILE_MUZZLE;
			_vfx.PlayEffectAtUnit(muzzleType, _unit.transform, firePos.position, firePos.rotation, _missileMuzzleFlashVFXPlayTime);
		}
		if (_useMissileSound)
		{
			SOUND_TYPE soundType = effectiveData != null ? effectiveData.shootSoundType : SOUND_TYPE.SFX_NONE;
            _sound.PlaySFX3DAtUnit(soundType, _unit.transform, firePos);
        }

		// 미사일 종류는 curMissileType(내 슬롯 상태)이 아니라 '실제로 꺼낸 투사체'로 판별함.
		// 슬롯 인덱스는 클라마다 다를 수 있지만 data가 정한 프리팹은 같으므로 이쪽이 항상 맞음.
		// ClusterMissile/DumbMissile 둘 다 Missile을 상속하므로 좁은 타입부터 검사할 것.
		// 타겟은 lockOnSystem을 여기서 직접 보지 않고 넘겨받은 것만 씀 — 원격 복제본은 락온이 안 돌아
		// 자기 lockOnSystem이 비어있어서, 직접 보면 타겟 없이 직진해버림(호스트는 유도, 게스트는 직진으로 갈림).
		if (proj is ClusterMissile cm)
		{
			if (targets != null && targets.Count > 0)
			{
				cm.Init(firePos.position, firePos.forward, _unit, targets);
			}
			else
			{
				cm.Init(firePos.position, firePos.forward, _unit);
			}
		}
		else if (proj is DumbMissile dm)
		{
			dm.Init(firePos.position, firePos.forward, _unit); // 무유도라 타겟 안 씀
		}
		else if (proj is Missile homing)
		{
			if (targets != null && targets.Count > 0)
			{
				homing.Init(firePos.position, firePos.forward, _unit, targets[0]);
			}
			else
			{
				homing.Init(firePos.position, firePos.forward, _unit);
			}
		}

		// 복제 미사일(RpcShoot)은 데미지 권위 없음 → 연출만. (자탄은 ClusterMissile이 부모 권위를 물려줌)
		proj.SetDamageAuthority(hasAuthority);
	}

	/// <summary>
	/// 발사할 풀 종류 결정. data가 있으면 data.curProjectilePoolType,
	/// data가 null이면(데이터 미설정 유닛) curMissileType 기준 기본 풀로 폴백.
	/// 복제 발사(RpcShoot)는 항상 ID로 조회한 data가 있어서 폴백을 안 탐 — 그래서 여기서 내 슬롯을 봐도 안전함.
	/// </summary>
	private POOL_TYPE GetMissilePoolType(MissileData data)
	{
		if (data != null)
		{
			return data.curProjectilePoolType;
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
