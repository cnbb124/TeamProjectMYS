using System.Collections;
using System.Collections.Generic;

using Photon.Pun;
using UnityEngine;


// ================================================================
// [외부 참조 가이드]
// ================================================================
// ▶ HUD팀 참조용 (읽기 전용으로 사용할 것)
//   curHpRemaining      : 현재 HP
//   maxHpRemaining      : 최대 HP
//   curShieldRemaining  : 현재 실드
//   maxShieldCapacity   : 최대 실드
//   curArmorRemaining   : 현재 방어구
//   maxArmor            : 최대 방어구
//   curBoostRemaining   : 현재 부스트 잔량
//   maxBoostCapacity    : 최대 부스트
//   curSpeed            : 현재 속도 (0.5초마다 갱신)
//   curState            : 현재 FSM 상태 (IDLE / MOVING / DODGE / DIE)
//   IsInvincible        : 무적 여부
//
// ▶ 스킬팀 참조용
//   Teleport(Vector3 position) : 즉시 위치 이동(Rigidbody.position까지 동기화). WarpSkill에서 사용.
//
//   예시)
//   float hpRatio     = (float)unit.curHpRemaining / unit.maxHpRemaining;
//   float shieldRatio = (float)unit.curShieldRemaining / unit.maxShieldCapacity;
//   float boostRatio  = unit.curBoostRemaining / unit.maxBoostCapacity;
//
// ▶ 이펙트/사운드팀 참조용
//   OnHitReaction(HitInfo info) : 피격 시 Player/Enemy에서 override → 여기서 VFXManager 호출
//   OnStateEnter(UNIT_STATE state) : IDLE/MOVING/BOOSTING 애니+루프사운드 재생, BRAKE는 MOVING 모션 재사용, DODGE 무적/타이머, DIE 애니
//   OnStateExit(UNIT_STATE state)  : IDLE/MOVING/BOOSTING 루프사운드 정지
//                                    Player는 DODGE(RCS버스트)/BRAKE(역추진 파티클)만 추가 override
// ================================================================

// FirePosEntry / BoostPosEntry 제거 — WeaponFirePos 마커 컴포넌트 + 파츠 프리팹으로 동적 관리

[System.Serializable]
public class MissileSlot // 미사일 슬롯 — 타입+잔탄 통합 관리. equippedMissiles+MissileAmmoInfo 통합.
{
	public MISSILE_TYPE type;
	public int curAmmo;
	public int maxAmmo;
	[Tooltip("장착된 미사일의 데이터(SO). CLUSTER면 ClusterMisslleData로 캐스팅해 splitCount 등 사용.")]
	public MissileData missileData;

	// maxAmmo > 0 이면 장착된 슬롯 (타입과 최대치가 설정됨)
	public bool IsEquipped { get { return maxAmmo > 0; } }
	public bool HasAmmo { get { return curAmmo > 0; } }
}

public abstract class Unit : MonoBehaviour, IDamageable, IPunObservable
{


	//==================레퍼런스==================//


	//=============기타 레퍼런스===============
	//애니메이션 컨트롤러 할당용
	protected AnimCtrl _animCtrl;
	//리지드바디 할당용 레퍼런스
	protected Rigidbody _rb;
	//외부(Enemy AI 예측사격 등)에서 실제 이동 속도벡터 참조용
	public Vector3 Velocity => _rb != null ? _rb.velocity : Vector3.zero;

	[Tooltip("레이저 등 관통 무기를 이 유닛이 막는지. 보스/전함 같은 거대몹만 체크. 일반 적은 꺼두면 관통됨.")]
	[SerializeField] private bool _blocksBeam = false;
	// IHittable 구현 — 관통 무기 차단 여부. 거대몹만 true.
	public bool BlocksBeam => _blocksBeam;

	//매니저 할당용 레퍼런스
	protected SoundManager _sound;
	protected PoolManager _pool;

	// 멀티플레이 소유권. PhotonView 없으면(싱글 씬배치/오프라인) 항상 내 것 → 기존 단일 동작 그대로.
	// PhotonNetwork.Instantiate로 스폰된 유닛만 PhotonView를 가지며, 소유자만 IsMine=true.
	// Player/Enemy가 각자 갖고 있던 것을 base로 통일 — 데미지 권위 라우팅(TakeDamage)이 여기서 필요하기 때문.
	protected PhotonView _photonView;
	public bool IsMine => _photonView == null || _photonView.IsMine;
	[HideInInspector]
	public WeaponSystem weaponSystem;
	[HideInInspector]
	public SkillSystem skillSystem;
	protected UnitParts _unitParts;

	//==================유닛데이터==================//

	[Header("<size=18>파츠 추가 스탯 제외한 유닛의 기본 스탯 설정</size>")]

	[Header("<size=14>1. HP")]
	[Tooltip("체력 최대치")]
	public int maxHpRemaining = 150; //최대,현재HP수치

	[Header("<size=14>2. 실드</size>")]
	[Tooltip("실드(보호막) 최대치")]
	public int maxShieldCapacity;//최대,현재실드수치
	[Tooltip("피격후 회복 딜레이")]
	public float shieldRegainDelay;//피격후 회복까지딜레이시간
	[Tooltip("실드 초당 회복수치(최소 6)")]
	[Range(6.0f, 100.0f)]
	public float shieldRegainRate; //실드회복수치
	[HideInInspector]							   //private float shieldRegainTimer = 0f;//딜레이 시간까지잴 타이머 >0516 코루틴으로변경
	public bool isShieldRegaining = false; //회복중인지 여부
	private Coroutine _shieldRegenCoroutine;//중간 정지등을 위한 코루틴변수 따로
											//실드연결용
											// [실드팀 참조] 실드 비주얼 오브젝트(ProceduralForceFieldOverlay 등 부착된 자식) 연결용.
											// curShieldRemaining > 0 ↔ SetActive(true), <= 0 ↔ SetActive(false) 로 표시 여부 제어 권장.

	// 피격 이펙트(Trigger) 호출은 OnHitReaction()에서 처리.
	[Tooltip("유닛에 있는 실드 오브젝트 직접 연결")]
	public GameObject shield;

	[Tooltip("실드가 있을때 OFF, 없을때 ON 되는 본체 HitBox 연결. 실드 콜라이더와 상호토글됨.")]
	public Transform bodyHitboxRoot;

	// bodyHitboxRoot 하위 콜라이더 캐싱용 
	private Collider[] _bodyHitboxColliders;

	// 실드 오브젝트(자식 포함) 콜라이더 캐싱용
	private Collider[] _shieldColliders;
	private ProceduralForceField.ProceduralForceFieldOverlay _shieldOverlay;


	[Header("<size=14>3. 장갑(아머)</size>")]
	[Tooltip("최대 아머 수치")]
	public int maxArmor;//최대,현재아머수치

	[Tooltip("아머 보유시 데미지 경감되는 수치.")]
	public int defense;//아머 있을시 데미지 경감수치(damageAmount=damage-defense)



