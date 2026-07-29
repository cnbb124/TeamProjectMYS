using System.Collections;
using System.Collections.Generic;

using Photon.Pun;
using UnityEngine;




// ================================================================
// [외부 참조 가이드]
// ================================================================
// ▶ HUD팀 참조용 (읽기 전용으로 사용할 것)
//   level            : 현재 레벨
//   exp              : 현재 경험치
//   expToNextLevel   : 다음 레벨까지 필요 경험치
//   curFuelRemaining : 현재 연료량
//   maxFuelCapacity  : 최대 연료량
//   (HP / 실드 / 부스트 등은 Unit.cs 참조)
//
//   onHitDirectionWorld : HitDirectionHandler — 피격 시 HitInfo.hitDiriection 그대로 전달
//                         (투사체→플레이어 방향. 화면 위치 계산 시 부호 반전 필요할 수 있음)
//                         예) GameManager.Instance.playerRef.onHitDirectionWorld += ShowHitIndicator;
//                             void ShowHitIndicator(Vector3 dir) { /* -dir 방향이 공격자 위치 */ }
//
//   예시)
//   float expRatio = (float)player.exp / player.expToNextLevel;
//   hudManager.SetLevel(player.level);
//
// ▶ 저장/로드 시스템 참조용
//   GameManager.CollectSaveData()에서 직접 읽어감 (별도 호출 불필요)
//   GameManager.Instance.playerRef 로 접근
//
// ▶ 스킬 UI팀 참조용 (WeaponSystem과 같은 자리 — 캐릭터 전용 컴포넌트)
//   skillSystem.slots[i]              : i번 슬롯에 등록된 ActiveSkill (없으면 null)
//   skillSystem.CurrentSlotIndex      : 현재 선택된 슬롯 인덱스
//   skillSystem.GetCooldownRatio(int) : 쿨다운 진행 비율(0~1). 게이지 UI용.
//   skillSystem.LearnSkill(SkillData) : 스킬 배움(레벨업/상점 등에서 호출). 액티브면 빈 슬롯에 자동 배치.
//
// ▶ 파티클팀 참조용 (추진/RCS 파티클 — 이름 기반 자동 탐색)
//   파티클은 THRUSTER 파츠 프리팹(Thruster_Main.prefab) 내부에 배치할 것.
//   Player가 Start 1프레임 뒤 자식 전체에서 아래 "이름"의 오브젝트를 찾아
//   그 아래 모든 ParticleSystem을 수집/제어함 (개수 자유, Play On Awake 전부 OFF).
//
//   [현재 사용 중 — 이름 변경 금지]
//   Step1_Slow       : 상시 약한 분사
//   Step2_Normal     : 전진 입력 중 루프 재생
//   Step3_Boost      : 부스트 중 루프 재생 (자식 중 fire_3-3 제외)
//   fire_3-3         : 부스트 진입 순간 1회 버스트
//
//   [RCS/후진/닷지용 예약 이름 — 컨테이너만 만들면 됨, 제어 코드는 추후 연결]
//   RCS_Roll_L       : Q(좌측 롤) 입력 중 루프 재생            → Looping ON
//   RCS_Roll_R       : E(우측 롤) 입력 중 루프 재생            → Looping ON
//   Thruster_Reverse : 후진(S) 입력 중 루프 재생               → Looping ON
//   RCS_Burst_L      : 좌측 닷지 순간 1회 (RCS_Roll_L과 같은 위치, 더 강한 이펙트) → Looping OFF
//   RCS_Burst_R      : 우측 닷지 순간 1회 (RCS_Roll_R과 같은 위치, 더 강한 이펙트) → Looping OFF
// ================================================================

//
// 플레이어 전용 컴포넌트.
// Unit을 상속받아 스탯/FSM/데미지 처리는 Unit에서,
// 플레이어 입력을 받는 이동/회전/사격 입력 처리는 여기서 담당.



public delegate void HitDirectionHandler(Vector3 dir);

[RequireComponent(typeof(Rigidbody))]
public class Player : Unit
{



	// =================================================

	// 추진기 파티클
	private ParticleSystem[] _step1Particles;
	private ParticleSystem[] _step2Particles;
	private ParticleSystem[] _step3Particles;
	private bool _wasBoosting = false;
	private bool _wasMoving = false;
	private ParticleSystem[] _boostBurstParticles;

	// RCS 방향 인덱스
	private enum RCS { RTop = 0, RBot = 1, LTop = 2, LBot = 3 }

	// Dodge / Roll RCS — [방향][파티클 인덱스]
	private ParticleSystem[][] _rcsDodge = new ParticleSystem[4][];
	private ParticleSystem[][] _rcsRoll  = new ParticleSystem[4][];

	// 역추진 파티클
	private ParticleSystem[] _reverseMainL;   // Rev-Booster_L 하위 (1회 버스트)
	private ParticleSystem[] _reverseMainR;   // Rev-Booster_R 하위 (1회 버스트)
	private ParticleSystem[] _reverseSubs;    // Rev-Sub-Booster_1~8 (루프)

	// 측면 이동(Strafe) RCS — A키 → 우측(R) 분사, D키 → 좌측(L) 분사
	private ParticleSystem[] _strafeL;        // RCS_Strafe_L 하위 (D키 누를 때)
	private ParticleSystem[] _strafeR;        // RCS_Strafe_R 하위 (A키 누를 때)

	//매니저 할당용 레퍼런스
	private InputManager _input;

	// 멀티플레이 소유권(_photonView/IsMine)은 base(Unit)로 통일됨 — 데미지 권위 라우팅이 base에서 필요하기 때문.
	// PhotonNetwork.Instantiate로 스폰된 함선만 PhotonView를 가지며, 남의 함선은 IsMine=false가 되어 입력이 차단된다.

	public QuickSlot quickSlot { get; private set; }
	//public SkillSystem skillSystem { get; private set; }





	// ==================회전 감도==================
	[Header("")]
	[Space(10)]
	[Header("<size=22>플레이어 설정<size>")]
	// 마우스 예민도(조종간이 얼마나 쉽게 기울어지나)는 InputManager 소관 — 여기 값들은 장치와 무관한
	// 기체 자체의 선회 성능임. 패드로 조종해도 똑같이 적용됨.
	[Header("기체 선회 성능")]
	[Tooltip("좌우 선회(Yaw) 최고 속도(도/초). 조종간을 최대로 꺾었을 때의 회전 속도임.")]
	public float yawSpeed = 120f;

	[Tooltip("상하 선회(Pitch) 최고 속도(도/초).")]
	public float pitchSpeed = 120f;

	[Tooltip("Q/E 롤(Z축 회전) 최고 속도(도/초).")]
	public float rollSpeed = 120f;

	[Tooltip("조종간을 최대로 꺾었을 때 최고 회전속도에 도달하기까지 걸리는 시간(초).\n" +
			 "기체 관성에 해당함 — 클수록 묵직하게 늦게 돌기 시작하고, 되돌릴 때도 서서히 멈춤.\n" +
			 "0.4~0.8 권장. 0이면 예전처럼 즉시 최고속도로 돎.")]
	public float timeToMaxTurn = 0.5f;

	// 현재 각속도(도/초). x=Pitch  y=Yaw  z=Roll
	// 조종간 기울기는 '목표' 각속도일 뿐이고, 실제 회전은 이 값이 목표를 따라잡으면서 일어남.
	private Vector3 _angularVelocity;

	// 마지막 물리 스텝 이후 흐른 시간(초). 0~fixedDeltaTime 사이를 오감.
	// 회전이 FixedUpdate에서만 갱신되는 탓에 생기는 계단을 HUD 쪽에서 지우는 데 씀(AimRotation 참고).
	//
	// 직접 누적하지 않고 Unity 시계(Time.fixedTime)에서 뽑는 이유:
	// 프레임당 몇 번의 물리 스텝이 도는지, Update가 중간에 걸러지는지에 전혀 영향받지 않음.
	// 물리 스텝이 도는 순간 이 값이 딱 스텝 하나만큼 줄어서, 회전이 튄 양과 정확히 상쇄됨.
	private float TurnExtrapTime
	{
		get
		{
			return Mathf.Clamp(Time.time - Time.fixedTime, 0f, Time.fixedDeltaTime);
		}
	}

