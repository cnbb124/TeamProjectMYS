using System.Collections;
using System.Collections.Generic;

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
//   예시)
//   float expRatio = (float)player.exp / player.expToNextLevel;
//   hudManager.SetLevel(player.level);
//
// ▶ 저장/로드 시스템 참조용
//   GameManager.CollectSaveData()에서 직접 읽어감 (별도 호출 불필요)
//   GameManager.Instance.playerRef 로 접근
// ================================================================

//
// 플레이어 전용 컴포넌트.
// Unit을 상속받아 스탯/FSM/데미지 처리는 Unit에서,
// 플레이어 입력을 받는 이동/회전/사격 입력 처리는 여기서 담당.



public class Player : Unit
{


	// 총알 교대 발사용 인덱스 (0=왼쪽, 1=오른쪽 → 0→1→0 순환)
	// bulletFirePos는 Unit에 있는 배열 그대로 사용
	// missileFirePos, laserFirePos도 Unit 그대로 사용
	// =================================================

	// 추진기 파티클
	private ParticleSystem[] _step1Particles;
	private ParticleSystem[] _step2Particles;
	private ParticleSystem[] _step3Particles;
	private bool _wasBoosting = false;
	private bool _wasMoving = false;
	private ParticleSystem _boostBurstParticle;

	//매니저 할당용 레퍼런스
	private InputManager _input;

	public QuickSlot quickSlot { get; private set; }





	// ==================회전 감도==================
	[Header("")]
	[Space(10)]
	[Header("<size=18>플레이어 설정<size>")]
	[Header("마우스 감도")]
	[Tooltip("마우스 좌우 회전(Yaw) 감도")]
	public float xSensitivity = 120f;

	[Tooltip("마우스 상하 회전(Pitch) 감도")]
	public float ySensitivity = 120f;

	[Tooltip("Q/E 롤(Z축 회전) 감도")]
	public float rollSensitivity = 120f;


	// ==================플레이어용==================
	[Header("경험치/레벨")]
	[Tooltip("현재 레벨. 최소 1")]
	public int level = 1;//차후 mathf.max치로 조정

	[Tooltip("현재 보유 경험치. 음수 불가")]
	public int exp = 0;//차후 mathf.max치로 조정

	[Tooltip("현재 레벨에서 다음 레벨까지 필요한 경험치")]
	public int expToNextLevel = 100;



	[Header("연료 소모량 입력")]
	[Tooltip("이동 시 초당 연료 소모량")]
	public float fuelMoveConsumeRate = 5f;
	[Tooltip("부스트 사용 시 추가 초당 연료 소모량")]
	public float fuelBoostConsumeRate = 10f;
	[Header("연료 최대량&잔량(입력x 참고용)")]
	[Tooltip("최대 연료량.HUD연결용. ENGINE 파츠 FUEL_MAX 스탯으로 결정.")]
	public float maxFuelCapacity = 0f;
	[Tooltip("현재 연료 잔량 HUD 연결용")]
	public float curFuelRemaining;
	



	// ==================락온 시스템==================




	[Header("회피")]
	[Tooltip("회피 시 가해지는 순간 힘")]
	public float dodgeForce = 800f;
	private Vector3 _dodgeDir;


	protected override void Awake()
	{
		base.Awake();
		// 우주 공간 = 중력 없음. 회전은 직접 제어하므로 물리 회전 고정
		_rb.useGravity = false;
		_rb.freezeRotation = true;
		quickSlot = GetComponent<QuickSlot>();
	}
	// Start is called before the first frame update
	protected override void Start()
	{
		base.Start(); //유닛 초기화 호출
		curFuelRemaining = maxFuelCapacity;
		_input = InputManager.Instance;
		// 게임 시작 시 1번 슬롯 무기로 초기화
		weaponSystem.Init();
		if (InventoryManager.Instance != null)
		{
			InventoryManager.Instance.RegisterPlayer(this);
		}

		StartCoroutine(InitParticlesNextFrame());
	}

	private IEnumerator InitParticlesNextFrame()
	{
		yield return null;
		_step1Particles = FindParticlesByName("Step1_Slow");
		_step2Particles = FindParticlesByName("Step2_Normal");
		_step3Particles = FindParticlesByNameExclude("Step3_Boost", "fire_3-3");

		Transform[] allChildren = GetComponentsInChildren<Transform>();
		foreach (Transform child in allChildren)
		{
			if (child.name == "fire_3-3")
			{
				_boostBurstParticle = child.GetComponent<ParticleSystem>();
				break;
			}
		}
	}