	[Header("<size=14>4. 크리티컬</size>")]
	public float criChance;
	public float criDamageMultiplier;


	// =====================================================================
	// 사망 시퀀스
	// =====================================================================
	[Header("<size=14>5. 사망 처리 시간</size>")]
	[Tooltip("사망 애니 재생 후 정리(풀 반납 등)까지 대기 시간(초). 이 시간 동안 죽는 모션이 재생됨.")]
	[SerializeField] protected float _deathSequenceDuration = 1.5f;

	//[HideInInspector]
	//public Transform curFirePos;//밑에서 총구스위칭용 
	//필요없음.

	[Header("===============<size=14>현재 상태(참고용 입력x )</size>================")]
	[Tooltip("UNIT_STATE — \"지금 어떤 상태인가\" (표현/물리 레이어)")]
	public UNIT_STATE curState = UNIT_STATE.IDLE;
	// 직전 상태. 전환별로 다른 애니메이션 블렌드(CrossFade duration)를 적용할 때 참조
	protected UNIT_STATE _previousState = UNIT_STATE.IDLE;
	public int curHpRemaining;
	public int CurHp => curHpRemaining;//인터페이스 프로퍼티용
									   //public int CurShiled => curShieldRemaining;//인터페이스 프로퍼티용
	public int curShieldRemaining;
	public int curArmorRemaining;
	public float curSpeed;
	public float curBoostRemaining;//부스트잔량
								   //잔탄도추가예정

	private float _updateTimer = 0f;

	//// ==================레이어==================
	//[HideInInspector]
	//public int playerLayer;
	//[HideInInspector]
	//public int enemyLayer;
	//[HideInInspector]
	//public int groundLayer;//행성등 지형지물, 차후 수정필요
	//[HideInInspector]
	//public int ItemLayer;//아이템레이어 추가필요
	//[HideInInspector]
	//public int playerProjectileLayer;
	//[HideInInspector]
	//public int enemyProjectileLayer;


	[HideInInspector]
	public SOUND_TYPE _playSoundType;

	// 엔진 사운드(공회전/가속/부스트) 볼륨·피치 튜닝값은 SoundManager.engineSoundConfig로 이전됨.
	// Unit은 speedRatio/isBoosting/mute만 계산해서 SoundManager.UpdateEngineLoopVolumes()에 넘김 (RTPC 스타일 분리).



	// =====================================================================
	// 일시정지 / 게임오버 체크
	// Player, Enemy 등 자식 클래스의 Update/FixedUpdate 첫 줄에서 사용.
	// Unit.Update() 에도 적용 - 자식이 base.Update() 호출 시 이중 안전망.
	// =====================================================================
	protected bool ShouldPause =>
		GameManager.Instance != null &&
		(GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver);


	// 크리여부 판정은 투사체가 담당 크확은 유닛이. → HitInfo.isCritical로 전달받음
	// criChance는 투사체 생성 시 attacker에서 복사해서 사용
	//데미지 계산식
	//shield>armor>hp순 실드없고 armor있을때는 경감수치만큼 데미지 경감
	//damageAmount=
	//(실드o,아머x)(HitInfo.damage) * (크리시)criDamageMultiplier;
	//(실드x,아머o)(HitInfo.damage-defense *(크리시)criDamageMultiplier;
	//(실드x,아머x)(HitInfo.damage) * (크리시)criDamageMultiplier;
	//curHp-=damageAmount;
	[Space(10)]
	[Header("<size=14>===========터렛등 좌표고정유닛은 적용안됨============</size>")]
	[Space(5)]
	[Header("<size=18>이동 관련 설정</size>")]
	[Tooltip("기본 이동속도 (초당 이동 거리, unit/s). 예: 350이면 초당 350유닛 이동.")]
	public float baseMoveSpeed;//기본이동속ㄷ
	[Tooltip("부스트 사용시 이동속도 (초당 이동 거리, unit/s)")]
	public float boostSpeed;//부스트사용시 이동속도
	[Tooltip("최대속도velocity가 넘어갈시 고정시킬속도")]
	public float maxSpeed;
	[Tooltip("목표 속도(baseMoveSpeed/boostSpeed)까지 도달하는 데 걸리는 시간(초).\n" +
			 "작을수록 빠릿하게 반응함. base든 boost든 목표속도가 달라도 항상 이 시간만큼 걸림")]
	public float timeToMaxSpeed = 0.4f;
	[Tooltip("입력을 떼고 완전히 멈추는 데 걸리는 시간(초)")]
	public float timeToStop = 0.4f;
	[Tooltip("입력없이 감속 중일 때 BRAKE 상태로 진입하는 속도 기준(최대속도 대비 비율, 0~1).")]
	[Range(0f, 1f)]
	public float brakeEnterSpeedRatio = 0.65f;
	[Tooltip("BRAKE 상태에서 빠져나가는 속도 기준(최대속도 대비 비율, 0~1).\n" +
			 "진입 기준보다 낮게 둬서 65%/20% 사이를 오갈 때 BRAKE-MOVING이 매 프레임 깜빡이는 걸 방지(히스테리시스).")]
	[Range(0f, 1f)]
	public float brakeExitSpeedRatio = 0.2f;

	[Tooltip("부스트 최대치")]
	public float maxBoostCapacity;
	[Tooltip("부스트 사용 최소 요구치")]
	public float minBoostRequired;//최소 부스트사용요구치
	[Tooltip("부스트 사용시 게이지 소모량")]
	public float boostConsumeAmont;
	[Tooltip("부스트 회복 딜레이 ")]
	public float boostRegainDelay;//부스트 회복딜레이
	[Tooltip("부스트 자연회복량")]
	public float boostRegainRate;//초당 부스트 잔량회복수치


	private float _boostRegainTimer = 0f;//부스트 회복딜레이까지 잴 타이머
	private bool _isBoostRegaining = false;//회복유무
	protected bool _isBoosting = false;//부스트 사용 중 여부 (자식에서 설정)



	[Header("<size=18>회피 관련 설정</size>")]
	[Tooltip("회피 지속시간")]
	public float dodgeDuration = 0.5f;
	[Tooltip("무적 지속시간")]
	public float dodgeInvincibleTime = 0.4f;
	private float _dodgeTimer = 0f;
	[Tooltip("회피 쿨타임")]
	public float dodgeCoolTime = 5f;
	protected float _dodgeCooldownTimer = 0f;
	[Tooltip("회피 시 실제로 이동하는 거리(unit). dodgeDuration 동안 이 거리만큼 이동하도록\n" +
			 "내부에서 속도(거리÷dodgeDuration)를 역산해 적용함 — 질량(mass)과 무관하게 항상 같은 거리를 이동.")]
	public float dodgeDistance = 40f;
	public bool IsInvincible { get; private set; }
	[Tooltip("피격부위 혹은 HP잔량에 따른이동속도 변경용")]
	public float speedMultiPlier;//HP 혹은 피격부위에따른 속도조절용.



