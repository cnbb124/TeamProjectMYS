using System.Collections;
using System.Collections.Generic;

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
//   예시)
//   float hpRatio     = (float)unit.curHpRemaining / unit.maxHpRemaining;
//   float shieldRatio = (float)unit.curShieldRemaining / unit.maxShieldCapacity;
//   float boostRatio  = unit.curBoostRemaining / unit.maxBoostCapacity;
//
// ▶ 이펙트/사운드팀 참조용
//   OnHitReaction(DamageInfo info) : 피격 시 Player/Enemy에서 override → 여기서 VFXManager 호출
//   OnStateEnter(UNIT_STATE state) : IDLE/MOVING/BOOSTING 애니+루프사운드 재생, DODGE 무적/타이머, DIE 애니
//   OnStateExit(UNIT_STATE state)  : IDLE/MOVING/BOOSTING 루프사운드 정지
//                                    Player는 DODGE(RCS버스트)만 추가 override
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

public abstract class Unit : MonoBehaviour, IDamageable
{


	//==================레퍼런스==================//


	//=============기타 레퍼런스===============
	//애니메이션 컨트롤러 할당용
	protected UnitAnimCtrl _animCtrl;
	//리지드바디 할당용 레퍼런스
	protected Rigidbody _rb;
	//외부(Enemy AI 예측사격 등)에서 실제 이동 속도벡터 참조용
	public Vector3 Velocity => _rb != null ? _rb.velocity : Vector3.zero;
	//매니저 할당용 레퍼런스
	protected SoundManager _sound;
	protected PoolManager _pool;
	[HideInInspector]
	public WeaponSystem weaponSystem;
	protected UnitParts _unitParts;

	//==================유닛데이터==================//

	[Header("<size=22>유닛 공통 기본 스탯 설정창</size>")]

	[Header("<size=18>HP</size>")]
	public int maxHpRemaining = 150; //최대,현재HP수치


	[Header("<size=18>Shield - 피격 후 일정 딜레이 후 자동회복</size>")]
	public int maxShieldCapacity;//최대,현재실드수치

	public float shieldRegainDelay;//피격후 회복까지딜레이시간
	[Range(6.0f,100.0f)]
	[Tooltip("실드 초당 회복수치(최소 6)")]
	public float shieldRegainRate; //실드회복수치
										 //private float shieldRegainTimer = 0f;//딜레이 시간까지잴 타이머 >0516 코루틴으로변경
	public bool isShieldRegaining = false; //회복중인지 여부
	private Coroutine _shieldRegenCoroutine;//중간 정지등을 위한 코루틴변수 따로
											//실드연결용
											// [실드팀 참조] 실드 비주얼 오브젝트(ProceduralForceFieldOverlay 등 부착된 자식) 연결용.
											// curShieldRemaining > 0 ↔ SetActive(true), <= 0 ↔ SetActive(false) 로 표시 여부 제어 권장.
											// 피격 이펙트(Trigger) 호출은 OnHitReaction()에서 처리.
	public GameObject shield;

	[Tooltip("실드가 있을때 OFF, 없을때 ON 되는 본체 HitBox 연결. 실드 콜라이더와 상호토글됨.")]
	public Transform bodyHitboxRoot;

	// bodyHitboxRoot 하위 콜라이더 캐싱용 
	private Collider[] _bodyHitboxColliders;

	// 실드 오브젝트(자식 포함) 콜라이더 캐싱용
	private Collider[] _shieldColliders;


	[Header("<size=18>Armor - 자동회복 X</size>")]
	public int maxArmor;//최대,현재아머수치


	[Tooltip("Armor보유시 데미지 경감되는 수치.")]
	public int defense;//아머 있을시 데미지 경감수치(damageAmount=damage-defense)



	[Header("<size=18>Critical</size>")]
	public float criChance;
	public float criDamageMultiplier;