	// 카메라가 따라갈 분신. 위치는 기체와 같고 회전만 매 프레임 보정된 값이 들어감.
	// 기체를 직접 따라가면 카메라가 물리 스텝 계단을 물려받아 떨리므로 한 겹 끼워둔 것(Start 참고).
	private Transform _camAimProxy;


	// ==================플레이어용==================
	[Header("경험치/레벨")]
	[Tooltip("현재 레벨. 최소 1")]
	public int level = 1;//차후 mathf.max치로 조정

	[Tooltip("현재 보유 경험치. 음수 불가")]
	public int exp = 0;//차후 mathf.max치로 조정

	[Tooltip("현재 레벨에서 다음 레벨까지 필요한 경험치")]
	public int expToNextLevel = 100;

	[Tooltip("미사일 슬롯을 1칸 늘려줄 레벨 주기. 3이면 3/6/9레벨마다 +1칸.\n" +
		"1이면 매 레벨 증가(과할 수 있음). 0 이하로 두면 슬롯 지급 안 함.")]
	public int missileSlotGainInterval = 3;



	[Header("연료 소모량 입력")]
	[Tooltip("이동 시 초당 연료 소모량")]
	public float fuelMoveConsumeRate = 5f;
	[Tooltip("부스트 사용 시 추가 초당 연료 소모량")]
	public float fuelBoostConsumeRate = 10f;
	[Header("연료 최대량&잔량")]
	[Tooltip("최대 연료량.")]
	public float maxFuelCapacity = 0f;
	[Tooltip("현재 연료 잔량 HUD 연결용. 입력X")]
	public float curFuelRemaining;


	[Header("좌우, 상하 이동")]
	[Range(0f, 1f)]
	[Tooltip("좌우(A/D)/상하(Mouse4,5) 이동 속도 비율 (전진 baseMoveSpeed/boostSpeed 대비).\n" +
		"1이면 전진과 동일 속도, 작을수록 옆/위아래 이동이 느려짐.")]
	public float strafeSpeedRatio = 0.5f;


	// ==================락온 시스템==================




	private Vector3 _dodgeDir;
	// timeToStop 계산용 — 입력이 막 끊긴 순간의 속도를 한 번만 저장해서 그 값 기준으로 감속력을 고정시킴.
	// (기존 _wasMoving은 UpdateBoostEffect()의 파티클 상태추적용으로 이미 쓰이고 있어서 이름 다르게 둠)
	private float _decelStartSpeed;
	private bool _wasThrusting;


	protected override void Awake()
	{
		base.Awake();
		// 우주 공간 = 중력 없음. 회전은 직접 제어하므로 물리 회전 고정
		_rb.useGravity = false;
		_rb.freezeRotation = true;
		quickSlot = GetComponent<QuickSlot>();
		//skillSystem = GetComponent<SkillSystem>();
	}
	// Start is called before the first frame update
	protected override void Start()
	{
		base.Start(); //유닛 초기화 호출 (RefillToMax 포함, Player override로 연료까지 채워짐)
		UnitManager.Instance.RegisterPlayer(this);

		// 네트워크 스폰 함선(PhotonView 보유, 멀티+오프라인)은 씬 로드에도 살아남게 DDOL.
		// 안 그러면 '남'이 씬을 로드할 때 그의 로컬에서 이 함선 복사본이 파괴되고, Photon은 이미 인스턴스화된
		// 오브젝트를 재생성하지 않아(버퍼된 Instantiate는 입장 시 1회뿐) 서로 안 보이게 된다.
		// 내 함선은 전투씬 이탈 시 NetworkManager.DestroyLocalPlayerShip으로 명시 제거하므로 스테이션 등에 안 남는다.
		// 씬 배치 싱글(PhotonView 없음)은 DDOL 안 함 — 기존 동작 유지.
		if (_photonView != null)
		{
			DontDestroyOnLoad(gameObject);
		}

		// 로컬 플레이어(내 함선)만 GameManager의 대표 참조로 등록.
		// 멀티에선 Player가 씬 로드 후 PhotonNetwork.Instantiate로 스폰돼 OnSceneLoaded의 FindObjectOfType가 놓치므로,
		// 여기서 자기 자신을 등록한다. 남의 함선(IsMine=false)은 등록하지 않는다. 싱글은 IsMine=true라 동일 동작.
		if (IsMine && GameManager.Instance != null)
		{
			GameManager.Instance.playerRef = this;
		}

		// 내 함선만 게임플레이 카메라(vCam)가 따라오게 붙인다. 남 함선(IsMine=false)엔 안 붙음.
		// 멀티에선 런타임 스폰이라 인스펙터로 미리 못 걸어서 여기서 자기 자신을 대상으로 등록.
		//
		// 단, 기체 트랜스폼을 직접 물리지 않고 '분신'을 물린다.
		// 회전은 FixedUpdate(초당 50회)에서만 기록되는데 카메라는 매 프레임 갱신되므로,
		// 기체를 직접 따라가게 하면 카메라가 그 계단을 그대로 물려받아 화면과 HUD가 떨림.
		// 분신에는 매 프레임 보정된 회전(AimRotation)을 넣어주므로 카메라 입력이 매끄러워짐.
		if (IsMine)
		{
			_camAimProxy = new GameObject($"{name}_CameraAimProxy").transform;
			_camAimProxy.SetPositionAndRotation(transform.position, transform.rotation);

			// 함선이 씬을 넘어 살아남는 경우(네트워크 스폰) 분신도 같이 살아남아야 카메라가 안 끊김
			if (_photonView != null)
			{
				DontDestroyOnLoad(_camAimProxy.gameObject);
			}

			if (CameraManager.Instance != null)
			{
				CameraManager.Instance.SetFollowTarget(_camAimProxy);
			}
		}
		_input = InputManager.Instance;
		// 게임 시작 시 1번 슬롯 무기로 초기화
		weaponSystem.Init();
		if (InventoryManager.Instance != null)
		{
			InventoryManager.Instance.RegisterPlayer(this);
		}

		StartCoroutine(InitParticlesNextFrame());
	}


	[Header("<size=14>엔진 쓰로틀(엔진음 반응)</size>")]
	[Tooltip("가속 입력에 따라 엔진음이 차오르고/잦아드는 속도(초당). 클수록 빠릿하게 반응.\n" +
			 "엔진음이 실제 속도가 아니라 '가속 입력(쓰로틀)'을 따라감 — 닷지로 순간 빨라져도 엔진음은 입력 기준.")]
	[SerializeField] private float throttleResponseSpeed = 3f;
	private float _throttle;              // 현재 쓰로틀(0~1). 매 프레임 목표로 램프.
	private float _engineThrottleTarget;  // 목표 쓰로틀 = 방향 가중 입력 크기(MovingByInput에서 매 FixedUpdate 갱신).
	                                       // 전진=풀(1), 좌우/후진=strafeSpeedRatio 배 → 실제 속도 가중과 동일.

	// 엔진음을 실제 속도가 아니라 '쓰로틀'(방향 가중 입력)에 연동 (Unit.GetEngineIntensity override).
	// 입력이 있으면 서서히 목표로 차오르고, 없으면 0으로 잦아든다.
	protected override float GetEngineIntensity()
	{
		_throttle = Mathf.MoveTowards(_throttle, _engineThrottleTarget, throttleResponseSpeed * Time.deltaTime);
		return _throttle;
	}

