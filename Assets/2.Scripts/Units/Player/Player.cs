using System.Collections;
using System.Collections.Generic;

using UnityEngine;




// 미사일 발사 모드 선택용 열거형 추가
public enum MISSILE_FIRE_MODE
{
	DOUBLE, // 동시 발사
	SINGLE     // 교대 발사
}
//
// 플레이어 전용 컴포넌트.
// Unit을 상속받아 스탯/FSM/데미지 처리는 Unit에서,
// 플레이어 입력을 받는 이동/회전/사격 입력 처리는 여기서 담당.



public class Player : Unit
{


	// 총알 교대 발사용 인덱스 (0=왼쪽, 1=오른쪽 → 0→1→0 순환)
	// bulletFirePos는 Unit에 있는 배열 그대로 사용
	// missileFirePos, laserFirePos도 Unit 그대로 사용
	private int _bulletFireIndex = 0;
	// 미사일 교대발사용인덱스
	private int _missileFireIndex = 0;
	// =================================================

	//매니저 할당용 레퍼런스
	private InputManager _input;



	

	// ==================회전 감도==================
	[Space(5)]
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

	

	// ==================락온 시스템==================
	[Header("락온 시스템 (인스펙터에서 할당)")]
	public MissileLockOnSystem lockOnSystem;
	[Header("미사일 발사 모드 설정")]
	public MISSILE_FIRE_MODE missileFireMode = MISSILE_FIRE_MODE.DOUBLE;
	[Header("현재 장착된 미사일 타입")]
	public MISSILE_TYPE curMissileType = MISSILE_TYPE.HOMING;

	// 미사일 장착 여부 (잔탄량에 따라 Update에서 자동 갱신됨)
	public bool isMissile_EquippedLeft = false;
	public bool isMissile_EquippedRight = false;

	[Header("미사일 장비 슬롯 (1, 2, 3번 키에 대응)")]
	[Tooltip("인벤토리나 장비창에서 장착한 미사일 타입들을 배열에 넣어줌")]
	public MISSILE_TYPE[] equippedMissiles = new MISSILE_TYPE[3]
	{
		MISSILE_TYPE.HOMING,
		MISSILE_TYPE.CLUSTER,
		MISSILE_TYPE.DUMB
	};

	protected override void Awake()
	{
		base.Awake();
		// 우주 공간 = 중력 없음. 회전은 직접 제어하므로 물리 회전 고정
		_rb.useGravity = false;
		_rb.freezeRotation = true;
	}
	// Start is called before the first frame update
	protected override void Start()
	{
		base.Start(); //유닛 초기화 호출
		_input = InputManager.Instance;
		// 게임 시작 시 1번 슬롯 무기로 초기화
		if (equippedMissiles.Length > 0)
		{
			curMissileType = equippedMissiles[0];
		}
	}

	// Update is called once per frame
	protected override void Update()
	{
		base.Update(); //FSM, 실드/부스트 회복 호출
					   // 입력처리 - InputManager 구현 뒤 여기서 호출
					   // ex. InputManager.Instance.HandleInput(this);
		
		if (_input == null)
		{
			return;
		}
		//==========혹여나 업뎃이 입력없을때도 필요한게ㅐ 있으면 이 위로 입력학ㄹ것=========
		//미사일 장착 토글 처리(온오프) 상태변화 관련이므로 즉시 Update로
		// 장착 슬롯 배열의 길이를 확인하여 에러 방지 후 현재 미사일 타입 변경
		if (_input.switchMissile1 && equippedMissiles.Length > 0)
		{
			curMissileType = equippedMissiles[0];
			Debug.Log($"[Player] 1번 슬롯 무기 장착: {curMissileType}");
		}
		else if (_input.switchMissile2 && equippedMissiles.Length > 1)
		{
			curMissileType = equippedMissiles[1];
			Debug.Log($"[Player] 2번 슬롯 무기 장착: {curMissileType}");
		}
		else if (_input.switchMissile3 && equippedMissiles.Length > 2)
		{
			curMissileType = equippedMissiles[2];
			Debug.Log($"[Player] 3번 슬롯 무기 장착: {curMissileType}");
		}

		if (_input.switchLockOnTarget != 0f && lockOnSystem != null)
		{
			lockOnSystem.SwitchTarget(_input.switchLockOnTarget > 0 ? 1 : -1);
		}

		if(_input.switchMissileShootMode)
		{
			missileFireMode = (missileFireMode == MISSILE_FIRE_MODE.DOUBLE) ? MISSILE_FIRE_MODE.SINGLE : MISSILE_FIRE_MODE.DOUBLE;

			Debug.Log($"[Player] 미사일 발사 모드 변경: {missileFireMode}");
		}
		// 매 프레임 잔탄을 체크하여 좌우 장착 여부 갱신
		UpdateMissileEquipStatus();
		ShootByInput();
	}