	// 크리여부 판정은 투사체가 담당 크확은 유닛이. → DamageInfo.isCritical로 전달받음
	// criChance는 투사체 생성 시 attacker에서 복사해서 사용
	//데미지 계산식
	//shield>armor>hp순 실드없고 armor있을때는 경감수치만큼 데미지 경감
	//damageAmount=
	//(실드o,아머x)(Damageinfo.damage) * (크리시)criDamageMultiplier;
	//(실드x,아머o)Damageinfo.damage-defense *(크리시)criDamageMultiplier;
	//(실드x,아머x)Damageinfo.damage) * (크리시)criDamageMultiplier;
	//curHp-=damageAmount;
	[Header("=========터렛등 좌표고정유닛은 적용안됨==========")]
	[Space(5)]
	[Header("<size=18>이동 관련</size>")]
	[Tooltip("기본 이동속도 (초당 이동 거리, unit/s). 예: 350이면 초당 350유닛 이동.")]
	public float baseMoveSpeed;//기본이동속ㄷ
	[Tooltip("부스트 사용시 이동속도 (초당 이동 거리, unit/s)")]
	public float boostSpeed;//부스트사용시 이동속도
	[Tooltip("최대속도velocity가 넘어갈시 고정시킬속도")]
	public float maxSpeed;
	[Tooltip("목표 속도(baseMoveSpeed/boostSpeed)까지 도달하는 데 걸리는 시간(초).\n" +
			 "작을수록 빠릿하게 반응함. base든 boost든 목표속도가 달라도 항상 이 시간만큼 걸림(내부에서 목표속도÷이 시간으로 가속력 계산).")]
	public float timeToMaxSpeed = 0.4f;
	[Tooltip("입력을 떼고 완전히 멈추는 데 걸리는 시간(초).\n" +
			 "Rigidbody.drag를 0으로 빼서(가속 시 목표속도까지 정확히 도달하게 하려고) 자연 감속이 없어졌으므로,\n" +
			 "정지 시 감속을 이 값으로 직접 제어함. 멈추기 시작한 시점의 속도를 기준으로 항상 이 시간 안에 0이 됨.")]
	public float timeToStop = 0.4f;
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


	private float boostRegainTimer = 0f;//부스트 회복딜레이까지 잴 타이머
	private bool isBoostRegaining = false;//회복유무
	protected bool _isBoosting = false;//부스트 사용 중 여부 (자식에서 설정)



	[Header("<size=18>회피 & 무적</size>")]
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
		_rb = GetComponent<Rigidbody>();
		_animCtrl = GetComponent<UnitAnimCtrl>();
		weaponSystem = GetComponent<WeaponSystem>();
		_unitParts = GetComponent<UnitParts>();

		if (shield != null)
		{
			_shieldColliders = shield.GetComponentsInChildren<Collider>();
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
		CurState = UNIT_STATE.IDLE;
	}

	// OnEnable과 대칭. 부모 오브젝트가 SetActive(false)되면 자식들도 같이 비활성화되며
	// 자식 각각의 OnDisable도 호출됨 — 자식이 독립된 Unit(터렛 등)일 때 자기 자신의 정리를 직접 하게 하는 용도.
	protected virtual void OnDisable() { }

	// Start is called before the first frame update
	protected virtual void Start()
	{
		_sound = SoundManager.Instance;
		_pool = PoolManager.Instance;
		//인스펙터에서 입력된 값 현재 스탯으로 설정
		//저장 기능 생길시 변경필요.

		// 엔진 사운드 3레이어 — 한 번 걸어두면 죽을 때까지 계속 재생, UpdateEngineAudio()가 볼륨만 조절
		_idleLoop = _sound?.PlaySFX3DLoop(SOUND_TYPE.SFX_IDLE, transform);
		_thrustLoop = _sound?.PlaySFX3DLoop(SOUND_TYPE.SFX_MOVING, transform);
		_boostLoop = _sound?.PlaySFX3DLoop(SOUND_TYPE.SFX_BOOST, transform);
		// PlaySFX3DLoop()가 SoundManager에 등록된 기본 볼륨으로 즉시 Play()해버리므로,
		// 다음 Update() 전까지 잠깐 잘못된(0이어야 할 가속/부스트음이 들리는) 볼륨으로 재생되는 버그가 있었음 —
		// 같은 프레임에서 바로 한 번 보정해서 그 틈을 없앰.
		UpdateEngineAudio();




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

		updateTimer += Time.deltaTime;
		if (updateTimer > 0.5f)
		{
			curSpeed = _rb != null ? (_rb.velocity.magnitude < 0.01f ? 0f : _rb.velocity.magnitude) : 0f;
			updateTimer = 0f;
		}

		UpdateEngineAudio();
	}