	// Update is called once per frame
	protected override void Update()
	{
		if (ShouldPause) return;
		base.Update(); //FSM, 실드/부스트 회복 호출
					   // 입력처리 - InputManager 구현 뒤 여기서 호출
					   // ex. InputManager.Instance.HandleInput(this);

		// 남의 함선(멀티)이면 입력을 읽지 않는다. 위치/상태는 PhotonView 동기화로만 갱신됨.
		if (!IsMine)
		{
			return;
		}

		// 카메라용 분신 갱신 — Cinemachine이 LateUpdate에서 읽으므로 반드시 그 전인 여기서 채워야 함.
		// 위치는 물리가 이미 프레임 단위로 보간해주고 있어서 그대로 복사하면 되고, 회전만 보정값을 넣음.
		//
		// 아래 사망 가드보다 위에 있어야 함 — 죽은 뒤에도 카메라는 잔해를 계속 따라가야
		// 폭발 연출이 화면에 보인다. 여기서 멈추면 기체만 관성으로 날아가고 카메라는 제자리에 남는다.
		if (_camAimProxy != null)
		{
			_camAimProxy.SetPositionAndRotation(transform.position, AimRotation);
		}

		// 죽었으면 조종 불가. base.Update()는 위에서 이미 돌렸으므로 사망 타이머(OnDying)는 계속 진행됨.
		// ShouldPause는 IsGameOver를 보는데 게임오버는 연출이 끝나야 켜지므로,
		// 이 가드가 없으면 폭발하는 동안 기체가 그대로 조종된다.
		if (curState == UNIT_STATE.DIE)
		{
			return;
		}

		if (_input == null)
		{
			return;
		}

		//==========혹여나 업뎃이 입력없을때도 필요한게ㅐ 있으면 이 위로 입력학ㄹ것=========
		//미사일 장착 토글 처리(온오프) 상태변화 관련이므로 즉시 Update로
		// 미사일 슬롯 전환 - WeaponSystem 위임
		if (_input.switchMissileNext)
		{
			weaponSystem.SwitchMissileNext();
		}
		else if (_input.switchMissilePrev)
		{
			weaponSystem.SwitchMissilePrev();
		}

		if (_input.switchLockOnTarget != 0f && weaponSystem.lockOnSystem != null)
		{
			//휠 올릴때 좌측, 내릴떄 우측
			weaponSystem.lockOnSystem.SwitchTarget(_input.switchLockOnTarget > 0 ? -1 : 1);
		}

		// 발사 모드 토글 제거 — 발사 수는 firePositions.Count와 curAmmo로 자동 결정
		ShootByInput();

		if (skillSystem != null)
		{
			if (_input.switchSkillSlot)
			{
				skillSystem.SwitchSlot();
			}
			if (_input.useSkill)
			{
				skillSystem.UseCurrentSlot();
			}
		}

		// 회피 입력 — GetKeyDown은 Update에서만 안정적으로 감지됨 (FixedUpdate에서 씹힘)
		if (curState != UNIT_STATE.DODGE && _input.isDodging && _dodgeCooldownTimer <= 0f)
		{
			CurState = UNIT_STATE.DODGE;
		}

	}

	protected override void FixedUpdate()
	{
		base.FixedUpdate(); // 일시정지 시 Rigidbody 프리즈/복원 처리
		if (ShouldPause)
		{
			return;
		}
		// 죽었으면 입력 기반 이동/회전을 돌리지 않는다.
		// MovingByInput()이 끝에서 속도를 보고 CurState를 IDLE/MOVING/BRAKE로 다시 세팅하는데,
		// 그 분기가 걸러내는 건 DODGE뿐이라 DIE도 덮어써버린다. 그러면 상태가 DIE에서 빠져나가
		// UpdateFSM이 OnDying()을 더 이상 호출하지 않고, 사망 타이머가 멈춰 게임오버로 못 넘어간다.
		if (curState == UNIT_STATE.DIE)
		{
			return;
		}

		// 남의 함선(멀티)이면 입력 기반 이동/회전을 돌리지 않는다. 위치는 PhotonView 동기화로만.
		if (!IsMine)
		{
			return;
		}
		if (_input == null)
		{
			return;
		}

		// 회전은 반드시 여기(FixedUpdate)에서 — Rigidbody Interpolate가 매 프레임 트랜스폼을 보간값으로
		// 덮어쓰기 때문에, Update에서 transform.Rotate로 직접 돌리면 그 결과가 지워졌다 다시 더해지길
		// 반복하며 심하게 떨림. 물리 동기화 직전인 여기서 돌려야 충돌이 없음.
		//항상 회전이먼저!!!
		RotateByInput();
		MovingByInput();

		// 파티클 온오프는 50Hz면 충분하고 눈에 차이가 없음 — 매 프레임 돌릴 이유가 없어 여기 둠.
		UpdateBoostEffect();
		UpdateRcsEffect();
		UpdateReverseInputEffect();
	}







	//===============override 메서드 FSM==================
	// IDLE / MOVING / BOOSTING 애니+사운드는 Unit.OnStateEnter에서 처리.
	// Player는 입력 의존적인 DODGE 방향/RCS, BRAKE 역추진 파티클, DIE 로그만 추가 처리.
	protected override void OnStateEnter(UNIT_STATE state)
	{
		base.OnStateEnter(state);
		switch (state)
		{
			case UNIT_STATE.BRAKE:
				// 메인(Rev-Booster_L/R) 1회 펑 — Clear 후 Play로 매번 확실히 터지게
				if (_reverseMainL != null) foreach (var ps in _reverseMainL) { if (ps != null) { ps.Clear(); ps.Play(); } }
				if (_reverseMainR != null) foreach (var ps in _reverseMainR) { if (ps != null) { ps.Clear(); ps.Play(); } }
				// 보조(Rev-Sub) 8개 루프 ON
				PlayAll(_reverseSubs);

				// 좌우(strafe) 잔여 속도 방향을 보고 감속에 맞는 분사구 선택.
				// UpdateRcsEffect() 가속 매핑(A입력→우측분사, D입력→좌측분사)과는 반대쪽 —
				// 가속은 "가려는 방향과 반대편" 분사구가 밀어주고, 브레이크는 "미끄러지는 방향과 같은편" 분사구가 막아줌.
				float lateralVel = Vector3.Dot(_rb.velocity, transform.right);
				if (lateralVel > 0.1f)       // 우측으로 미끄러지는 중 → 우측 분사구로 막음
				{
					PlayAll(_strafeR);
				}
				else if (lateralVel < -0.1f) // 좌측으로 미끄러지는 중 → 좌측 분사구로 막음
				{
					PlayAll(_strafeL);
				}
				break;

			case UNIT_STATE.DODGE:
				// 좌우 입력 있으면 해당 방향, 없으면 좌/우 랜덤 — 방향을 변수에 저장해 RCS와 동기화
				bool dodgeLeft;
				// 좌입력
				if (_input != null && _input.moveInput.x < -0.1f)
				{
					dodgeLeft = true;
					PlayAnim(ANIM_TYPE.DODGE_L);
				}
				// 우입력
				else if (_input != null && _input.moveInput.x > 0.1f)
				{
					dodgeLeft = false;
					PlayAnim(ANIM_TYPE.DODGE_R);
				}
				// 좌우입력없을시 랜덤모션
				else
				{
					dodgeLeft = Random.Range(0, 2) == 0;//0이면 true, 1이면 false 즉 50퍼
					PlayAnim(dodgeLeft ? ANIM_TYPE.DODGE_L : ANIM_TYPE.DODGE_R);
				}

				// 이동입력
				if (_input != null && _input.moveInput.magnitude > 0.1f)
				{
					_dodgeDir = transform.forward * _input.moveInput.z
							  + transform.right * _input.moveInput.x
							  + transform.up * _input.moveInput.y;
					_dodgeDir.Normalize();
				}
				// 이동입력없을시 — 위에서 정한 dodgeLeft(좌우 랜덤)와 동일한 방향으로 이동 (애니/RCS와 동기화)
				else
				{
					_dodgeDir = dodgeLeft ? -transform.right : transform.right;
				}
				// dodgeDistance(실제 이동거리)÷dodgeDuration = 필요한 속도. VelocityChange는 mass와 무관하게 그 속도를 그대로 더해줌.
				_rb.AddForce(_dodgeDir * (dodgeDistance / dodgeDuration), ForceMode.VelocityChange);

				// 닷지 순간 메인/보조 부스터 fire_3-3 버스트 1회 재생
				PlayAll(_boostBurstParticles);

				// 닷지 방향에 맞는 RCS 버스트 1회 재생 (애니메이션 방향과 동기화)
				if (dodgeLeft)
				{
					PlayAll(_rcsDodge[(int)RCS.RBot]); PlayAll(_rcsDodge[(int)RCS.LTop]);
					PlayAll(_rcsRoll[(int)RCS.RBot]);  PlayAll(_rcsRoll[(int)RCS.LTop]);
				}
				else
				{
					PlayAll(_rcsDodge[(int)RCS.RTop]); PlayAll(_rcsDodge[(int)RCS.LBot]);
					PlayAll(_rcsRoll[(int)RCS.RTop]);  PlayAll(_rcsRoll[(int)RCS.LBot]);
				}
				break;

			case UNIT_STATE.DIE:
				Debug.Log("[Player] 사망");
				break;
		}
	}