	// 총구/부스터 위치는 각 파츠 프리팹의 WeaponFirePos 컴포넌트로 관리. Unit에서 직접 보유 안 함.




	protected virtual void Awake()
	{
		// SoundManager.Instance/PoolManager.Instance 최초 호출은 Start()에서만 — Awake/OnEnable은
		// 다른 오브젝트와 실행순서가 보장 안 돼서 매니저 자신의 초기화보다 먼저 instance를 선점할 수 있음.
		_rb = GetComponent<Rigidbody>();
		_animCtrl = GetComponent<AnimCtrl>();
		weaponSystem = GetComponent<WeaponSystem>();
		skillSystem = GetComponent<SkillSystem>();
		_unitParts = GetComponent<UnitParts>();
		_photonView = GetComponent<PhotonView>();

		if (shield != null)
		{
			_shieldColliders = shield.GetComponentsInChildren<Collider>();
			_shieldOverlay = shield.GetComponentInChildren<ProceduralForceField.ProceduralForceFieldOverlay>(true);
		}

		if (bodyHitboxRoot != null)
		{
			_bodyHitboxColliders = bodyHitboxRoot.GetComponentsInChildren<Collider>();
		}
	}

	// 풀에서 재사용(SetActive(true))될 때마다 호출 — Start()는 오브젝트 생애 단 한 번만 실행되므로,
	// 죽었을 때의 상태(curHpRemaining=0, CurState=DIE 등)가 재사용 시 그대로 남는 문제를 막기 위함.
	// 최초 활성화 시에도 Start()보다 먼저 호출되는데, 그 시점엔 RefillToMax()가 인스펙터 기본값 기준으로 한 번 돌고
	// 곧이어 Start()가 파츠 보너스 적용 후 다시 RefillToMax()를 불러 최종값으로 덮어쓰므로 문제없음.
	protected virtual void OnEnable()
	{
		RefillToMax();
		_shieldOverlay?.TriggerReset();
		CurState = UNIT_STATE.IDLE;

		// 풀 재사용 시, 이전 생애에 일시정지로 걸어둔 물리 프리즈가 남아있지 않도록 초기화.
		_physFrozen = false;
		if (_rb != null) _rb.isKinematic = false;

		// 이전 생애에 실드회복 코루틴이 돌다가 SetActive(false)로 강제종료됐을 수 있음 —
		// 그 경우 코루틴 자체는 유니티가 자동으로 멈추지만 이 두 필드는 안 지워지고 남아있었음.
		isShieldRegaining = false;
		_shieldRegenCoroutine = null;

		// _sound는 Start()에서 캐싱된 값을 그대로 씀(여기서 새로 Instance를 안 부름). 최초 1회차는 Start
		// 전이라 null이라 조용히 스킵되고 Start가 등록함. 풀 재사용(2회차+)부터는 이미 캐싱돼있어 바로 작동.
		RegisterEngineSound();
	}

	// Start is called before the first frame update
	protected virtual void Start()
	{
		// .Instance 최초 호출은 반드시 여기(Start)에서만 — Unity가 보장하는 건 "모든 Awake가 끝난 뒤 Start가 돈다"뿐.
		_sound = SoundManager.Instance;
		_pool = PoolManager.Instance;

		//인스펙터에서 입력된 값 현재 스탯으로 설정
		//저장 기능 생길시 변경필요.

		// 엔진사운드 최초 등록(1회차). 풀 재사용(2회차+)은 OnEnable()의 RegisterEngineSound()가 처리.
		RegisterEngineSound();

		//UnitParts.Start()의 파츠 스탯보너스 적용(max값 변경)과 실행순서가 보장되지 않으므로,
		//파츠 적용 후 UnitParts에서 RefillToMax()를 한번 더 호출해 cur을 최종 max로 동기화함.
		//유닛파츠에서 해주긴하는데 유닛파츠없을시 임시적용용.
		RefillToMax();

		//===========레거시=========
		//playerLayer = LayerMask.NameToLayer("UNIT_Player");
		//enemyLayer = LayerMask.NameToLayer("UNIT_Enemy");
		//groundLayer = LayerMask.NameToLayer("Environment");
		////아이템 레이어 추가필요ItemLayer = LayerMask.NameToLayer("");
		//playerProjectileLayer = LayerMask.NameToLayer("PlayerProjectile");
		//enemyProjectileLayer = LayerMask.NameToLayer("EnemyProjectile");

		CurState = UNIT_STATE.IDLE;
	}

	// OnEnable과 대칭. 부모 오브젝트가 SetActive(false)되면 자식들도 같이 비활성화되며
	// 자식 각각의 OnDisable도 호출됨 — 자식이 독립된 Unit(터렛 등)일 때 자기 자신의 정리를 직접 하게 하는 용도.
	// 엔진 루프(SFX_IDLE/MOVING/BOOST)도 여기서 같이 정지 — StopSFX3DLoop를 아무도 안 호출해서
	// 죽거나 풀로 반납된 유닛의 루프 등록이 activeLoopSounds에 영원히 남아있던 버그 수정.
	protected virtual void OnDisable()
	{
		if (HasEngineSound)
		{
			_sound?.StopSFX3DLoop(SOUND_TYPE.SFX_IDLE, transform);
			_sound?.StopSFX3DLoop(SOUND_TYPE.SFX_MOVING, transform);
			_sound?.StopSFX3DLoop(SOUND_TYPE.SFX_BOOST, transform);
		}
	}

	// 엔진 루프 사운드(SFX_IDLE/MOVING/BOOST) 등록 여부. 고정 포탑처럼 이동이 없는 유닛은
	// EnemyTurretBase에서 false로 override — SoundManager의 maxConcurrent 슬롯 낭비 방지.
	protected virtual bool HasEngineSound
	{
		get
		{
			return true;
		}
	}



	// cur을 max로 채움. UnitParts가 파츠 스탯보너스로 max를 바꾼 직후에도 호출해서 동기화.
	// 새 STAT_TYPE이 max와 별도의 cur 스냅샷을 갖는 스탯이라면 여기에도 추가할 것.
	public virtual void RefillToMax()
	{
		curHpRemaining = maxHpRemaining;
		curShieldRemaining = maxShieldCapacity;
		curArmorRemaining = maxArmor;
		curBoostRemaining = maxBoostCapacity;

		UpdateShieldHitboxState();
	}
	// 엔진 루프 사운드(SFX_IDLE/MOVING/BOOST) 등록. Start()(최초 1회차)와 OnEnable()(풀 재사용 2회차+) 양쪽에서 호출.
	private void RegisterEngineSound()
	{
		if (HasEngineSound)
		{
			_sound?.PlaySFX3DLoop(SOUND_TYPE.SFX_IDLE, transform, 0f);
			_sound?.PlaySFX3DLoop(SOUND_TYPE.SFX_MOVING, transform, 0f);
			_sound?.PlaySFX3DLoop(SOUND_TYPE.SFX_BOOST, transform, 0f);
			// 무음 상태로 시작한 직후, 올바른 초기 볼륨으로 즉시 보정.
			UpdateEngineAudio();
		}
	}
	// 워프 등 즉시 위치 이동 스킬용. Rigidbody가 있으면 그쪽 position도 같이 맞춰야 물리 동기화가 깨지지 않음.
	public void Teleport(Vector3 position)
	{
		if (_rb != null)
		{
			_rb.position = position;
		}
		transform.position = position;
	}