	// Update is called once per frame
	protected override void Update()
	{
		if (ShouldPause) return;
		base.Update(); //FSM, 실드/부스트 회복 호출
					   // 입력처리 - InputManager 구현 뒤 여기서 호출
					   // ex. InputManager.Instance.HandleInput(this);

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
			weaponSystem.lockOnSystem.SwitchTarget(_input.switchLockOnTarget > 0 ? 1 : -1);
		}

		// 발사 모드 토글 제거 — 발사 수는 firePositions.Count와 curAmmo로 자동 결정
		ShootByInput();

		// 회피 입력 — GetKeyDown은 Update에서만 안정적으로 감지됨 (FixedUpdate에서 씹힘)
		if (curState != UNIT_STATE.DODGE && _input.isDodging)
		{
			CurState = UNIT_STATE.DODGE;
		}
	}

	protected override void FixedUpdate()
	{
		if (ShouldPause) return;

		//항상 회전이먼저!!!
		RotateByInput();
		MovingByInput();
		UpdateBoostEffect();

	}


	//===============override 메서드 FSM==================
	protected override void OnStateEnter(UNIT_STATE state)
	{
		base.OnStateEnter(state);
		switch (state)
		{
			case UNIT_STATE.DODGE:
				if (_input != null && _input.moveInput.magnitude > 0.1f)
				{
					_dodgeDir = transform.forward * _input.moveInput.z
							  + transform.right * _input.moveInput.x
							  + transform.up * _input.moveInput.y;
					_dodgeDir.Normalize();
				}
				else
				{
					_dodgeDir = transform.forward;
				}
				_rb.AddForce(_dodgeDir * dodgeForce, ForceMode.Impulse);
				break;
			case UNIT_STATE.DIE:
				Debug.Log("[Player] 사망");

				break;
		}
	}
	protected override void OnStateExit(UNIT_STATE state) { }
	protected override void OnIdle() { }
	protected override void OnMoving() { }
	protected override void OnDodge() { base.OnDodge(); }
	protected override void OnDying() { }

	// 피격 반동 - 카메라 쉐이크, 넉백 등
	protected override void OnHitReaction(DamageInfo info)
	{
		// 크리티컬이면 강한 쉐이크
		// ex. if (info.isCritical) CameraShake.Strong(); else CameraShake.Light();
	}

	// 사망처리
	protected override void Die()
	{
		GameManager.Instance.GameOver();
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
		//레이저발사입력
		if (_input.fireLaser)
		{
			Shoot(PROJECTILE_TYPE.LASER);
		}
		//전체발사(총알제외.총알은 좌클릭으로유지)입력
		if (_input.fireAll)
		{
			Shoot(PROJECTILE_TYPE.MISSILE);
			Shoot(PROJECTILE_TYPE.LASER);
		}
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


	// 마우스/키보드 입력으로 오브젝트 자체를 3축 회전.
	// Rigidbody.freezeRotation = true이므로 transform.Rotate 직접 사용.
	//
	// Yaw  (Y축): 마우스 X → 좌우 회전. Space.World 기준
	// Pitch(X축): 마우스 Y → 상하 회전. Space.Self 기준
	// Roll (Z축): Q/E      → 좌우 스핀. Space.Self 기준
	// 
	// Space.Self 사용 이유: 어느 방향 바라봐도 직관적으로 상하/롤 회전됨.





	private void RotateByInput()
	{
		float yaw = _input.lookInput.x * xSensitivity * Time.fixedDeltaTime;
		float pitch = -_input.lookInput.y * ySensitivity * Time.fixedDeltaTime;
		// lookInput.y 반전: 마우스 위로 올리면 기수가 올라가야 하므로
		float roll = -_input.rollInput * rollSensitivity * Time.fixedDeltaTime;
		// rollInput 반전: E키 눌렀을 때 오른쪽으로 기우는 방향

		transform.Rotate(transform.up, yaw, Space.World);
		transform.Rotate(Vector3.right, pitch, Space.Self);
		transform.Rotate(Vector3.forward, roll, Space.Self);
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
		// 로컬 축 기준 6방향 합산
		Vector3 dir =
			transform.forward * _input.moveInput.z +
			transform.right * _input.moveInput.x +
			transform.up * _input.moveInput.y;

		// 대각선 이동 시 속도 튀는 것 방지
		if (dir.magnitude > 1f)
		{
			dir.Normalize();
		}


		//float 오차 패딩값
		bool isMoving = dir.sqrMagnitude > 0.001f;

		// 부스트 조건: Shift 누름 + 잔량 남아있음 + 전진
		bool canBoost = _input.isBoosting && curBoostRemaining > minBoostRequired && _input.moveInput.z > 0;
		_isBoosting = canBoost;

		if (canBoost)
		{
			// Unit.UseBoost(): 잔량 감소 + 회복 타이머 초기화
			UseBoost(20f * Time.fixedDeltaTime);
		}

		float speed = canBoost ? boostSpeed : baseMoveSpeed;
		// speedMultiPlier: 피격/HP에 따른 속도 감소용. 0이면 1배율 적용
		float multiplier = speedMultiPlier > 0f ? speedMultiPlier : 1f;

		// 최종 가해질 힘의 크기 계산
		float finalForce = speed * multiplier;

		// AddForce를 이용한 물리 기반 가속 및 역분사 제어
		if (isMoving)
		{
			if (curFuelRemaining > 0f)
			{
				// 연료 소모 (이동 기본 + 부스트 중이면 추가)
				float fuelCost = fuelMoveConsumeRate;
				if (canBoost)
				{
					fuelCost += fuelBoostConsumeRate;
				}
				curFuelRemaining = Mathf.Max(0f, curFuelRemaining - fuelCost * Time.fixedDeltaTime);

				// 입력이 있을 때 해당 방향으로 가속
				_rb.AddForce(dir * finalForce, ForceMode.Acceleration);
			}
			// 연료 없으면 추진력 없음 (관성은 유지)
		}
		else
		{
			// 방향키 입력이 없지만, 우주선의 물리적 속도가 남아있어 미끄러지는 중일 때
			if (_rb.velocity.sqrMagnitude > 0.1f)
			{
				// 역분사 이펙트 활성화 및 애니메이션 트리거 로직을 작성
				//ex PlayReverseThrusterEffect();
				// ex anim.SetBool("isReverseThrusting", true);
			}
			else
			{
				//우주선이 완전히 정지했을 때 역분사 이펙트 끄기
				// ex StopReverseThrusterEffect();
				// ex anim.SetBool("isReverseThrusting", false);
			}
		}

		// maxSpeed 클램프 (초과 시 방향 유지하고 크기만 제한)
		float maxV = maxSpeed;
		if (_rb.velocity.magnitude > maxV)
		{
			_rb.velocity = _rb.velocity.normalized * maxV;
		}

		// FSM 상태 전환 (MOVING / IDLE — 물리 기반이므로 FixedUpdate에서)
		// DODGE 전환은 GetKeyDown 특성상 Update()에서 처리
		if (curState == UNIT_STATE.DODGE)
		{
			// 회피 중 — Unit.OnDodge()의 타이머가 IDLE 복귀를 담당
		}
		else if (isMoving)
		{
			CurState = UNIT_STATE.MOVING;
		}
		else
		{
			// 입력 없음 — 관성 드리프트 중이면 현재 상태 유지, 거의 정지 시 IDLE
			if (_rb.velocity.sqrMagnitude <= 0.1f)
			{
				CurState = UNIT_STATE.IDLE;
			}
		}

	}

	//==============부스트이펙트====================(이동에서같이)

	private void UpdateBoostEffect()
	{
		if (_step1Particles == null) return;
		bool isMoving = _input.moveInput.z > 0.001f;
		bool isBoosting = _isBoosting;

		if (isMoving == _wasMoving && isBoosting == _wasBoosting) return;

		SetParticles(_step1Particles, true);
		SetParticles(_step2Particles, isMoving);
		SetParticles(_step3Particles, isBoosting);

		if (isBoosting && !_wasBoosting && curBoostRemaining > minBoostRequired)
		{
			if (_boostBurstParticle != null)
			{
				_boostBurstParticle.Play();
			}
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
					if (ps.name != excludeName)
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
			if (child.name == stepName)
			{
				ParticleSystem[] particles = child.GetComponentsInChildren<ParticleSystem>();
				list.AddRange(particles);
			}
		}
		return list.ToArray();
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



	// 
	// 총알 - 좌우 교대 발사
	// bulletFirePos[0]=왼쪽, bulletFirePos[1]=오른쪽




	// ==================연료 충전==================

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

		// 레벨업 체크
		if (exp >= expToNextLevel)
			LevelUp();
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

		// 레벨업 시 스탯 증가 예정
		// OnLevelUp();
	}
}