	// IDLE / MOVING / BOOSTING 루프 사운드 정지는 Unit.OnStateExit에서 처리.
	// Player는 DODGE RCS 정지, BRAKE 역추진 보조/좌우 분사 정지만 추가 처리.
	protected override void OnStateExit(UNIT_STATE state)
	{
		base.OnStateExit(state);
		switch (state)
		{
			case UNIT_STATE.DODGE:
				foreach (var arr in _rcsRoll) StopAll(arr);
				_wasThrusting = true; // 닷지 임펄스로 생긴 속도를 감속 스냅샷이 잡을 수 있게
				break;

			case UNIT_STATE.BRAKE:
				StopAll(_reverseSubs);
				StopAll(_strafeL);
				StopAll(_strafeR);
				break;
		}
	}
	protected override void OnIdle() { }
	protected override void OnMoving() { }
	protected override void OnBoosting() { }
	protected override void OnDodge() { base.OnDodge(); }
	// 사망 연출이 끝나면 게임오버로 넘어감.
	// Die()에서 바로 GameOver()를 부르면 폭발이 터지는 순간 게임오버 UI가 같이 떠서 연출이 안 보임.
	// Enemy가 풀 반납을 미루는 것과 같은 자리·같은 방식이고, 시간도 같은 _deathSequenceDuration을 씀.
	//
	// 코루틴이 아니라 타이머인 이유: 이 프로젝트의 일시정지가 timeScale이 아니라 플래그 방식이라
	// 멈춘 동안 Update가 안 돌아야 카운트다운도 같이 멈춤(Enemy.OnDying과 동일).
	// GameOver 전까지는 IsGameOver가 false라 ShouldPause에 안 걸려 타이머가 정상 진행됨.
	protected override void OnDying()
	{
		if (_gameOverTriggered)
		{
			return;
		}

		_deathTimer -= Time.deltaTime;
		if (_deathTimer > 0f)
		{
			return;
		}

		_gameOverTriggered = true;
		GameManager.Instance.GameOver();
	}

	// 사망 연출 잔여시간. Die()에서 세팅하고 OnDying()이 깎음.
	private float _deathTimer;
	// GameOver를 한 번만 부르기 위한 표식 — OnDying은 사망 상태 동안 매 프레임 돌기 때문.
	private bool _gameOverTriggered;

	// UI팀 구독용 — 피격 시 공격자의 월드 방향 벡터 전달 (정규화).
	// HUD 피격 방향 인디케이터에서 이 이벤트를 구독하면 됨.
	// 예) player.OnHitDirectionWorld += dir => hudManager.ShowHitIndicator(dir);
	public HitDirectionHandler onHitDirectionWorld;

	// 피격 반동 - 카메라 쉐이크, 넉백 등 (Unit.OnHitReaction public화에 맞춰 public override)
	public override void OnHitReaction(HitInfo info)
	{
		base.OnHitReaction(info);

		// 피격 방향 이벤트 발생 (HUD 피격 인디케이터용)
		// hitDiriection = 투사체→플레이어 방향 (총알이 날아온 방향).
		// 화면 어느 쪽에서 맞았는지 표시할 때는 -hitDiriection(플레이어→공격자)을 사용할 것.
		if (info.hitDiriection != Vector3.zero)
		{
			onHitDirectionWorld?.Invoke(info.hitDiriection);
		}

		// 피격 카메라 흔들림 — 받은 데미지 비례(크리면 증폭). 플레이어가 맞았을 때만.
		CameraShaker.Instance?.ShakeDamage(info.hitPosition, info.damageAmount, info.isCritical);
	}

	// 함선이 사라지면 카메라용 분신도 같이 치움 — 안 그러면 빈 오브젝트가 씬에 남음
	private void OnDestroy()
	{
		if (_camAimProxy != null)
		{
			Destroy(_camAimProxy.gameObject);
		}
	}

	// 사망처리
	protected override void Die()
	{
		UnitManager.Instance.UnregisterPlayer(this);
		// 게임오버는 여기서 바로 부르지 않음 — 사망 연출(폭발/사망모션)이 다 나온 뒤
		// OnDying()이 타이머를 다 깎으면 그때 호출함.
		_deathTimer = _deathSequenceDuration;
		_gameOverTriggered = false;
		//기타 필요한거 반납??여기서해야하나
	}




	// ===========================================
	// Shoot - Unit의 배열 총구 사용
	// 총알: 좌우 교대 고정
	// 미사일: 좌/우
	// 레이저: 머리 중앙 고정
	// ALL: 전체 동시(총알제외)
	// =================================================


	//================InputManager에서 입력받을시 작동할 입력처리 조작관련 메서드=============

	// 늘 회전후 이동하게 rotate부터 호출할것

	/// <summary>
	/// 	발사 (Update에서 호출)
	/// 	입력 기반 발사 명령 처리.
	/// </summary>

	/// 실제 투사체 생성은 Shoot() 내부에서 PoolManager 호출 예정.
	/// GetKeyDown 씹힘 방지를 위해 Update에서 호출.

	void ShootByInput()
	{
		//혹여나 버그걸릴시 다시 매니저 직접인스턴스할것. 스타트속도등으로 버그날수있따함.
		// 총알발사 입력
		if (_input.fireBullet)
		{
			Shoot(PROJECTILE_TYPE.BULLET);
		}
		//미사일 발사 입력
		if (_input.fireMissile)
		{
			Shoot(PROJECTILE_TYPE.MISSILE);
		}
		////전체발사(총알제외.총알은 좌클릭으로유지)입력
		//if (_input.fireAll)
		//{
		//	Shoot(PROJECTILE_TYPE.MISSILE);
		//}
		//미사용레거시
	}


	//부스트 로직
	//W(전진)     → BACK 부스터(뒤에서 밀어줌)
	//S(후진)     → FRONT 부스터(앞에서 밀어줌)
	//A(좌이동)   → RIGHT 부스터(오른쪽에서 밀어줌)
	//D(우이동)   → LEFT 부스터(왼쪽에서 밀어줌)
	//Mouse4(상승) → 없음 or 하단 부스터(추후 추가)
	//Mouse3(하강) → 없음 or 상단 부스터(추후 추가)
	//Q(좌롤)     → 윙 rightdown부스터, leftup부스터
	//E(우롤)     → 윙 rightup 부스터  leftdown부스터
	//마우스 상하  → FRONT or BACK 부스터
	//손 뗌        → 역분사(모두 켜거나 반대 부스터)


	// ==================회전 (FixedUpdate에서 호출)==================


	// 조종간 입력으로 오브젝트 자체를 3축 회전.
	// Rigidbody.freezeRotation = true이므로 transform.Rotate 직접 사용.
	//
	// lookInput은 '조종간을 얼마나 기울였나'(-1~1)임 — 마우스 이동량이 아님.
	// 마우스는 InputManager가 델타를 누적해 가상 조종간 위치로 바꿔서 넘겨주고, 패드 스틱은 원래 그 값이라
	// 여기서는 장치를 구분할 필요가 없음. 기울인 만큼의 속도로 계속 회전하고, 중앙으로 되돌려야 멈춤.
	// 그래서 yawSpeed/pitchSpeed는 '최대로 기울였을 때의 회전 속도(도/초)' 의미가 됨.
	//
	// Yaw  (Y축): 마우스 X → 좌우 회전. Space.World 기준
	// Pitch(X축): 마우스 Y → 상하 회전. Space.Self 기준
	// Roll (Z축): Q/E      → 좌우 스핀. Space.Self 기준
	// 
	// Space.Self 사용 이유: 어느 방향 바라봐도 직관적으로 상하/롤 회전됨.