	// 실드 유무에 따라 실드 콜라이더 / 본체 HitBox 콜라이더를 상호토글.
	// 실드 있음 -> 실드 콜라이더만 ON, 본체 HitBox는 OFF
	// 실드 없음 -> 실드 콜라이더 OFF, 본체 HitBox만 ON
	private void UpdateShieldHitboxState()
	{
		bool shieldUp = curShieldRemaining > 0;
		bool bodyHitboxEnabled = !shieldUp && !IsInvincible;

		if (_shieldColliders != null)
		{
			foreach (Collider shieldCollider in _shieldColliders)
			{
				shieldCollider.enabled = shieldUp && !IsInvincible;
			}
		}

		if (_bodyHitboxColliders == null)
		{
			return;
		}

		foreach (Collider hitbox in _bodyHitboxColliders)
		{
			hitbox.enabled = bodyHitboxEnabled;
		}
	}

	// Update is called once per frame
	protected virtual void Update()
	{
		if (ShouldPause) return;
		UpdateFSM();
		//UpdateShieldRegen(); >>0516 코루틴으로변경
		UpdateBoostRegen();

		if (_dodgeCooldownTimer > 0f)
		{
			_dodgeCooldownTimer -= Time.deltaTime;
		}

		_updateTimer += Time.deltaTime;
		if (_updateTimer > 0.5f)
		{
			curSpeed = _rb != null ? (_rb.velocity.magnitude < 0.01f ? 0f : _rb.velocity.magnitude) : 0f;
			_updateTimer = 0f;
		}

		UpdateEngineAudio();
	}

	// 일시정지/게임오버 시 Rigidbody 물리 정지용. 스크립트가 return해도 물리엔진은 기존 속도를
	// 계속 적분해 관성으로 미끄러지므로, 속도를 저장 후 isKinematic으로 완전히 멈추고 재개 시 복원한다.
	private bool _physFrozen;
	private Vector3 _savedVelocity;
	private Vector3 _savedAngularVelocity;

	protected virtual void FixedUpdate()
	{
		// 자식 클래스는 FixedUpdate 최상단에서 base.FixedUpdate()를 호출해 이 프리즈 처리를 태울 것.
		if (_rb == null) return;

		if (ShouldPause)
		{
			if (!_physFrozen)
			{
				_savedVelocity = _rb.velocity;
				_savedAngularVelocity = _rb.angularVelocity;
				_rb.isKinematic = true; // 물리 적분 자체를 멈춤 (관성/충돌 정지)
				_physFrozen = true;
			}
			return;
		}

		if (_physFrozen)
		{
			_rb.isKinematic = false;
			_rb.velocity = _savedVelocity;               // 멈추기 직전 속도 그대로 복원
			_rb.angularVelocity = _savedAngularVelocity;
			_physFrozen = false;
		}
	}

	// 매 프레임 실제 속도(_rb.velocity, 0.5초 캐시인 curSpeed 말고 즉시값 사용) 기준으로 speedRatio만 계산해서
	// SoundManager에 넘김 — 볼륨/피치 곡선 자체(SoundManager.EngineSoundConfig)는 SoundManager가 전담(RTPC 스타일).
	private void UpdateEngineAudio()
	{
		// 엔진음 없는 유닛(고정 터렛 등)은 루프를 등록하지 않으므로 갱신 자체를 건너뜀 —
		// 안 그러면 매 프레임 "루프없음" 경고 + 불필요한 조회로 로그 스팸/렉 유발.
		if (!HasEngineSound)
		{
			return;
		}
		bool mute = CurState == UNIT_STATE.DIE || _rb == null;
		float intensity = mute ? 0f : GetEngineIntensity();
		_sound?.UpdateEngineLoopVolumes(transform, intensity, _isBoosting, mute);
	}

	// 엔진 루프음 강도(0~1). 기본은 '현재 속도 비율' — 적 등은 이걸 그대로 사용.
	// Player는 '쓰로틀'(가속 입력)로 override해서 실제 속도가 아니라 입력에 반응하게 함.
	protected virtual float GetEngineIntensity()
	{
		return (_rb != null && maxSpeed > 0f) ? Mathf.Clamp01(_rb.velocity.magnitude / maxSpeed) : 0f;
	}
	// GetFirePos / GetBoostPos 제거 — WeaponSystem이 직접 _bulletFirePositions 등을 보유


	








	// =====================================================================
	// [FSM 구조 안내]
	//
	// ▶ UNIT_STATE (이 파일) — "지금 어떤 상태인가" (표현/물리 레이어)
	//     - 애니메이션, 이펙트, 무적, Rigidbody 처리가 이 값에 의존
	//     - OnStateEnter/OnStateExit : 전환 시 1회 실행
	//     - UpdateFSM()              : 매 프레임 OnIdle/OnMoving 등 실행
	//     - Player : 입력이 CurState를 직접 설정
	//     - Enemy  : AI_STATE(행동 의도)가 UpdateAI() 끝에 단방향 동기화
	//
	// ▶ AI_STATE (Enemy.cs) — "무엇을 하려는가" (행동 의도 레이어, Enemy 전용)
	//     - STANDBY/PATROL/CHASE/ATTACK 중 하나
	//     - UpdateAI()에서 조건 체크 → 다음 AI_STATE 전환
	//     - 전환 후 UNIT_STATE에 동기화: STANDBY→IDLE / 나머지→MOVING
	//     - 단, DODGE/DIE 중에는 동기화 스킵 — Unit FSM이 우선권 가짐
	//
	//   AI_STATE(의도) ──단방향──→ UNIT_STATE(표현) ──→ 애니/이펙트/물리
	//   Player는 AI_STATE 없이 입력으로 UNIT_STATE 직접 제어
	// =====================================================================

	

	// 마지막으로 치명타(사망)를 입힌 공격자. 멀티에서 킬 보상을 '죽인 사람'에게 귀속시키기 위해 기록.
	// (싱글에선 유일한 플레이어라 결과는 같지만, 이 구조로 잡아두면 MP 전환 시 킬러 구분이 자동으로 맞음)
	protected GameObject _lastAttacker;

	public UNIT_STATE CurState
	{
		get
		{
			return curState;
		}
		set
		{
			if (curState == value)//셋할때 똑같으면 필요없음로
			{
				return;
			}
			//현재상태에서 나가는 메섣
			OnStateExit(curState);
			//넣은값 적용해주고
			_previousState = curState;
			curState = value;
			//들어가는 메서드
			OnStateEnter(curState);
		}
	}