	protected virtual void FixedUpdate()
	{

	}

	// 매 프레임 실제 속도(_rb.velocity, 0.5초 캐시인 curSpeed 말고 즉시값 사용)를 기준으로
	// 공회전/가속/부스트 3레이어의 볼륨(+가속음 피치)을 크로스페이드. CurState==DIE면 전부 무음.
	private void UpdateEngineAudio()
	{
		if (CurState == UNIT_STATE.DIE || _rb == null)
		{
			if (_idleLoop != null) _idleLoop.volume = 0f;
			if (_thrustLoop != null) _thrustLoop.volume = 0f;
			if (_boostLoop != null) _boostLoop.volume = 0f;
			return;
		}

		float speedRatio = maxSpeed > 0f ? Mathf.Clamp01(_rb.velocity.magnitude / maxSpeed) : 0f;

		if (_idleLoop != null)
		{
			_idleLoop.volume = Mathf.Lerp(idleMaxVolume, 0f, speedRatio);
		}
		if (_thrustLoop != null)
		{
			_thrustLoop.volume = Mathf.Lerp(0f, thrustMaxVolume, speedRatio);
			_thrustLoop.pitch = Mathf.Lerp(thrustMinPitch, thrustMaxPitch, speedRatio);
		}
		if (_boostLoop != null)
		{
			float targetVolume = _isBoosting ? boostMaxVolume : 0f;
			_boostLoop.volume = Mathf.MoveTowards(_boostLoop.volume, targetVolume, Time.deltaTime * boostFadeSpeed);
		}
	}
	// GetFirePos / GetBoostPos 제거 — WeaponSystem이 직접 _bulletFirePositions 등을 보유


	//[HideInInspector]
	//public Transform curFirePos;//밑에서 총구스위칭용 
	//필요없음.

	[Header("===============<size=14>현재 상태(참고용 입력x )</size>================")]
	[Tooltip("UNIT_STATE — \"지금 어떤 상태인가\" (표현/물리 레이어)")]
	public UNIT_STATE curState = UNIT_STATE.IDLE;
	// 직전 상태. 전환별로 다른 애니메이션 블렌드(CrossFade duration)를 적용할 때 참조
	protected UNIT_STATE previousState = UNIT_STATE.IDLE;
	public int curHpRemaining;
	public int CurHp => curHpRemaining;//인터페이스 프로퍼티용
									   //public int CurShiled => curShieldRemaining;//인터페이스 프로퍼티용
	public int curShieldRemaining;
	public int curArmorRemaining;
	public float curSpeed;
	public float curBoostRemaining;//부스트잔량
								   //잔탄도추가예정

	private float updateTimer = 0f;

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

	// =====================================================================
	// 엔진 사운드 (공회전/가속/부스트 레이어 — 항상 동시 재생, 속도 기준으로 볼륨만 크로스페이드)
	// =====================================================================
	[Header("<size=14>엔진 사운드 크로스페이드</size>")]
	[Tooltip("정지 상태(속도비율 0)일 때 공회전음 최대 볼륨")]
	public float idleMaxVolume = 1f;
	[Tooltip("최고속(속도비율 1)일 때 가속음 최대 볼륨")]
	public float thrustMaxVolume = 1f;
	[Tooltip("부스트 중일 때 부스트음 최대 볼륨")]
	public float boostMaxVolume = 1f;
	[Tooltip("부스트 사운드가 켜지고/꺼질 때 볼륨이 변하는 속도(초당)")]
	public float boostFadeSpeed = 4f;
	[Tooltip("가속음 피치 범위 — 속도비율 0일 때 minPitch, 1일 때 maxPitch")]
	public float thrustMinPitch = 0.9f;
	public float thrustMaxPitch = 1.3f;

	private AudioSource _idleLoop;
	private AudioSource _thrustLoop;
	private AudioSource _boostLoop;