	private void RotateByInput()
	{
		float dt = Time.fixedDeltaTime;

		// 조종간 기울기 → '목표' 각속도(도/초). 최대로 꺾으면 감도값이 그대로 최고 회전속도가 됨.
		float targetYaw = _input.lookInput.x * yawSpeed;
		float targetPitch = -_input.lookInput.y * pitchSpeed;
		// lookInput.y 반전: 마우스 위로 올리면 기수가 올라가야 하므로
		float targetRoll = -_input.rollInput * rollSpeed;
		// rollInput 반전: E키 눌렀을 때 오른쪽으로 기우는 방향

		// 기체 관성 — 조종간을 꺾어도 기수가 즉시 안 돌고 목표 속도까지 서서히 붙음.
		// 조종간을 중앙으로 되돌리면 목표가 0이 되어 같은 비율로 서서히 멈춤(관성으로 지나치지 않음).
		// 축마다 최고 속도가 달라서, 전부 timeToMaxTurn 안에 자기 최고치에 닿도록 축별로 가속도를 나눠 냄.
		float ramp = Mathf.Max(0.01f, timeToMaxTurn);
		_angularVelocity.y = Mathf.MoveTowards(_angularVelocity.y, targetYaw, (yawSpeed / ramp) * dt);
		_angularVelocity.x = Mathf.MoveTowards(_angularVelocity.x, targetPitch, (pitchSpeed / ramp) * dt);
		_angularVelocity.z = Mathf.MoveTowards(_angularVelocity.z, targetRoll, (rollSpeed / ramp) * dt);

		transform.Rotate(transform.up, _angularVelocity.y * dt, Space.World);
		transform.Rotate(Vector3.right, _angularVelocity.x * dt, Space.Self);
		transform.Rotate(Vector3.forward, _angularVelocity.z * dt, Space.Self);
	}

	// ==================HUD용 회전 보정==================
	//
	// 렌더 프레임 시점으로 맞춘 기수 회전. 매 프레임 카메라와 대조하는 쪽은
	// transform.rotation/forward 대신 반드시 이 값을 쓸 것.
	//
	// [왜 필요한가]
	// 회전은 FixedUpdate(50Hz)에서만 기록되고, freezeRotation 때문에 Rigidbody 보간 대상도 아님
	// (Interpolate는 물리가 소유한 회전만 보간해 줌 — 여기선 transform에 직접 쓰므로 해당 없음).
	// 반면 카메라는 Cinemachine이 렌더 프레임마다 부드럽게 갱신함.
	// 그래서 '기수 방향과 카메라 방향의 각도차'로 화면 위치가 정해지는 마커에는
	// 두 갱신 주기의 차이가 물리 스텝 계단(회전속도 × 0.02초)으로 그대로 드러남.
	// 마우스 크로스헤어는 입력값을 매 프레임 그대로 읽어서 이 문제가 없음.
	//
	// 마지막 스텝 이후 경과시간만큼 현재 각속도로 앞질러 계산해 그 계단을 메움.
	// 스텝이 진행되는 순간 경과시간도 같은 양만큼 줄어들어 값이 이어짐 — 지연이 늘지 않음.
	public Quaternion AimRotation
	{
		get
		{
			return ApplyTurn(transform.rotation, _angularVelocity, TurnExtrapTime);
		}
	}

	// 3축 회전을 dt만큼 적용한 결과를 돌려줌.
	// 축과 적용 순서가 RotateByInput과 반드시 같아야 보정값이 실제 회전과 어긋나지 않음.
	private Quaternion ApplyTurn(Quaternion rot, Vector3 angularVel, float dt)
	{
		rot = Quaternion.AngleAxis(angularVel.y * dt, rot * Vector3.up) * rot;  // Yaw   : 자기 up축 기준(Space.World)
		rot = rot * Quaternion.AngleAxis(angularVel.x * dt, Vector3.right);     // Pitch : 로컬(Space.Self)
		rot = rot * Quaternion.AngleAxis(angularVel.z * dt, Vector3.forward);   // Roll  : 로컬(Space.Self)
		return rot;
	}



	// ==================이동 (FixedUpdate에서 호출)==================
	// 오브젝트가 바라보는 방향(로컬축) 기준으로 6방향 물리 이동.
	// RotateByInput() 이후 호출되므로 이미 회전된 방향 기준으로 이동.

	// transform.forward = 오브젝트가 바라보는 방향 (W/S)
	// transform.right   = 오브젝트 기준 오른쪽    (A/D)
	// transform.up      = 오브젝트 기준 위쪽      (Mouse4/Mouse3)