	private void UpdateFSM()
	{
		switch (CurState)
		{
			case UNIT_STATE.IDLE:
				OnIdle();
				break;
			case UNIT_STATE.MOVING:
				OnMoving();
				break;
			case UNIT_STATE.BOOSTING:
				OnBoosting();
				break;
			case UNIT_STATE.BRAKE:
				OnBraking();
				break;
			case UNIT_STATE.DODGE:
				OnDodge();
				break;
			case UNIT_STATE.DIE:
				OnDying();
				break;
		}
	}

	//===============자식에서 직접 override==================
	/// <summary>
	/// 진입시 한번만 할것들(애니재생. 이펙트,사운드는 자식에서 따로(서로다르고 매니저에서관리중이니))
	/// </summary>
	/// <param name="state"></param>
	protected virtual void OnStateEnter(UNIT_STATE state)
	{
		switch (state)
		{
			case UNIT_STATE.IDLE:
				// 부스트 직후 정지는 블렌드를 길게 해 관성이 빠지는 느낌
				if (_previousState == UNIT_STATE.BOOSTING)
				{
					PlayAnim(ANIM_TYPE.IDLE, 0.3f);
				}
				else
				{
					PlayAnim(ANIM_TYPE.IDLE);
				}
				// 엔진 사운드(공회전/가속/부스트)는 더 이상 상태 전환 시점에 트리거 안 함 —
				// Start()에서 3개 레이어를 한 번씩 걸어두고, UpdateEngineAudio()가 매 프레임 속도 기준으로 볼륨만 크로스페이드함.
				break;

			case UNIT_STATE.MOVING:
				// 부스트 → 일반 이동 전환도 동일하게 블렌드를 길게
				if (_previousState == UNIT_STATE.BOOSTING)
				{
					PlayAnim(ANIM_TYPE.MOVING, 0.3f);
				}
				else
				{
					PlayAnim(ANIM_TYPE.MOVING);
				}
				break;

			case UNIT_STATE.BOOSTING:
				PlayAnim(ANIM_TYPE.BOOST);
				break;

			case UNIT_STATE.BRAKE:
				// 별도 애니메이션 없음 — 비주얼상 여전히 비행 중이므로 MOVING 모션 그대로 재생
				PlayAnim(ANIM_TYPE.MOVING);
				break;

			case UNIT_STATE.DODGE:
				//PlayAnim(ANIM_TYPE.DODGE_N);키입력따라 좌우 혹은 랜덤방향(키입력없을때)
				_dodgeTimer = dodgeDuration;
				IsInvincible = true;
				_dodgeCooldownTimer = dodgeCoolTime;
				UpdateShieldHitboxState();
				break;

			case UNIT_STATE.DIE:
				PlayAnim(ANIM_TYPE.DIE);
				// DIE 상태 진입 시 사망 정리 1회 실행 (어느 경로로 죽든 여기로 일원화).
				// 풀 반납 등 "즉시 하면 사망 애니가 안 보이는" 처리는 Die() 구현부에서 _deathSequenceDuration만큼 지연.
				Die();
				break;

		}

	}
	protected virtual void OnStateExit(UNIT_STATE state)
	{
		// 엔진 사운드(IDLE/MOVING/BOOSTING)는 항상 재생 중인 상태로 두고 볼륨만 크로스페이드하므로
		// 상태 퇴장 시 따로 정지할 게 없음(UpdateEngineAudio() 참고).
	}
	protected virtual void OnIdle()
	{
		//애니메이션명령, 사운드재생?
	}
	protected virtual void OnMoving()
	{
		//애니메이션명령, 사운드재생?
	}
	protected virtual void OnBoosting()
	{
		//부스트 중 매 프레임 처리
	}
	protected virtual void OnBraking()
	{
		//입력없이 감속(브레이크) 중 매 프레임 처리
	}
	protected virtual void OnDodge()
	{
		//애니메이션명령, 사운드재생?
		//PlayAnim(ANIM_TYPE.DODGE_N);
		//SoundManager.Instance.PlaySFX3DAtPosition(SOUND_TYPE.) 회피소스넣기
		_dodgeTimer -= Time.deltaTime;
		if (IsInvincible && _dodgeTimer <= dodgeDuration - dodgeInvincibleTime)
		{
			IsInvincible = false;
			UpdateShieldHitboxState();
		}
		if (_dodgeTimer <= 0f)
		{
			_dodgeTimer = 0f;
			CurState = UNIT_STATE.IDLE;
		}
	}
	protected virtual void OnDying()
	{
		//애니메이션명령, 사운드재생?
		//죽는처리 - 풀매니저
	}

	//실드회복
	protected IEnumerator ShieldRegenerationRoutine()
	{
		//피격 후 설정된 딜레이(초)만큼 대기합니다. (Update의 타이머 연산을 완벽히 대체)
		yield return GameManager.WaitGameplaySeconds(shieldRegainDelay);

		// 회복 시작 시 Overlay 리셋 (파괴 상태 해제, 다시 피격 이펙트 보이게)
		_shieldOverlay?.TriggerReset();


		isShieldRegaining = true;

		// 최적화를 위해 0.1초마다 대기할 캐싱 객체 생성
		WaitForSeconds tick = new WaitForSeconds(0.1f);

		//  실드가 꽉 차지 않았고, 유닛이 살아있는 동안 반복해서 회복
		while (curShieldRemaining < maxShieldCapacity && curState != UNIT_STATE.DIE)
		{
			// 일시정지/게임오버 중엔 회복하지 않음 — 코루틴 tick(WaitForSeconds)은 플래그 방식 정지를
			// 무시하므로, 프리즈 동안은 회복 연산을 스킵한다(대기는 그대로 흘려보냄).
			if (!ShouldPause)
			{
				// 초당 회복량(shieldRegainRate)을 0.1초 기준 단위로 계산하여 더함
				curShieldRemaining += Mathf.RoundToInt(shieldRegainRate * 0.1f);
				curShieldRemaining = Mathf.Min(curShieldRemaining, maxShieldCapacity);

				// 0 -> 양수로 회복된 시점에 콜라이더 상태 갱신
				UpdateShieldHitboxState();
			}

			// 다음 0.1초까지 대기
			yield return tick;
		}

		// 회복이 완료되었거나 죽었을 경우 상태 초기화
		isShieldRegaining = false;
		_shieldRegenCoroutine = null;

		// 쉴드 완전 회복 시 Overlay 리셋

		_shieldOverlay?.TriggerReset();

	}