	protected override void FixedUpdate()
	{

		//항상 회전이먼저!!!
		RotateByInput();
		MovingByInput();

	}


	// ==================================상태 업데이트 관련=======================
	/// <summary>
	/// 잔탄과 발사 모드에 따라 좌/우 총구의 활성화 상태(isMissile_Equipped)를 갱신
	/// 기존 Shoot() 메서드의 if문을 제어하는 스위치 역할
	/// </summary>
	private void UpdateMissileEquipStatus()
	{
		MissileAmmoInfo info = missileAmmoList.Find(x => x.missileType == curMissileType);
		int ammo = info != null ? info.curAmmo : 0;

		// 잔탄이 0이면 무조건 둘 다 비활성화하여 Shoot() 내부의 if문을 통과하지 못하게 차단
		if (ammo <= 0)
		{
			isMissile_EquippedLeft = false;
			isMissile_EquippedRight = false;
			return;
		}

		if (missileFireMode == MISSILE_FIRE_MODE.DOUBLE)
		{
			// [동시 발사] 1발 남았으면 왼쪽만, 2발 이상이면 양쪽 다 활성화
			isMissile_EquippedLeft = ammo > 0;
			isMissile_EquippedRight = ammo > 1;
		}
		else if (missileFireMode == MISSILE_FIRE_MODE.SINGLE)
		{
			// [교대 발사] 잔탄이 1발일 때는 강제로 왼쪽 총구로 고정하여 발사 보장
			if (ammo == 1)
			{
				isMissile_EquippedLeft = true;
				isMissile_EquippedRight = false;
				_missileFireIndex = 0; // 다음번을 위해 동기화
			}
			else
			{
				// 인덱스에 맞춰 한 쪽만 활성화하여 기존 Shoot()의 if문 중 하나만 통과하게 유도
				isMissile_EquippedLeft = (_missileFireIndex == 0);
				isMissile_EquippedRight = (_missileFireIndex == 1);
			}
		}
	}