	// 부스트: LeftShift + 잔량 있을 때 boostSpeed 적용.
	//         Unit.UseBoost()로 잔량 소모 및 회복 타이머 초기화.
	// maxSpeed: 속도 초과 시 방향 유지하고 크기만 클램프.
	private void MovingByInput()
	{

		if (curState == UNIT_STATE.DODGE)
		{
			return; // 회피 중 입력 차단, 관성은 유지됨
		}

		float forwardInput = Mathf.Max(0f, _input.moveInput.z);
		float backwardInput = Mathf.Min(0f, _input.moveInput.z);
		// 로컬 축 기준 6방향 합산 — 전진/후진(z)은 기준속도 그대로, 좌우(x)/상하(y)는 strafeSpeedRatio만큼 느리게
		Vector3 forwardDir = transform.forward * forwardInput;
		Vector3 nonForwardDir = transform.right * _input.moveInput.x +
							transform.up * _input.moveInput.y +
							transform.forward * backwardInput;
		

		if (nonForwardDir.magnitude > 1f)
		{
			nonForwardDir.Normalize();
		}

		//Vector3 lateralDir = transform.right * _input.moveInput.x + transform.up * _input.moveInput.y;
		//// 좌우+상하 동시 입력 시 그쪽만 속도 튀는 것 방지 (전진과는 별개로 클램프)
		//if (lateralDir.magnitude > 1f)
		//{
		//	lateralDir.Normalize();
		//}

		//Vector3 dir = forwardDir + lateralDir * strafeSpeedRatio;


		//  전진 벡터 + (전진 외 벡터 * strafeSpeedRatio)
		Vector3 dir = forwardDir + nonForwardDir * strafeSpeedRatio;

		// 엔진음 쓰로틀 목표 = 방향 가중 입력 크기(normalize 전 값). 전진=1, 좌우/후진=strafeSpeedRatio 배.
		_engineThrottleTarget = Mathf.Clamp01(dir.magnitude);

		if (dir.magnitude > 1f)
		{
			dir.Normalize();
		}
		//float 오차 패딩값
		bool isMoving = dir.sqrMagnitude > 0.001f;

		

		// 부스트 조건: Shift 누름 + 잔량 남아있음 + 전진 + 연료있음
		bool canBoost = _input.isBoosting && curBoostRemaining > minBoostRequired && _input.moveInput.z > 0 && curFuelRemaining > 0;
		_isBoosting = canBoost;

		// baseMoveSpeed/boostSpeed = 더 이상 "힘"이 아니라 실제 도달하는 목표 속도 그 자체.
		// (이전엔 AddForce(ForceMode.Acceleration) + Rigidbody.drag 평형점이 실속도였어서,
		//  이 값과 실제 도달 속도가 안 맞았음 — MoveTowards로 직접 목표속도를 따라가게 변경)
		float speed = canBoost ? boostSpeed : baseMoveSpeed;
		// speedMultiPlier: 피격/HP에 따른 속도 감소용. 0이면 1배율 적용
		float multiplier = speedMultiPlier > 0f ? speedMultiPlier : 1f;

		// 목표 속도(방향 포함)
		float targetSpeed = speed * multiplier;

		// 목표 속도로 점진적 접근 (timeToMaxSpeed초 만에 도달하도록 가속력을 매 프레임 역산)
		if (isMoving)
		{
			if (curFuelRemaining > 0f)
			{
				// 연료 소모 (이동 기본 + 부스트 중이면 추가)
				float fuelCost = fuelMoveConsumeRate;
				if (canBoost)
				{
					// Unit.UseBoost(): 잔량 감소 + 회복 타이머 초기화
					UseBoost(boostConsumeAmont * Time.fixedDeltaTime);

					fuelCost += fuelBoostConsumeRate;
				}
				curFuelRemaining = Mathf.Max(0f, curFuelRemaining - fuelCost * Time.fixedDeltaTime);

				// 입력 방향으로 목표 속도까지 가속 — targetSpeed÷timeToMaxSpeed초 만에 도달
				Vector3 targetVelocity = dir * targetSpeed;
				float accel = (timeToMaxSpeed > 0f) ? targetSpeed / timeToMaxSpeed : float.MaxValue;
				_rb.velocity = Vector3.MoveTowards(_rb.velocity, targetVelocity, accel * Time.fixedDeltaTime);
			}
			// 연료 없으면 추진력 없음 (관성은 유지)
			_wasThrusting = true;
		}
		else
		{
			// 방향키 입력 없음 — timeToStop초 만에 0까지 직접 감속.
			// (Rigidbody.drag를 0으로 뺐기 때문에 — drag가 살아있으면 MoveTowards로 정한 속도를
			//  매 물리스텝마다 깎아먹어서 목표속도(base/boost)에 도달을 못 하는 문제가 있었음)
			// 멈추기 시작한 순간의 속도를 한 번 저장해두고 그 값 기준으로 감속력을 고정 —
			// 매 프레임 "현재속도÷timeToStop"으로 다시 계산하면 속도가 줄어들수록 감속력도 줄어들어
			// 드래그처럼 영원히 0에 못 도달하는 지수감쇠가 되어버린다.
			if (_wasThrusting)
			{
				_decelStartSpeed = _rb.velocity.magnitude;
				_wasThrusting = false;
			}
			float decel = (timeToStop > 0f) ? _decelStartSpeed / timeToStop : float.MaxValue;
			// 역추진(브레이크) 이펙트는 OnStateEnter/OnStateExit(UNIT_STATE.BRAKE)에서 처리
			_rb.velocity = Vector3.MoveTowards(_rb.velocity, Vector3.zero, decel * Time.fixedDeltaTime);
			// if (_rb.velocity.sqrMagnitude > 0.1f)
			// {
			// 	//ex PlayReverseThrusterEffect();
			// 	// ex anim.SetBool("isReverseThrusting", true);
			// }
			// else
			// {
			// 	// ex StopReverseThrusterEffect();
			// 	// ex anim.SetBool("isReverseThrusting", false);
			// }
		}

		// maxSpeed 클램프 (초과 시 방향 유지하고 크기만 제한)
		float maxV = maxSpeed;
		if (_rb.velocity.magnitude > maxV)
		{
			_rb.velocity = _rb.velocity.normalized * maxV;
		}

		// FSM 상태 전환 (MOVING / BRAKE / IDLE — 물리 기반이므로 FixedUpdate에서)
		// DODGE 전환은 GetKeyDown 특성상 Update()에서 처리
		if (curState == UNIT_STATE.DODGE)
		{
			// 회피 중 — Unit.OnDodge()의 타이머가 IDLE 복귀를 담당
		}
		else if (isMoving)
		{
			if (canBoost)
			{
				CurState = UNIT_STATE.BOOSTING;
			}
			else
			{
				CurState = UNIT_STATE.MOVING;
			}
		}

		else//입력없을시 — 속도(최대속도 대비 비율) 기준으로 BRAKE/MOVING/IDLE 판정
		{
			if (_rb.velocity.sqrMagnitude <= 0.1f)
			{
				CurState = UNIT_STATE.IDLE;
			}
			else
			{
				float speedRatio = maxSpeed > 0f ? _rb.velocity.magnitude / maxSpeed : 0f;
				bool wasBraking = CurState == UNIT_STATE.BRAKE;
				// 히스테리시스: 이미 BRAKE면 이탈기준(brakeExitSpeedRatio) 밑으로 떨어질 때까지 유지,
				// 아니면 진입기준(brakeEnterSpeedRatio) 넘을 때만 BRAKE로 진입.
				// 부스트 중 입력을 놓아도 이미 고속이라 자동으로 여기서 BRAKE로 걸림 (별도 BOOSTING 분기 불필요).
				if (wasBraking ? speedRatio > brakeExitSpeedRatio : speedRatio >= brakeEnterSpeedRatio)
				{
					CurState = UNIT_STATE.BRAKE;
				}
				else
				{
					CurState = UNIT_STATE.MOVING;
				}
			}
		}

	}

	//==============부스트이펙트====================(이동에서같이)
	private IEnumerator InitParticlesNextFrame()
	{
		yield return null;
		_step1Particles = FindParticlesByName("Step1_Slow");
		_step2Particles = FindParticlesByName("Step2_Normal");
		_step3Particles = FindParticlesByNameExclude("Step3_Boost", "fire_3-3");

		_boostBurstParticles = FindParticlesByName("fire_3-3");
		// 닷지 RCS의 "D_fire_3-3"도 "fire_3-3"으로 끝나 EndsWith 검색에 같이 잡힘 —
		// 부스터 입력 시 닷지 이펙트가 오발동하므로 D_ 접두사(닷지 전용)는 제외.
		_boostBurstParticles = System.Array.FindAll(_boostBurstParticles, ps => ps != null && !ps.name.StartsWith("D_"));

		// Dodge RCS 캐싱
		_rcsDodge[(int)RCS.RTop] = FindParticlesByName("RCS_Wing_R_Top");
		_rcsDodge[(int)RCS.RBot] = FindParticlesByName("RCS_Wing_R_Bot");
		_rcsDodge[(int)RCS.LTop] = FindParticlesByName("RCS_Wing_L_Top");
		_rcsDodge[(int)RCS.LBot] = FindParticlesByName("RCS_Wing_L_Bot");

		// Roll RCS 캐싱
		_rcsRoll[(int)RCS.RTop] = FindParticlesByName("RCS_Roll_R_Top");
		_rcsRoll[(int)RCS.RBot] = FindParticlesByName("RCS_Roll_R_Bot");
		_rcsRoll[(int)RCS.LTop] = FindParticlesByName("RCS_Roll_L_Top");
		_rcsRoll[(int)RCS.LBot] = FindParticlesByName("RCS_Roll_L_Bot");

		// 역추진 파티클 캐싱
		_reverseMainL = FindParticlesByName("Rev-Booster_L");
		_reverseMainR = FindParticlesByName("Rev-Booster_R");

		_reverseSubs = new ParticleSystem[8];
		for (int i = 0; i < 8; i++)
		{
			_reverseSubs[i] = FindParticleSingle($"Rev-Sub-Booster_{i + 1}");
		}

		// 측면 이동(Strafe) RCS 캐싱 — A키 → 우측(R), D키 → 좌측(L)
		_strafeL = FindParticlesByName("RCS_Strafe_L");
		_strafeR = FindParticlesByName("RCS_Strafe_R");

		// Play On Awake가 켜진 파티클은 시작 시 자동 1회 재생됨 — 첫 조작 때 같이 터져 보이는
		// 문제 방지를 위해 부스트 버스트(fire_3-3)와 역추진 파티클을 초기에 전부 정지/클리어.
		if (_boostBurstParticles != null) foreach (var ps in _boostBurstParticles) ps?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		if (_reverseMainL != null) foreach (var ps in _reverseMainL) ps?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		if (_reverseMainR != null) foreach (var ps in _reverseMainR) ps?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		if (_reverseSubs  != null) foreach (var ps in _reverseSubs)  ps?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		if (_strafeL != null) foreach (var ps in _strafeL) ps?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		if (_strafeR != null) foreach (var ps in _strafeR) ps?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
	}

