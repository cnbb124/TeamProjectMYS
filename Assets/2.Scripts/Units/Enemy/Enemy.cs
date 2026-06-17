using UnityEngine;


// ================================================================
// [Enemy 작동 구조]
// ================================================================
// Update()
//   └── UpdateAI()                              AI_STATE 전환 판단 + UNIT_STATE 동기화
//         ├── OnAIStandby()                     빈 상태 (자식 override용)
//         ├── OnAIPatrol()                      detectRange 체크 → CHASE 전환 / 순찰지점 갱신
//         ├── OnAIChase()                       detectRange/공격범위 체크 → PATROL/ATTACK 전환
//         └── OnAIAttack()                      detectRange/공격범위 체크 → PATROL/CHASE 전환
//
// FixedUpdate()
//   └── switch(aiState)
//         ├── PATROL  : RotateTowardPosition(patrolTarget) + MoveTowardPosition(patrolTarget)
//         ├── CHASE   : RotateTowardTarget()               + MoveTowardTarget()
//         └── ATTACK  : RotateTowardTarget()               + MoveTowardTarget()
//
// ================================================================

// 사용함수
// ================================================================
// IsTargetInRange(float range)              단순 거리 비교
// HasTargetInAttackRange()                  LockOnSystem.TargetsInLockonRange 수 체크
// PickNewPatrolPoint()                      spawnPosition 기준 랜덤 순찰 지점 선정
// RotateTowardTarget()                      target.position → RotateTowardPosition 위임
// RotateTowardPosition(Vector3 worldPos)    Slerp 회전
// MoveTowardTarget()                        target.position → MoveTowardPosition 위임
// MoveTowardPosition(Vector3 worldPos)      AddForce + 최대속도 클램프
// ================================================================

public class Enemy : Unit
{
	// AI 행동 상태. Unit.CurState(UNIT_STATE)와 별개로, "무엇을 할지"를 결정하는 상태.
	// STANDBY(정지) 외에는 전부 이동하므로, UpdateAI()에서 CurState(UNIT_STATE)로 자동 매핑됨: STANDBY->IDLE, 나머지->MOVING.
	public enum AI_STATE { STANDBY, PATROL, CHASE, ATTACK }

	[Header("<size=18>Enemy AI 설정</size>")]
	[Tooltip("이 범위 안에 타겟이 들어오면 추격 시작. 공격 가능 범위는 WeaponSystem.lockOnSystem.lockOnRange 사용.")]
	public float detectRange = 500f;
	[Tooltip("타겟을 향한 회전 속도")]
	public float rotateSpeed = 3f;

	[Header("순찰 설정")]
	[Tooltip("스폰 위치 기준 순찰 반경")]
	public float patrolRadius = 500f;
	[Tooltip("순찰 지점 도착 판정 거리")]
	public float patrolArriveDist = 2f;

	[Header("AI 상태 (디버그용)")]
	public AI_STATE aiState = AI_STATE.PATROL;

	protected Transform target;
	protected Vector3 spawnPosition;
	protected Vector3 patrolTarget;

	// Enemy의 범용 전투/순찰 AI 사용 여부. EnemyWorker처럼 자체 AI를 쓰는 자식은 false로 override.
	protected virtual bool UseGenericAI => true;

	// Start is called before the first frame update
	protected override void Start()
	{
		base.Start();

		target = GameObject.FindWithTag("Player")?.transform;

		spawnPosition = transform.position;
		PickNewPatrolPoint();
	}

	// Update is called once per frame
	protected override void Update()
	{
		base.Update();

		if (ShouldPause || CurState == UNIT_STATE.DIE || !UseGenericAI)
		{
			return;
		}

		UpdateAI();
	}

	protected override void FixedUpdate()
	{
		base.FixedUpdate();

		if (ShouldPause || CurState == UNIT_STATE.DIE || !UseGenericAI)
		{
			return;
		}
		_rb.angularVelocity = Vector3.zero;
		switch (aiState)
		{
			case AI_STATE.STANDBY:
				break;

			case AI_STATE.PATROL:
				RotateTowardPosition(patrolTarget);
				MoveTowardPosition(patrolTarget);
				break;

			case AI_STATE.CHASE:
				RotateTowardTarget();
				MoveTowardTarget();
				break;

			case AI_STATE.ATTACK:
				RotateTowardTarget();
				MoveTowardTarget();
				break;
		}
	}