	//0516 실드회복 코루틴으로변겨ㅑㅇ
	//private void UpdateShieldRegen()
	//{
	//    if (curState == UNIT_STATE.DIE || curShieldRemaining >= maxShieldRemaining)
	//    {
	//        return;
	//    }
	//    //if(curShieldRemaning>=maxShieldRemaning)//디버그 로깅같은거 필요하면 주석풀고 위에서 지울것
	//    //{
	//    //	return;
	//    //}
	//    //타이머에 일정시간더해주고
	//    shieldRegainTimer += Time.deltaTime;
	//    //타이머가 딜레이보다 커졌고 충전중이아닐때, 즉 딜레이만큼시간지났을떄
	//    if (!isShieldRegaining && shieldRegainTimer >= shieldRegainDelay)
	//    {
	//        isShieldRegaining = true;
	//    }

	//    if (isShieldRegaining)
	//    {
	//        //반올림공식
	//        curShieldRemaining += Mathf.RoundToInt(shieldRegainRate * Time.deltaTime);
	//        //혹여나 초과시 제한걸도록 둘중 작은값 반환하는 함수(동일시 그값반환)
	//        curShieldRemaining = Mathf.Min(curShieldRemaining, maxShieldRemaining);
	//    }

	//}
	//부스트회복
	private void UpdateBoostRegen()
	{
		if (curState == UNIT_STATE.DIE || curBoostRemaining >= maxBoostCapacity)
		{
			return;
		}
		if (_isBoosting)
		{
			_boostRegainTimer = 0f;
			_isBoostRegaining = false;
			return;
		}
		//if(curBoostRemaining>=maxBoostRemaining)//디버그 로깅같은거 필요하면 주석풀고 위에서 지울것
		//{
		//	return;
		//}
		//타이머에 일정시간더해주고
		_boostRegainTimer += Time.deltaTime;
		//타이머가 딜레이보다 커졌고 충전중이아닐때, 즉 딜레이만큼시간지났을떄
		if (!_isBoostRegaining && _boostRegainTimer >= boostRegainDelay)
		{
			_isBoostRegaining = true;
		}

		if (_isBoostRegaining)
		{
			curBoostRemaining += boostRegainRate * Time.deltaTime;
			curBoostRemaining = Mathf.Min(curBoostRemaining, maxBoostCapacity);//실드와동일
		}
	}

	//부스트사용
	public void UseBoost(float amount)
	{
		curBoostRemaining = Mathf.Max(0f, curBoostRemaining - amount);
		_boostRegainTimer = 0f;
		_isBoostRegaining = false;
	}



	//(실드o,아머x)(HitInfo.damage) * (크리시)criDamageMultiplier;
	//(실드x,아머o)(HitInfo.damage-defense *(크리시)criDamageMultiplier;
	//(실드x,아머x)(HitInfo.damage) * (크리시)criDamageMultiplier;
	//반올림할것. 0.5->1 0.4->0

	//자식에서 오버라이드
	public virtual void Shoot(PROJECTILE_TYPE type)
	{
		if (weaponSystem != null)
		{
			weaponSystem.Shoot(type);
		}
	}

	/// <summary>
	/// 애니메이션 재생 호출용
	/// </summary>
	public void PlayAnim(ANIM_TYPE type, float duration = 0.1f)
	{
		if (_animCtrl != null)
		{
			_animCtrl.Play(type, duration);
		}
	}

	/// <summary>
	/// Unit TakeDamage(IDamageable 상속시 필수구현하는 메서드) 
	/// </summary>
	/// <param name="info"> 데미지정보구조체 받음</param>
	// 투사체/스킬이 부르는 데미지 진입점(IDamageable). 멀티에서 '대상 소유자'만 실제 데미지를 계산하도록 라우팅한다:
	// 내가 소유자가 아니면 여기서 처리하지 않고 소유자에게만 RPC로 넘긴다(권위 일원화 — 클라마다 HP 어긋남/이중적용 방지).
	// 싱글/오프라인(PhotonView 없음)은 항상 IsMine=true라 그대로 로컬 적용(기존 동작).
	// ※ 총알은 로컬 복제라 각 클라에 사본이 있으므로, '쏜 클라의 총알'만 여기까지 온다(복제탄은 데미지 권위 없음 — Projectile 참고).
	public void TakeDamage(HitInfo info)
	{
		if (_photonView != null && !_photonView.IsMine)
		{
			_photonView.RPC(nameof(RpcTakeDamage), _photonView.Owner,
				(int)info.type, info.damageAmount, info.isCritical,
				info.ignoreArmor, info.shieldDamageMultiplier, info.aoeRadius, info.hitPosition,
				(int)info.hitVfxType, (int)info.shieldHitVfxType, (int)info.hitSoundType);
			return;
		}
		ApplyHitDamage(info);
	}

	// 대상 소유자 클라에서만 실행되는 데미지 적용 RPC(위 라우터가 전송). 피격 VFX/사운드 종류도 함께 전송해
	// 소유자 화면에서 올바른 피격 연출이 나오게 함 — 누락하면 수신부에서 enum 기본값 0(VFX_EXPLOSION_MISSILE)으로
	// 재구성돼 총알 피격에도 폭발이 재생됨. attacker(킬 귀속)는 아직 미전송(후속).
	// public 필수 — PUN은 실제 컴포넌트(Enemy/Player 등 파생 타입)를 리플렉션해 [PunRPC]를 찾는데,
	// base(Unit)에 private로 선언하면 파생 타입에서 안 잡혀 "RPC method not found" 에러 남.
	[PunRPC]
	public void RpcTakeDamage(int type, int damageAmount, bool isCritical,
		bool ignoreArmor, float shieldDamageMultiplier, float aoeRadius, Vector3 hitPosition,
		int hitVfxType, int shieldHitVfxType, int hitSoundType)
	{
		HitInfo info = new HitInfo
		{
			type = (DAMAGE_TYPE)type,
			damageAmount = damageAmount,
			isCritical = isCritical,
			ignoreArmor = ignoreArmor,
			shieldDamageMultiplier = shieldDamageMultiplier,
			aoeRadius = aoeRadius,
			hitPosition = hitPosition,
			hitVfxType = (EFFECT_TYPE)hitVfxType,
			shieldHitVfxType = (EFFECT_TYPE)shieldHitVfxType,
			hitSoundType = (SOUND_TYPE)hitSoundType,
		};
		ApplyHitDamage(info);
	}