	// 이름이 일치하는 첫 번째 ParticleSystem 1개만 반환. 없으면 null + 경고 로그.
	private ParticleSystem FindParticleSingle(string targetName)
	{
		foreach (Transform child in GetComponentsInChildren<Transform>(true))
		{
			if (child.name == targetName)
			{
				ParticleSystem ps = child.GetComponentInChildren<ParticleSystem>();
				if (ps == null)
					Debug.LogWarning($"[Player] RCS 파티클 \"{targetName}\" 오브젝트는 있지만 ParticleSystem 컴포넌트 없음");
				return ps;
			}
		}
		Debug.LogWarning($"[Player] RCS 파티클 \"{targetName}\" 을(를) 찾지 못함 — 프리팹 이름 확인 필요");
		return null;
	}
	private void UpdateBoostEffect()
	{
		if (_step1Particles == null)
		{
			return;
		}
		bool isMoving = _input.moveInput.z > 0.001f && curFuelRemaining > 0f;
		bool isBoosting = _isBoosting;

		if (isMoving == _wasMoving && isBoosting == _wasBoosting)
		{
			return;
		}

		SetParticles(_step1Particles, true);
		SetParticles(_step2Particles, isMoving);
		SetParticles(_step3Particles, isBoosting);

		if (isBoosting && !_wasBoosting && curBoostRemaining > minBoostRequired)
		{
			PlayAll(_boostBurstParticles);
		}
		_wasMoving = isMoving;
		_wasBoosting = isBoosting;
	}

	private ParticleSystem[] FindParticlesByNameExclude(string stepName, string excludeName)
	{
		List<ParticleSystem> list = new List<ParticleSystem>();
		Transform[] allChildren = GetComponentsInChildren<Transform>();
		foreach (Transform child in allChildren)
		{
			if (child.name == stepName)
			{
				ParticleSystem[] particles = child.GetComponentsInChildren<ParticleSystem>();
				foreach (var ps in particles)
				{
					if (!ps.name.EndsWith(excludeName))
						list.Add(ps);
				}
			}
		}
		return list.ToArray();
	}

	private ParticleSystem[] FindParticlesByName(string stepName)
	{
		List<ParticleSystem> list = new List<ParticleSystem>();
		Transform[] allChildren = GetComponentsInChildren<Transform>();
		foreach (Transform child in allChildren)
		{
			if (child.name.EndsWith(stepName))
			{
				ParticleSystem[] particles = child.GetComponentsInChildren<ParticleSystem>();
				list.AddRange(particles);
			}
		}
		return list.ToArray();
	}

	// RCS 입력 방향 캐시(-1/0/+1). 값이 안 바뀌면 파티클을 다시 훑지 않기 위함.
	// RcsCacheInvalid는 "캐시를 믿을 수 없음" 표식 — 닷지/브레이크가 파티클을 직접 갈아치운 뒤에 씀.
	private const int RcsCacheInvalid = -99;
	private int _lastRcsRoll = RcsCacheInvalid;
	private int _lastRcsStrafe = RcsCacheInvalid;

	// RCS 파티클 전부 정지. 사망 처리처럼 입력과 무관하게 싹 꺼야 할 때 씀.
	private void StopAllRcs()
	{
		if (_rcsRoll != null)
		{
			foreach (var arr in _rcsRoll)
			{
				StopAll(arr);
			}
		}
		StopAll(_strafeL);
		StopAll(_strafeR);
	}

	// 롤 입력(Q/E)에 따라 대각 RCS 루프 재생·정지
	// E(우롤): 우측 날개 위 + 좌측 날개 아래 → 기체 우측으로 기울어짐
	// Q(좌롤): 우측 날개 아래 + 좌측 날개 위 → 기체 좌측으로 기울어짐
	private void UpdateRcsEffect()
	{
		// 죽으면 분사가 남아있으면 안 되므로 전부 끄고 종료. 캐시도 초기화해서 부활 시 다시 적용되게 함.
		if (CurState == UNIT_STATE.DIE)
		{
			if (_lastRcsRoll != 0 || _lastRcsStrafe != 0)
			{
				StopAllRcs();
				_lastRcsRoll = 0;
				_lastRcsStrafe = 0;
			}
			return;
		}

		// 닷지 중 Roll RCS, 브레이크 중 Strafe RCS는 각각 OnStateEnter/OnStateExit에서 전담 처리 — 여기서 건드리면 충돌남
		if (curState == UNIT_STATE.DODGE || curState == UNIT_STATE.BRAKE)
		{
			// 그 상태들이 파티클을 직접 갈아치우므로, 빠져나온 뒤엔 캐시가 실제 상태와 다름 →
			// 무효화해둬야 다음 프레임에 입력대로 다시 세팅됨(안 하면 같은 입력이라고 판단해 건너뜀).
			_lastRcsRoll = RcsCacheInvalid;
			_lastRcsStrafe = RcsCacheInvalid;
			return;
		}

		float roll = _input.rollInput;
		float strafeInput = _input.moveInput.x;

		// 입력을 -1/0/+1 방향으로만 압축해서 비교 — 값이 그대로면 파티클도 그대로라 훑을 필요가 없음.
		// (Play/Stop 호출 자체는 싸지만 배열 6~8개를 매번 순회하는 게 낭비라 가드로 막음)
		int rollDir = roll > 0.01f ? 1 : (roll < -0.01f ? -1 : 0);
		int strafeDir = strafeInput > 0.01f ? 1 : (strafeInput < -0.01f ? -1 : 0);

		if (rollDir == _lastRcsRoll && strafeDir == _lastRcsStrafe)
		{
			return;
		}
		_lastRcsRoll = rollDir;
		_lastRcsStrafe = strafeDir;

		if (roll > 0.01f)       // E키 → 우측 롤
		{
			PlayAllIfStopped(_rcsRoll[(int)RCS.RTop]); PlayAllIfStopped(_rcsRoll[(int)RCS.LBot]);
			StopAll(_rcsRoll[(int)RCS.RBot]);          StopAll(_rcsRoll[(int)RCS.LTop]);
		}
		else if (roll < -0.01f) // Q키 → 좌측 롤
		{
			PlayAllIfStopped(_rcsRoll[(int)RCS.RBot]); PlayAllIfStopped(_rcsRoll[(int)RCS.LTop]);
			StopAll(_rcsRoll[(int)RCS.RTop]);          StopAll(_rcsRoll[(int)RCS.LBot]);
		}
		else
		{
			foreach (var arr in _rcsRoll) StopAll(arr);
		}

		// 측면 이동(Strafe) 분사 — A키(좌측, x<0) → 우측(R) 분사, D키(우측, x>0) → 좌측(L) 분사
		float strafe = strafeInput;
		if (strafe < -0.01f)        // A키 → 우측 분사
		{
			PlayAllIfStopped(_strafeR);
			StopAll(_strafeL);
		}
		else if (strafe > 0.01f)    // D키 → 좌측 분사
		{
			PlayAllIfStopped(_strafeL);
			StopAll(_strafeR);
		}
		else
		{
			StopAll(_strafeL);
			StopAll(_strafeR);
		}
	}

	// 역추진 보조(Rev-Sub) 파티클 — S키 후진 입력 전용 (FixedUpdate에서 호출).
	// 브레이크(감속) 연출은 더 이상 여기서 안 함 — UNIT_STATE.BRAKE로 분리되어
	// Player.OnStateEnter/OnStateExit(BRAKE)가 메인 버스트 + 보조 루프를 전부 처리함.
	// 이 함수는 BRAKE가 아닐 때(=입력이 있을 때)만 S키 후진 여부로 보조 루프를 단독 제어.
	private void UpdateReverseInputEffect()
	{
		if (_reverseSubs == null || CurState == UNIT_STATE.BRAKE)
		{
			return;
		}

		bool reversing = _input.moveInput.z < -0.01f; // S키 후진
		if (reversing) PlayAllIfStopped(_reverseSubs);
		else StopAll(_reverseSubs);
	}

	private void PlayIfStopped(ParticleSystem ps)
	{
		if (ps != null && !ps.isPlaying) ps.Play();
	}