	// =====================================================================
	// 일시정지 / 게임오버 체크
	// Player, Enemy 등 자식 클래스의 Update/FixedUpdate 첫 줄에서 사용.
	// Unit.Update() 에도 적용 - 자식이 base.Update() 호출 시 이중 안전망.
	// =====================================================================
	protected bool ShouldPause =>
		GameManager.Instance != null &&
		(GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver);









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
			previousState = curState;
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
				if (previousState == UNIT_STATE.BOOSTING)
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
				if (previousState == UNIT_STATE.BOOSTING)
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

			case UNIT_STATE.DODGE:
				//PlayAnim(ANIM_TYPE.DODGE_N);키입력따라 좌우 혹은 랜덤방향(키입력없을때)
				_dodgeTimer = dodgeDuration;
				IsInvincible = true;
				_dodgeCooldownTimer = dodgeCoolTime;
				UpdateShieldHitboxState();
				break;

			case UNIT_STATE.DIE:
				PlayAnim(ANIM_TYPE.DIE);
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
		yield return new WaitForSeconds(shieldRegainDelay);

		// 회복 시작 시 Overlay 리셋 (파괴 상태 해제, 다시 피격 이펙트 보이게)
		if (shield != null)
		{
			var overlay = shield.GetComponentInChildren<ProceduralForceField.ProceduralForceFieldOverlay>();
			overlay?.TriggerReset();
		}

		isShieldRegaining = true;

		// 최적화를 위해 0.1초마다 대기할 캐싱 객체 생성
		WaitForSeconds tick = new WaitForSeconds(0.1f);

		//  실드가 꽉 차지 않았고, 유닛이 살아있는 동안 반복해서 회복
		while (curShieldRemaining < maxShieldCapacity && curState != UNIT_STATE.DIE)
		{
			// 초당 회복량(shieldRegainRate)을 0.1초 기준 단위로 계산하여 더함
			curShieldRemaining += Mathf.RoundToInt(shieldRegainRate * 0.1f);
			curShieldRemaining = Mathf.Min(curShieldRemaining, maxShieldCapacity);

			// 0 -> 양수로 회복된 시점에 콜라이더 상태 갱신
			UpdateShieldHitboxState();

			// 다음 0.1초까지 대기
			yield return tick;
		}

		// 회복이 완료되었거나 죽었을 경우 상태 초기화
		isShieldRegaining = false;
		_shieldRegenCoroutine = null;