	// HP/실드 스트리밍 — 소유자(적=Master, 플레이어=본인)만 값을 쓰고 비소유자는 받기만 함.
	// 이게 없으면 비소유자 화면에서 체력바가 안 깎이고 풀피로 보이다 적이 갑자기 사라짐(데미지는 실제로 들어가는데 안 보이는 것).
	// 사망(CurState=DIE)·실드재생은 ApplyHitDamage(소유자 전용) 안에서만 트리거되므로, 비소유자가 값만 받아도 멋대로 죽거나 재생하지 않음.
	// 단, 실드 오버레이 시각(파괴/페이드/재생)은 비소유자에도 보여야 하므로 수신부에서 받은 실드량으로 직접 몰아줌.
	// ⚠ 에디터: PhotonView의 Observed Components에 이 유닛 컴포넌트(Player/Enemy)를 등록해야 호출됨.
	public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
	{
		if (stream.IsWriting)
		{
			stream.SendNext(curHpRemaining);
			stream.SendNext(curShieldRemaining);
		}
		else
		{
			bool hadShield = curShieldRemaining > 0;
			curHpRemaining = (int)stream.ReceiveNext();
			curShieldRemaining = (int)stream.ReceiveNext();
			bool hasShield = curShieldRemaining > 0;

			// 실드 유무가 바뀐 순간에만 콜라이더 갱신 — 피격 판정은 각 클라 로컬에서 나므로 비소유자도 콜라이더가 맞아야 함.
			if (hadShield != hasShield)
			{
				UpdateShieldHitboxState();
			}

			// 실드 오버레이 시각도 비소유자에서 재현 — 스트리밍은 값만 주므로 여기서 오버레이를 직접 몰아줌.
			// (소유자는 calculTakeDamage에서 파괴, ShieldRegenerationRoutine에서 리셋 — 그 대칭)
			if (_shieldOverlay != null && maxShieldCapacity > 0)
			{
				if (hadShield && !hasShield)
				{
					// 이번 갱신으로 실드가 완전 소진됨 — 파괴 이펙트/파괴음 1회(오버레이가 _isDestroyed로 중복 차단).
					_shieldOverlay.TriggerDestroy();
					_sound?.PlaySFX3DAtPosition(SOUND_TYPE.SFX_SHIELD_DESTROY, transform.position);
				}
				else if (!hadShield && hasShield)
				{
					// 실드가 다시 회복됨 — 파괴 상태 해제 후 HP 비율 반영.
					_shieldOverlay.TriggerReset();
					_shieldOverlay.UpdateShieldHP((float)curShieldRemaining / maxShieldCapacity);
				}
				else if (hasShield)
				{
					// 실드량 변화에 따른 페이드/깜빡임 반영.
					_shieldOverlay.UpdateShieldHP((float)curShieldRemaining / maxShieldCapacity);
				}
			}
		}
	}

	// 실제 데미지 적용(계산/파츠/사망 트리거). 회피·자원흡수 등은 서브클래스가 override.
	// 반드시 라우터(TakeDamage)를 거쳐 호출됨 — 소유자(또는 싱글) 클라에서만 실행 보장.
	protected virtual void ApplyHitDamage(HitInfo info)
	{

		if (IsInvincible)
		{
			return;
		}

		// 이미 사망 처리 중이면 추가 피격 무시 (사망 애니 재생 동안 중복 사망/피격 방지)
		if (CurState == UNIT_STATE.DIE)
		{
			return;
		}

		//info.isCritical = Random.Range(0f, 100f) < criChance; //크리판정은 투사체에서 직접담당.
		int damageAmount = info.isCritical ? Mathf.RoundToInt(info.damageAmount * criDamageMultiplier) : info.damageAmount;
		//실드회복중지, 타이머 초기화
		//shieldRegainTimer = 0f; //0516 코루틴으로 변경
		isShieldRegaining = false;

		if (_shieldRegenCoroutine != null)
		{
			StopCoroutine(_shieldRegenCoroutine);
		}
		// 실드가 막아주는 동안엔 파츠가 안 깎임 — 데미지 적용 '전' 실드 유무로 판정.
		bool shieldWasUp = curShieldRemaining > 0;

		//피격 데미지수치필요(실드있을시, 없을시),실제로 데미지받음
		calculTakeDamage(damageAmount, info.ignoreArmor, info.shieldDamageMultiplier);

		// 피격으로 실드가 0이 됐을수있으니 콜라이더 상태 갱신
		UpdateShieldHitboxState();

		// 파츠 피격 — FRAME HP는 본체가 담당하므로 FRAME 제외한 파츠만 처리.
		// 실드가 켜져 있었으면 실드가 대신 막은 것으로 보고 파츠는 안 깎음.
		if (_unitParts != null && !shieldWasUp)
		{

			//범위딜일시
			if (info.aoeRadius > 0f)
			{
				_unitParts.DamagePartsInRange(info.hitPosition, info.aoeRadius, damageAmount);
			}
			//단일딜일시
			else
			{
				_unitParts.DamageNearestPart(info.hitPosition, damageAmount);
			}
		}

		//피격 방향에 따른 리액션(사운드,이펙트,카메라흔들림, 혹은 밀려남등)
		OnHitReaction(info);

		if (curHpRemaining <= 0)
		{
			// 킬러 기록 — 사망 직전 마지막 타격의 공격자. 킬 보상 귀속용(멀티 대비).
			_lastAttacker = info.attacker;
			// 사망 트리거는 상태 전환만. 실제 정리(Die)는 OnStateEnter(DIE)에서 1회 호출됨(FSM 일원화).
			CurState = UNIT_STATE.DIE;
		}
		else//실드 배터리?엔진?이 파츠가 말짱할경우 조건추가
		{
			// 죽지 않았다면 딜레이 후 다시 실드가 차오르도록 코루틴을 새로 시작함
			_shieldRegenCoroutine = StartCoroutine(ShieldRegenerationRoutine());
		}

	}

	/// <summary>
	/// 피격 반동(카메라 쉐이크, 넉백등 자식에서 override)
	/// </summary>
	/// <param name="info"></param>
	// IHittable 구현 — 데미지 대상은 TakeDamage 내부에서, 환경은 투사체가 직접 호출. 그래서 public.
	public virtual void OnHitReaction(HitInfo info)
	{
		//피격 애니메이션재생 필요
		//피격 사운드재생 필요 실드있을떄는 실드사운드, 아니면 타입맞춰서
		//실드 없을때: 탄종(SO)에 등록된 hitSoundType 그대로 사용. 미등록(SFX_NONE)이면 SoundManager가 자동 무음 처리.
		//실드 있을때: 탄종별로 안 나누고 DAMAGE_TYPE 기준(GetPlaySoundTypeShield)으로 일괄 처리.
		if (curShieldRemaining <= 0)
		{
			_playSoundType = info.hitSoundType;
		}
		else
		{
			_playSoundType = GetPlaySoundTypeShield(info);

			_shieldOverlay?.Trigger(info.hitPosition);

			// [실드팀 참조 - 실드 피격 비주얼 연동 위치]
			// curShieldRemaining > 0 분기 = 이번 피격을 실드가 막아낸 경우.
			//info.hitPosition (Vector3) = 피격 월드 좌표.

			// shield 오브젝트에서 ProceduralForceFieldOverlay 가져와서
			// ex) shield.GetComponent<ProceduralForceFieldOverlay>().Trigger(info.hitPosition);
			// 호출하면 해당 위치에 실드 피격 이펙트(쉐이더 비주얼+사운드) 재생됨.
		}
		//Debug.Log($"[OnHitReaction-DEBUG] curShieldRemaining={curShieldRemaining}, _playSoundType={_playSoundType}, dmgType={info.type}");
		_sound.PlaySFX3DAtPosition(_playSoundType, info.hitPosition);

		// 피격 VFX — 사운드와 동일한 curShieldRemaining 기준으로 실드/일반 분기(사운드·VFX 일관성 유지).
		// 폭발(미사일)은 Missile.Explode()가 폭발 VFX를 이미 냈으므로 여기선 스킵(중복 방지).
		if (info.type != DAMAGE_TYPE.EXPLOSION)
		{
			EFFECT_TYPE hitVfx = (curShieldRemaining > 0) ? info.shieldHitVfxType : info.hitVfxType;
			VFXManager.Instance.PlayEffectAtPosition(hitVfx, info.hitPosition, Quaternion.identity);
		}
		
	}