	private void StopIfPlaying(ParticleSystem ps)
	{
		if (ps != null && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
	}

	private void PlayAll(ParticleSystem[] arr)
	{
		if (arr == null) return;
		foreach (var ps in arr) ps?.Play();
	}

	private void PlayAllIfStopped(ParticleSystem[] arr)
	{
		if (arr == null) return;
		foreach (var ps in arr) PlayIfStopped(ps);
	}

	private void StopAll(ParticleSystem[] arr)
	{
		if (arr == null) return;
		foreach (var ps in arr) StopIfPlaying(ps);
	}

	private void SetParticles(ParticleSystem[] particles, bool shouldPlay)
	{
		foreach (var ps in particles)
		{
			if (ps == null) continue;
			if (shouldPlay && !ps.isPlaying) ps.Play();
			else if (!shouldPlay && ps.isPlaying)
				ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
		}
	}








	// ==================연료 충전==================

	// Unit.RefillToMax + 연료까지 채움. UnitParts가 파츠 스탯보너스(FUEL_MAX 등) 적용 후 호출.
	public override void RefillToMax()
	{
		base.RefillToMax();
		curFuelRemaining = maxFuelCapacity;
	}

	/// <summary>
	/// 연료 부분 충전. 맵 상호작용 오브젝트 등에서 호출.
	/// </summary>
	public void Refuel(float amount)
	{
		curFuelRemaining = Mathf.Min(maxFuelCapacity, curFuelRemaining + amount);
	}

	/// <summary>
	/// 연료 완전 충전. 마을(STATION) 귀환 시 호출.
	/// </summary>
	public void RefuelFull()
	{
		curFuelRemaining = maxFuelCapacity;
	}

	// ==================플레이어 레벨관련================== 차후 수정필요

	//
	// 경험치 획득. 음수 방지 처리 포함.
	// 레벨업 조건 충족 시 LevelUp() 호출.
	// Enemy 사망 시 Enemy.Die()에서 호출 예정.
	//
	public void GainExp(int amount)
	{
		// 음수 방지
		exp += Mathf.Max(0, amount);

		// 레벨업 체크 — while이라 한 번에 여러 레벨치 경험치를 받아도 연속 레벨업 처리됨.
		// (expToNextLevel은 항상 양수라 무한루프 없음)
		while (exp >= expToNextLevel)
			LevelUp();
	}

	//
	// 킬 보상 수령 — 경험치/골드를 이 클라 로컬 스탯에 지급.
	// 골드(InventoryManager)도 클라마다 로컬이라, 죽인 사람 클라에서만 호출되면 플레이어별로 귀속됨.
	// 호출: GameManager.GiveRewardToPlayer(로컬 킬) / RpcReceiveKillReward(원격 킬).
	//
	public void ReceiveKillReward(int expAmount, int goldAmount)
	{
		GainExp(expAmount);
		if (InventoryManager.Instance != null)
		{
			InventoryManager.Instance.gold += goldAmount;
		}
	}

	//
	// 원격 킬 보상 수신 — 적 소유자(방장)가 killer 소유 클라 한 곳으로만 보냄(GameManager.GiveRewardToPlayer).
	//
	[PunRPC]
	public void RpcReceiveKillReward(int expAmount, int goldAmount)
	{
		ReceiveKillReward(expAmount, goldAmount);
	}

	//
	// 적 처치 드랍 아이템 수신. 호출: GameManager.GiveItemDropToKiller(로컬 킬) / RpcReceiveItemDrops(원격 킬).
	//
	// 월드에 픽업이 스폰되는 게 아니라, 여기서 내 로컬 풀에서 연출용 오브젝트를 꺼내 나에게 날린다.
	// 다른 클라에는 이 오브젝트가 존재하지 않는다(연출이라 동기화 불필요).
	// 실제 인벤토리 지급은 연출이 도착하는 시점에 ItemPickupVisual이 처리한다 —
	// 숫자가 먼저 오르고 아이콘이 나중에 도착하면 연출이 겉돌기 때문.
	//
	// <param name="poolTypes">드랍 픽업 프리팹의 풀 타입 목록(POOL_TYPE을 int로 받음 — RPC가 enum을 못 보냄)</param>
	// <param name="center">연출이 출발할 중심 위치(적이 죽은 자리)</param>
	// <param name="spreadRadius">출발 지점을 흩뿌릴 반경</param>
	//
	public void ReceiveItemDrops(int[] poolTypes, Vector3 center, float spreadRadius)
	{
		if (poolTypes == null || PoolManager.Instance == null)
		{
			return;
		}

		for (int i = 0; i < poolTypes.Length; i++)
		{
			// 흩뿌릴 좌표는 각 클라가 알아서 뽑음 — 이 오브젝트는 내 화면에만 있는 연출이라 동기화가 필요 없음
			SpawnItemDropVisual((POOL_TYPE)poolTypes[i], center + Random.insideUnitSphere * spreadRadius);
		}
	}

	// 드랍 1개분 연출 오브젝트를 로컬 풀에서 꺼내 나에게 날림.
	private void SpawnItemDropVisual(POOL_TYPE dropType, Vector3 dropPos)
	{
		GameObject obj = PoolManager.Instance.Get(dropType);
		if (obj == null)
		{
			return;   // 풀 없음 경고는 PoolManager가 이미 냄
		}

		obj.transform.SetPositionAndRotation(dropPos, Quaternion.identity);

		ItemPickupVisual visual = obj.GetComponent<ItemPickupVisual>();
		if (visual != null)
		{
			visual.StartChase(transform);
			return;
		}

		// 연출 컴포넌트가 아직 안 붙은 프리팹 대비 — 아이템을 잃지 않게 즉시 지급하고 오브젝트는 반납.
		// (연출용 오브젝트를 그대로 두면 나만 보이는 유령 픽업이 씬에 남음)
		Debug.LogWarning($"[Player] {dropType} 프리팹에 ItemPickupVisual이 없음 — 연출 없이 즉시 지급함.");
		ItemPickup pickup = obj.GetComponent<ItemPickup>();
		if (pickup != null && InventoryManager.Instance != null)
		{
			InventoryManager.Instance.AddItem(pickup.ItemData, pickup.Amount);
		}
		PoolManager.Instance.Return(obj);
	}

	//
	// 원격 킬 드랍 수신 — 적 소유자(방장)가 killer 소유 클라 한 곳으로만 보냄(GameManager.GiveItemDropToKiller).
	//
	[PunRPC]
	public void RpcReceiveItemDrops(int[] poolTypes, Vector3 center, float spreadRadius)
	{
		ReceiveItemDrops(poolTypes, center, spreadRadius);
	}

	// 
	// 레벨업 처리.
	// 경험치 초과분 이월, 레벨 증가, 다음 레벨 필요 경험치 갱신.
	// 레벨업 시 스탯 증가는 추후 장비/스탯 시스템 구현 후 여기서 처리.
	//
	private void LevelUp()
	{
		// 초과 경험치 이월
		exp = exp - expToNextLevel;
		exp = Mathf.Max(0, exp);

		level++;

		// 다음 레벨 필요 경험치 증가 (예시: 레벨당 50씩 증가. 수치는 추후 조정)
		expToNextLevel += 50;

		Debug.Log($"[Player] 레벨업! 현재 레벨: {level}");

		maxHpRemaining += 50;
		criChance += 5;
		criDamageMultiplier += 0.1f;
		dodgeCoolTime -= 0.2f;

		// 일정 레벨마다 미사일 슬롯 +1 (매 레벨은 과해서 주기로 지급).
		// AddMissileSlot()은 빈 슬롯을 붙이는 것 — 실제 미사일은 인벤토리/상점에서 장착해 채움.
		if (missileSlotGainInterval > 0 && level % missileSlotGainInterval == 0)
		{
			weaponSystem?.AddMissileSlot();
		}

		// HP/실드/아머/부스트 + 파츠 HP까지 최대치로 회복(파츠 스탯 페널티도 함께 해제됨).
		RefillToMax();

		// 레벨업 효과는 여기에 추가 예정:
		//  - 인벤토리 사용 가능 슬롯 증가 (InventoryManager에 슬롯 상한 개념이 생기면 연결)
		// (레벨업 연출/HUD 갱신이 필요하면 이벤트 훅을 여기서 발행)
	}
}