		// 쉴드 완전 회복 시 Overlay 리셋
		if (shield != null)
		{
			var overlay = shield.GetComponentInChildren<ProceduralForceField.ProceduralForceFieldOverlay>();
			overlay?.TriggerReset();
		}
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
			boostRegainTimer = 0f;
			isBoostRegaining = false;
			return;
		}
		//if(curBoostRemaining>=maxBoostRemaining)//디버그 로깅같은거 필요하면 주석풀고 위에서 지울것
		//{
		//	return;
		//}
		//타이머에 일정시간더해주고
		boostRegainTimer += Time.deltaTime;
		//타이머가 딜레이보다 커졌고 충전중이아닐때, 즉 딜레이만큼시간지났을떄
		if (!isBoostRegaining && boostRegainTimer >= boostRegainDelay)
		{
			isBoostRegaining = true;
		}

		if (isBoostRegaining)
		{
			curBoostRemaining += boostRegainRate * Time.deltaTime;
			curBoostRemaining = Mathf.Min(curBoostRemaining, maxBoostCapacity);//실드와동일
		}
	}

	//부스트사용
	public void UseBoost(float amount)
	{
		curBoostRemaining = Mathf.Max(0f, curBoostRemaining - amount);
		boostRegainTimer = 0f;
		isBoostRegaining = false;
	}



	//(실드o,아머x)(Damageinfo.damage) * (크리시)criDamageMultiplier;
	//(실드x,아머o)Damageinfo.damage-defense *(크리시)criDamageMultiplier;
	//(실드x,아머x)Damageinfo.damage) * (크리시)criDamageMultiplier;
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
	public virtual void TakeDamage(DamageInfo info)
	{

		if (IsInvincible)
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
		//피격 데미지수치필요(실드있을시, 없을시),실제로 데미지받음
		calculTakeDamage(damageAmount);

		// 피격으로 실드가 0이 됐을수있으니 콜라이더 상태 갱신
		UpdateShieldHitboxState();

		// 파츠 피격 — FRAME HP는 본체가 담당하므로 FRAME 제외한 파츠만 처리
		if (_unitParts != null)
		{
			if (info.aoeRadius > 0f)
			{
				_unitParts.DamagePartsInRange(info.hitPosition, info.aoeRadius, damageAmount);
			}
			else
			{
				_unitParts.DamageNearestPart(info.hitPosition, damageAmount);
			}
		}

		//피격 방향에 따른 리액션(사운드,이펙트,카메라흔들림, 혹은 밀려남등)
		OnHitReaction(info);

		if (curHpRemaining <= 0)
		{
			CurState = UNIT_STATE.DIE;
			Die();
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
	protected virtual void OnHitReaction(DamageInfo info)
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

			if (shield != null)
				shield.GetComponentInChildren<ProceduralForceField.ProceduralForceFieldOverlay>()?.Trigger(info.hitPosition);

			// [실드팀 참조 - 실드 피격 비주얼 연동 위치]
			// curShieldRemaining > 0 분기 = 이번 피격을 실드가 막아낸 경우.
			//info.hitPosition (Vector3) = 피격 월드 좌표.

			// shield 오브젝트에서 ProceduralForceFieldOverlay 가져와서
			// ex) shield.GetComponent<ProceduralForceFieldOverlay>().Trigger(info.hitPosition);
			// 호출하면 해당 위치에 실드 피격 이펙트(쉐이더 비주얼+사운드) 재생됨.
		}
		//Debug.Log($"[OnHitReaction-DEBUG] curShieldRemaining={curShieldRemaining}, _playSoundType={_playSoundType}, dmgType={info.type}");
		_sound.PlaySFX3DAtPosition(_playSoundType, info.hitPosition);
		//피격 카메라무빙필요

		//크리면 데미지 배율, 아니면 그냥 데미지
		//데미지인포에서 총알인지 폭발인지 레이저인지에 따라서
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

	protected void calculTakeDamage(int damageAmount)
	{

		if (curShieldRemaining > 0)
		{
			int shieldDamage = Mathf.Min(curShieldRemaining, damageAmount);//현지실드량보다 초과해서 -가되면 안됨
			curShieldRemaining -= shieldDamage;//실드에 가해진 피해량만큼 현재실드량 깎기
			damageAmount -= shieldDamage;//실드에 가해진피해량만큼 데미지잔량도 깎기

			// 쉴드 Overlay에 HP 비율 전달 (깜빡임/투명도 연출용)
			if (shield != null && maxShieldCapacity > 0)
			{
				float hpRatio = (float)curShieldRemaining / maxShieldCapacity;
				var overlay = shield.GetComponentInChildren<ProceduralForceField.ProceduralForceFieldOverlay>();
				if (overlay != null)
				{
					overlay.UpdateShieldHP(hpRatio);
					// 쉴드 완전 소진 시 파괴 이펙트
					if (curShieldRemaining <= 0)
						overlay.TriggerDestroy();
				}
			}
		}
		if (damageAmount > 0 && curArmorRemaining > 0)//데미지잔량0초과,실드0,아머0초과
		{
			int reducedDamage = Mathf.Max(1, damageAmount - defense);//아머가몇이건 최소 1이건 데미지들어감
			int armorDamage = Mathf.Min(curArmorRemaining, reducedDamage);//아머로 경감한데미지만큼 현재아머량깎기 초과해서 -가되면안되므로
			curArmorRemaining -= armorDamage;//아머에 가해진피해량만큼깎기
			damageAmount -= armorDamage;//아머에 가해진 피해량만큼 데미지잔량도깎기

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
	//protected SOUND_TYPE GetPlaySoundType(DamageInfo info)
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


	protected SOUND_TYPE GetPlaySoundTypeShield(DamageInfo info)
	{
		switch (info.type)
		{
			case DAMAGE_TYPE.BULLET:
				return SOUND_TYPE.SFX_BULLETHIT_SHIELD;

			case DAMAGE_TYPE.LASER:
				return SOUND_TYPE.SFX_LASERHIT_SHIELD;

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