	//===============override 메서드 FSM==================
	protected override void OnStateEnter(UNIT_STATE state)
	{
		switch (state)
		{
			case UNIT_STATE.DIE:
				Debug.Log("[Player] 사망");
				// GameManager.Instance.OnPlayerDie();
				break;
		}
	}
	protected override void OnStateExit(UNIT_STATE state) { }
	protected override void OnIdle() { }
	protected override void OnMoving() { }
	protected override void OnDodge() { }
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
		// ex. GameManager.Instance.OnPlayerDie();
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
		if (_input.fireBullet && Time.time >= lastFireTime + fireDelay)//딜레이
		{
			lastFireTime = Time.time;
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
	// Yaw만 World 기준인 이유: 완전 뒤집혔을 때도 마우스 좌우가 자연스럽게 동작.




	private void RotateByInput()
	{
		float yaw = _input.lookInput.x * xSensitivity * Time.fixedDeltaTime;
		float pitch = -_input.lookInput.y * ySensitivity * Time.fixedDeltaTime;
		// lookInput.y 반전: 마우스 위로 올리면 기수가 올라가야 하므로
		float roll = -_input.rollInput * rollSensitivity * Time.fixedDeltaTime;
		// rollInput 반전: E키 눌렀을 때 오른쪽으로 기우는 방향

		transform.Rotate(Vector3.up, yaw, Space.World);
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

		// 부스트 조건: Shift 누름 + 잔량 남아있음
		bool canBoost = _input.isBoosting && curBoostRemaining > 0f;

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
			// 입력이 있을 때 해당 방향으로 가속
			_rb.AddForce(dir * finalForce, ForceMode.Acceleration);
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

		// FSM 상태 전환
		if (_input.isDodging)
		{
			CurState = UNIT_STATE.DODGE;
		}
		else if (isMoving)
		{
			CurState = UNIT_STATE.MOVING;
		}
		else
		{
			CurState = UNIT_STATE.IDLE;
		}

	}

	//==============부스트이펙트====================(이동에서같이)



	private void UpdateBoostEffect()
	{

	}

	//===========================인풋끝=================
	/// <summary>
	/// 사격및 소리재생
	/// </summary>
	/// <param name="type"></param>
	/// 

	//=====================실제 InputManager에서 받아오면 작동할 동작명령구현부================
	public override void Shoot(PROJECTILE_TYPE type)
	{

		//혹여나 버그걸릴시 다시 매니저 직접인스턴스할것. 스타트속도등으로 버그날수있따함.
		_playSoundType = GetPlaySoundType(type);

		switch (type)
		{
			case PROJECTILE_TYPE.BULLET:
				_sound.PlaySFX3DAtPosition(_playSoundType, transform.position, 0.7f, 1.2f);//총알소리 살짝랜덤하게
				ShootBullet();
				break;
			case PROJECTILE_TYPE.LASER:
				_sound.PlaySFX3DAtPosition(_playSoundType, transform.position);
				ShootLaser();
				break;
			case PROJECTILE_TYPE.MISSILE://소리 너무 크면 가운데서 실행되게 아래로 빼기.
				if (isMissile_EquippedLeft)
				{
					_sound.PlaySFX3DAtPosition(_playSoundType, transform.position);
					ShootMissile(FIREPOS_TYPE.MISSILE_LEFT);
				}
				if (isMissile_EquippedRight)
				{
					_sound.PlaySFX3DAtPosition(_playSoundType, transform.position);
					ShootMissile(FIREPOS_TYPE.MISSILE_RIGHT);
				}
					
				break;

		}
	}





	// 
	// 총알 - 좌우 교대 발사
	// bulletFirePos[0]=왼쪽, bulletFirePos[1]=오른쪽
	// =================================================






	/// <summary>
	/// Shoot 메서드에있는  사격시 사용메서드
	/// </summary>
	//총알
	private void ShootBullet()
	{
		FIREPOS_TYPE[] bulletTypes = { FIREPOS_TYPE.BULLET_LEFT, FIREPOS_TYPE.BULLET_RIGHT };
		Transform curFirePos = GetFirePos(bulletTypes[_bulletFireIndex]);
		if (curFirePos == null)
		{
			return;
		}
		_bulletFireIndex = (_bulletFireIndex + 1) % bulletTypes.Length; // 좌우 순환

		//발사로직필요// poolmanager 구현 뒤 넣기
		//ex.PoolManager.Instance.GetBullet(curFirePos.position, curFirePos.forward, this);
		Bullet newBullet = _pool.GetBullet();
		newBullet.Init(curFirePos.position, curFirePos.forward, this);

	}

	// 미사일 - 타입으로 좌우선택. 총구타입선택(좌우)
	/// <summary>
	/// Shoot()에서 조건문을 통과했을 때 호출되며 실제 생성과 잔탄 소모만 담당
	/// </summary>
	private void ShootMissile(FIREPOS_TYPE firePosType)
	{
		Transform curFirePos = GetFirePos(firePosType);
		if (curFirePos == null) return;

		// Shoot()에서 이미 장착 여부(if)를 통과하고 들어왔으므로 여기서 즉시 1발 소모
		RemoveMissileAmmo(curMissileType);

		// 교대 모드 시, 잔탄이 부족하여 오른쪽 발사가 불가능하다면 인덱스를 다시 0으로 강제 보정
		if (missileFireMode == MISSILE_FIRE_MODE.SINGLE && !isMissile_EquippedRight)
		{
			_missileFireIndex = 0;
		}
		// 교대 모드일 경우 다음 발사를 위해 인덱스 전환 (0 -> 1 -> 0)
		if (missileFireMode == MISSILE_FIRE_MODE.SINGLE)
		{
			_missileFireIndex = (_missileFireIndex + 1) % 2;
		}

		// 투사체 생성 및 초기화 로직
		switch (curMissileType)
		{
			case MISSILE_TYPE.HOMING:
				Missile newMissile = _pool.GetMissile();
				if (lockOnSystem != null && lockOnSystem.IsLocked)
				{
					newMissile.Init(curFirePos.position, curFirePos.forward, this, lockOnSystem.LockedTarget);
				}
				else
				{
					newMissile.Init(curFirePos.position, curFirePos.forward, this);
				}
				break;

			case MISSILE_TYPE.CLUSTER:
				// ClusterMissile cm = _pool.GetClusterMissile();
				// cm.Init(curFirePos.position, curFirePos.forward, this);
				break;

			case MISSILE_TYPE.DUMB:
				// DumbMissile dm = _pool.GetDumbMissile();
				// dm.Init(curFirePos.position, curFirePos.forward, this);
				break;
		}
	}


	// 레이저 - 머리 중앙 고정 (laserFirePos 단일 Transform)

	private void ShootLaser()
	{

		Transform curFirePos = GetFirePos(FIREPOS_TYPE.LASER);
		if (curFirePos == null)
		{
			return;
		}

		//발사로직필요// poolmanager 구현 뒤 넣기
		//ex.PoolManager.Instance.GetLaser(curFirePos.position, curFirePos.forward, this);
		Laser newLaser = _pool.GetLaser();
		newLaser.Init(curFirePos.position, curFirePos.forward, this);

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