	//===============AI 상태머신 (자식에서 override해 디테일 구현)==================
	protected virtual void UpdateAI()
	{
		// AI_STATE -> UNIT_STATE 자동 동기화. DODGE/DIE 중에는 Unit FSM이 자체적으로 처리하므로 건드리지 않음.
		if (curState != UNIT_STATE.DODGE && curState != UNIT_STATE.DIE)
		{
			CurState = (aiState == AI_STATE.STANDBY) ? UNIT_STATE.IDLE : UNIT_STATE.MOVING;
		}

		switch (aiState)
		{
			case AI_STATE.STANDBY:
				OnAIStandby();
				break;

			case AI_STATE.PATROL:
				OnAIPatrol();
				break;

			case AI_STATE.CHASE:
				OnAIChase();
				break;

			case AI_STATE.ATTACK:
				OnAIAttack();
				break;
		}
	}

	// 진짜 정지 상태. 현재는 사용 안 함 (자식에서 필요시 진입/탈출 조건 구현).
	protected virtual void OnAIStandby()
	{
	}

	protected virtual void OnAIPatrol()
	{
		if (IsTargetInRange(detectRange))
		{
			aiState = AI_STATE.CHASE;
			return;
		}

		// 순찰 지점에 도착했으면 새 순찰 지점 선정
		if (Vector3.Distance(transform.position, patrolTarget) <= patrolArriveDist)
		{
			PickNewPatrolPoint();
		}
	}

	protected virtual void OnAIChase()
	{
		if (!IsTargetInRange(detectRange))
		{
			aiState = AI_STATE.PATROL;
			return;
		}

		if (HasTargetInAttackRange())
		{
			aiState = AI_STATE.ATTACK;
		}
	}

	protected virtual void OnAIAttack()
	{
		if (!IsTargetInRange(detectRange))
		{
			aiState = AI_STATE.PATROL;
			return;
		}

		if (!HasTargetInAttackRange())
		{
			aiState = AI_STATE.CHASE;
			return;
		}

		
	}

	//===============공통 헬퍼==================

	// 타겟이 detectRange 안에 있는지 (단순 거리 체크)
	protected bool IsTargetInRange(float range)
	{
		if (target == null)
		{
			return false;
		}

		return Vector3.Distance(transform.position, target.position) <= range;
	}

	// LockOnSystem 탐지범위+각도 안에 타겟이 들어왔는지 (딜레이 없는 즉시 판정, 공격범위로 사용)
	protected bool HasTargetInAttackRange()
	{
		if (weaponSystem == null || weaponSystem.lockOnSystem == null)
		{
			return false;
		}

		return weaponSystem.lockOnSystem.TargetsInLockonRange.Count > 0;
	}

	// 스폰 위치 기준 patrolRadius 안의 랜덤 지점을 새 순찰 목표로 선정
	protected void PickNewPatrolPoint()
	{
		Vector2 rand = Random.insideUnitCircle * patrolRadius;
		patrolTarget = spawnPosition + new Vector3(rand.x, 0f, rand.y);
	}

	// 타겟 방향으로 부드럽게 회전
	protected virtual void RotateTowardTarget()
	{
		if (target == null)
		{
			return;
		}

		RotateTowardPosition(target.position);
	}

	// 타겟 방향으로 추진. Player.MovingByInput()과 동일한 가속/최대속도 공식 사용.
	protected void MoveTowardTarget()
	{
		if (target == null)
		{
			return;
		}

		MoveTowardPosition(target.position);
	}

	// 월드 위치 방향으로 부드럽게 회전
	protected void RotateTowardPosition(Vector3 worldPos)
	{
		Vector3 dir = (worldPos - transform.position).normalized;
		if (dir != Vector3.zero)
		{
			transform.rotation = Quaternion.Slerp(transform.rotation,
				Quaternion.LookRotation(dir), rotateSpeed * Time.fixedDeltaTime);
		}
	}

	// 월드 위치 방향으로 추진. Player.MovingByInput()과 동일한 가속/최대속도 공식 사용.
	protected void MoveTowardPosition(Vector3 worldPos)
	{
		Vector3 dir = (worldPos - transform.position).normalized;
		float multiplier = speedMultiPlier > 0f ? speedMultiPlier : 1f;
		float finalForce = baseMoveSpeed * multiplier;

		_rb.AddForce(dir * finalForce, ForceMode.Acceleration);

		if (_rb.velocity.magnitude > maxSpeed)
		{
			_rb.velocity = _rb.velocity.normalized * maxSpeed;
		}
	}
}