	/// <summary>
	/// 사망처리(오브젝트 풀반납, 비활성화등. 플레이어와는 다르게 처리할거기때문에 자식에서 override)
	/// </summary>
	protected virtual void Die() { }

	//bool isCritical()
	//{
	//	float rand = Random.Range(0f, 100f);//0~100퍼
	//	return rand < criChance;
	//}

	protected void calculTakeDamage(int damageAmount, bool ignoreArmor, float shieldDamageMultiplier)
	{
		// shieldDamageMultiplier (실드뎀 추가비율) 미지정(0)이면 1로 보정 
		float multiplier = shieldDamageMultiplier > 0f ? shieldDamageMultiplier : 1f;

		if (curShieldRemaining > 0)
		{

			// 실드가 원본 데미지로 흡수 가능한 양만큼만 damageAmount에서 차감
			int absorbedOriginal = Mathf.Min(damageAmount, Mathf.FloorToInt(curShieldRemaining / multiplier));
			int shieldDamage = Mathf.Min(curShieldRemaining, Mathf.RoundToInt(absorbedOriginal * multiplier));//현지실드량보다 초과해서 -가되면 안됨
			curShieldRemaining -= shieldDamage;//실드에 가해진 피해량만큼 현재실드량 깎기
			damageAmount -= absorbedOriginal;//실드가 흡수한 원본 데미지만큼 데미지잔량도 깎기

			// 쉴드 오버레이에 HP 비율 전달 (깜빡임/투명도 연출용)
			if (shield != null && maxShieldCapacity > 0)
			{
				float hpRatio = (float)curShieldRemaining / maxShieldCapacity;

				if (_shieldOverlay != null)
				{
					_shieldOverlay.UpdateShieldHP(hpRatio);
					// 쉴드 완전 소진 시 파괴 이펙트
					if (curShieldRemaining <= 0)
					{
						_shieldOverlay.TriggerDestroy();
							// 실드가 이번 히트로 완전 소진된 순간 파괴음 1회 재생.
							// (바깥 if가 curShieldRemaining>0 진입 조건이라 깨지는 그 히트에서만 <=0 → 중복 없음)
							_sound?.PlaySFX3DAtPosition(SOUND_TYPE.SFX_SHIELD_DESTROY, transform.position);
					}
				}
			}
		}
		if (damageAmount > 0 && curArmorRemaining > 0)//데미지잔량0초과,실드0,아머0초과
		{
			// 관통탄(ignoreArmor): 아머 자체는 그대로 깎이되, defense(방어력) 경감만 무시하고 통과
			int effectiveDefense = ignoreArmor ? 0 : defense;
			int reducedDamage = Mathf.Max(1, damageAmount - effectiveDefense);//아머가몇이건 최소 1이건 데미지들어감
			int armorDamage = Mathf.Min(curArmorRemaining, reducedDamage);//아머로 경감한데미지만큼 현재아머량깎기 초과해서 -가되면안되므로
			curArmorRemaining -= armorDamage;//아머에 가해진피해량만큼깎기

			// 아머가 남아있던 동안의 피격은 이번 히트로 아머가 깨지더라도 초과분이 HP로 안 넘어감 —
			// 아머가 "이미 0이었던" 다음 히트부터만 HP가 닳음(이번 호출에서 curArmorRemaining>0으로 들어왔으므로 통과데미지 0).
			damageAmount = 0;

		}
		if (damageAmount > 0)
		{

			int hpDamage = Mathf.Min(curHpRemaining, damageAmount);
			curHpRemaining -= hpDamage;
		}

	}



	public virtual void OnCollisionEnter(Collision collision)
	{

	}



	//레거시
	///// <summary>
	///// 재생할 사운드 찾는 함수 (오버로딩)
	///// 피격
	///// </summary>
	///// <param name="info">맞은 투사체 정보</param>
	///// <returns></returns>
	//protected SOUND_TYPE GetPlaySoundType(HitInfo info)
	//{


	//	switch (info.type)
	//	{
	//		case DAMAGE_TYPE.BULLET:
	//			return SOUND_TYPE.SFX_BULLETHIT;

	//		case DAMAGE_TYPE.LASER:
	//			return SOUND_TYPE.SFX_LASERHIT;

	//		case DAMAGE_TYPE.EXPLOSION:
	//			return SOUND_TYPE.SFX_NONE; // 폭발 소리는 Missile.Explode()가 담당

	//		case DAMAGE_TYPE.CONTACT:
	//			if (info.attacker.CompareTag("Enemy"))
	//			{
	//				return SOUND_TYPE.SFX_CONTACTSHIP;
	//			}
	//			if (info.attacker.CompareTag("Ground"))
	//			{
	//				return SOUND_TYPE.SFX_CONTACTGROUND;
	//			}
	//			break;
	//	}
	//	return SOUND_TYPE.SFX_NONE;
	//}
	// 발사 사운드/머즐 결정 로직은 WeaponSystem.ShootBulletFrom()/ShootMissileFrom()으로 통합 이전됨 (구 GetPlaySoundType(PROJECTILE_TYPE) 삭제).
	// BULLET/MISSILE은 풀에서 꺼낸 프리팹 자신의 bulletData/missileData(shootSoundType, muzzleEffectType)를 직접 사용. LASER만 고정값 유지.


	protected SOUND_TYPE GetPlaySoundTypeShield(HitInfo info)
	{
		switch (info.type)
		{
			case DAMAGE_TYPE.BULLET:
				return SOUND_TYPE.SFX_BULLETHIT_SHIELD;

			//case DAMAGE_TYPE.LASER:
			//	return SOUND_TYPE.SFX_LASERHIT_SHIELD;

			case DAMAGE_TYPE.EXPLOSION:
				return SOUND_TYPE.SFX_EXPLOSION_SHIELD;

			case DAMAGE_TYPE.CONTACT:
				if (info.attacker.CompareTag("Enemy"))
				{
					return SOUND_TYPE.SFX_CONTACTSHIP_SHIELD;
				}
				if (info.attacker.CompareTag("Ground"))
				{
					return SOUND_TYPE.SFX_CONTACTGROUND_SHIELD;
				}
				break;
		}
		return SOUND_TYPE.SFX_NONE;
	}
}
