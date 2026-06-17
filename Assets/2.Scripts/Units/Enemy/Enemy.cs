using UnityEngine;


// ================================================================
// [Enemy 작동 구조]
// ================================================================
// Update()
//   └── UpdateAI()                              AI_STATE 전환 판단 + UNIT_STATE 동기화
//         ├── OnAIStandby()                     빈 상태 (자식 override용)
//         ├── OnAIPatrol()                      detectRange 체크 → CHASE 전환 / 순찰지점 갱신
//         ├── OnAIChase()                       detectRange/공격범위 체크 → PATROL/PickCombatPattern 전환
//         ├── OnAIAttack()                      detectRange/공격범위 체크 → PATROL/CHASE 전환 (모든 가중치 0일 때 진입)
//         ├── OnAIAttackPass()                  dot product로 "지나쳤다" 판정 → EnterReposition
//         ├── OnAIReposition()                  재배치 목표 도착 판정 → PickCombatPattern
//         ├── OnAIEvade()                       타이머 만료 → CHASE 또는 PickCombatPattern
//         └── OnAIDodge()                       UNIT_STATE.DODGE 종료 감지 → _prevAiState 복귀
//
// FixedUpdate()
//   └── switch(aiState)
//         ├── PATROL      : RotateTowardPosition(patrolTarget) + MoveTowardPosition(patrolTarget)
//         ├── CHASE       : RotateTowardTarget() + MoveTowardTarget()
//         ├── ATTACK      : RotateTowardTarget() + MoveTowardTarget()
//         ├── ATTACK_PASS : RotateTowardTarget() + MoveTowardTarget() (지나쳤다 판정은 UpdateAI에서)
//         ├── REPOSITION  : RotateTowardPosition(_repositionTarget) + MoveTowardPosition(_repositionTarget)
//         ├── EVADE       : 플레이어 반대 방향으로 MoveTowardPosition
//         └── DODGE       : 진입 시 dodgeForce Impulse만 적용, 이후 별도 이동 없음
//
// TakeDamage() override
//   ├── DODGE 판정: dodgeProbability + _dodgeProbCoolTimer → 성공 시 데미지 무효화
//   └── EVADE 판정: evadeChance + _evadeCoolTimer → 성공 시 도망 시작 (데미지는 받음)
//
// ================================================================

// 사용 함수
// ================================================================
// IsTargetInRange(float range)              단순 거리 비교
// HasTargetInAttackRange()                  LockOnSystem.TargetsInLockonRange 수 체크
// PickNewPatrolPoint()                      spawnPosition 기준 랜덤 순찰 지점 선정
// PickCombatPattern()                       가중치 기반 전투 패턴 선택 (ATTACK_PASS/REPOSITION/EVADE)
// EnterAttackPass()                         ATTACK_PASS 진입
// EnterReposition()                         REPOSITION 진입 + _repositionTarget 선정
// EnterEvade()                              EVADE 진입 + 쿨타임 세팅
// RotateTowardTarget()                      target.position → RotateTowardPosition 위임
// RotateTowardPosition(Vector3 worldPos)    Slerp 회전
// MoveTowardTarget()                        target.position → MoveTowardPosition 위임
// MoveTowardPosition(Vector3 worldPos)      AddForce + 최대속도 클램프
// ================================================================

public class Enemy : Unit
{
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

	[Header("기동 패턴 가중치 (합계 기반 확률 / 모두 0이면 기본 ATTACK)")]
	[Tooltip("돌진 후 타겟을 지나쳐 재배치하는 패턴 선택 가중치.")]
	public float attackPassWeight = 1f;
	[Tooltip("타겟을 추적하며 사격하는 패턴 선택 가중치.")]
	public float attackWeight = 1f;
	[Tooltip("패턴 선택 시 EVADE(도주)가 뽑힐 가중치. 피격 없이도 발동 가능.")]
	public float evadeWeight = 1f;

	[Header("기동 패턴 지속 시간 (초)")]
	[Tooltip("ATTACK_PASS 최대 지속 시간. dot product 판정 전에 이 시간이 지나면 강제로 REPOSITION 전환.")]
	public float attackPassDuration = 3f;
	[Tooltip("REPOSITION 지속 시간. 목적지 도착 또는 시간 만료 시 CHASE로 복귀.")]
	public float repositionDuration = 4f;
	[Tooltip("EVADE 지속 시간. 이 시간이 지나면 전투 재개.")]
	public float evadeDuration = 2f;
	[Tooltip("재배치 목표 거리 (플레이어 기준)")]
	public float repositionDistance = 150f;

	[Header("EVADE 설정")]
	[Tooltip("피격 시 EVADE 진입 확률 (0~1)")]
	public float evadeChance = 0.4f;
	[Tooltip("EVADE 쿨타임 (초). 피격 반응 및 패턴 선택 양쪽 모두 적용.")]
	public float evadeCoolTime = 5f;

	[Header("DODGE 확률 설정 (피격 시 순간 무적 회피 / 쿨타임,무적시간은 Unit.dodgeCoolTime 공용)")]
	[Tooltip("피격 시 DODGE 발동 확률 (0~1)")]
	[Range(0.1f,1f)]
	public float dodgeProbability = 0.1f;

	[Header("AI 상태 (참고용, 입력X)")]
	public AI_STATE aiState = AI_STATE.PATROL;

	protected Transform target;
	protected Vector3 spawnPosition;
	protected Vector3 patrolTarget;

	private float _stateTimer = 0f;
	private float _evadeCoolTimer = 0f;
	private Vector3 _repositionTarget;
	private AI_STATE _prevAiState;

	// Enemy의 범용 전투/순찰 AI 사용 여부. EnemyWorker처럼 자체 AI를 쓰는 자식은 false로 override.
	protected virtual bool UseGenericAI => true;

	protected override void Start()
	{
		base.Start();

		target = GameObject.FindWithTag("Player")?.transform;

		spawnPosition = transform.position;
		PickNewPatrolPoint();
	}

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

		bool shouldBoost = (aiState == AI_STATE.CHASE || aiState == AI_STATE.ATTACK_PASS || aiState == AI_STATE.EVADE)
			&& curBoostRemaining > minBoostRequired;
		_isBoosting = shouldBoost;
		if (shouldBoost)
		{
			UseBoost(boostConsumeAmont * Time.fixedDeltaTime);
		}

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

			case AI_STATE.ATTACK_PASS:
				RotateTowardTarget();
				MoveTowardTarget();
				break;

			case AI_STATE.REPOSITION:
				RotateTowardPosition(_repositionTarget);
				MoveTowardPosition(_repositionTarget);
				break;

			case AI_STATE.EVADE:
				if (target != null)
				{
					Vector3 awayDir = (transform.position - target.position).normalized;
					MoveTowardPosition(transform.position + awayDir * 500f);
				}
				break;

			case AI_STATE.DODGE:
				// 진입 시 Impulse 이미 적용됨. 별도 이동 없음.
				break;
		}
	}

	//===============AI 상태머신 (자식에서 override해 디테일 구현)==================
	protected virtual void UpdateAI()
	{
		if (_stateTimer > 0f)
		{
			_stateTimer -= Time.deltaTime;
		}
		if (_evadeCoolTimer > 0f)
		{
			_evadeCoolTimer -= Time.deltaTime;
		}

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

			case AI_STATE.ATTACK_PASS:
				OnAIAttackPass();
				break;

			case AI_STATE.REPOSITION:
				OnAIReposition();
				break;

			case AI_STATE.EVADE:
				OnAIEvade();
				break;

			case AI_STATE.DODGE:
				OnAIDodge();
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
			PickCombatPattern();
		}
	}

	// 모든 가중치가 0일 때 진입하는 기본 전투 상태.
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

		ShootWeapons();
	}

	// 플레이어를 향해 돌진. dot product < 0 = 뒤로 지나침 → EnterReposition.
	protected virtual void OnAIAttackPass()
	{
		if (target == null || !IsTargetInRange(detectRange))
		{
			aiState = AI_STATE.CHASE;
			return;
		}

		Vector3 dirToTarget = (target.position - transform.position).normalized;
		bool passed = Vector3.Dot(transform.forward, dirToTarget) < 0f;

		if (passed || _stateTimer <= 0f)
		{
			EnterReposition();
			return;
		}

		ShootWeapons();
	}

	// 서브클래스가 override해서 발사할 무기 종류 지정. ATTACK/ATTACK_PASS 양쪽에서 호출됨.
	protected virtual void ShootWeapons() { }

	// _repositionTarget 방향으로 이동. 도착 or 타이머 만료 → CHASE 복귀.
	protected virtual void OnAIReposition()
	{
		float dist = Vector3.Distance(transform.position, _repositionTarget);
		if (dist <= patrolArriveDist || _stateTimer <= 0f)
		{
			aiState = AI_STATE.CHASE;
		}
	}

	// 플레이어 반대 방향으로 도망. 타이머 만료 → 전투 재개.
	protected virtual void OnAIEvade()
	{
		if (_stateTimer <= 0f)
		{
			if (HasTargetInAttackRange())
			{
				PickCombatPattern();
			}
			else
			{
				aiState = AI_STATE.CHASE;
			}
		}
	}

	// UNIT_STATE.DODGE 종료 감지 → 이전 AI_STATE로 복귀.
	protected virtual void OnAIDodge()
	{
		if (CurState != UNIT_STATE.DODGE)
		{
			aiState = _prevAiState;
		}
	}

	//===============피격 override==================

	public override void TakeDamage(DamageInfo info)
	{
		// 무적 중엔 DODGE 재진입 포함 모든 처리 차단 (dodge 타이머 리셋 방지)
		if (IsInvincible)
		{
			return;
		}

		// DODGE: 확률+쿨타임 충족 시 데미지 무효화. 쿨타임은 Unit.dodgeCoolTime 공용.
		if (_dodgeCooldownTimer <= 0f && Random.value < dodgeProbability)
		{
			_prevAiState = aiState;
			aiState = AI_STATE.DODGE;

			Vector3 dodgeDir = (Random.value < 0.5f) ? transform.right : -transform.right;
			_rb.AddForce(dodgeDir * dodgeForce, ForceMode.Impulse);
			CurState = UNIT_STATE.DODGE;
			return;
		}

		// EVADE: 확률+쿨타임 충족 시 도망 시작 (데미지는 그대로 받음)
		if (_evadeCoolTimer <= 0f && Random.value < evadeChance)
		{
			EnterEvade();
		}

		base.TakeDamage(info);
	}

	//===============패턴 선택==================

	// 가중치 기반으로 전투 패턴 선택. 모두 0이면 기본 ATTACK. EVADE 쿨타임 중엔 EVADE 제외.
	protected virtual void PickCombatPattern()
	{
		float effectiveEvadeWeight = (_evadeCoolTimer <= 0f) ? evadeWeight : 0f;
		float total = attackPassWeight + attackWeight + effectiveEvadeWeight;
		if (total <= 0f)
		{
			aiState = AI_STATE.ATTACK;
			return;
		}

		float rand = Random.Range(0f, total);
		if (rand < attackPassWeight)
		{
			EnterAttackPass();
		}
		else if (rand < attackPassWeight + attackWeight)
		{
			aiState = AI_STATE.ATTACK;
		}
		else
		{
			EnterEvade();
		}
	}

	private void EnterAttackPass()
	{
		aiState = AI_STATE.ATTACK_PASS;
		_stateTimer = attackPassDuration;
	}

	private void EnterReposition()
	{
		if (target != null)
		{
			Vector3 randDir = Random.insideUnitSphere.normalized;
			_repositionTarget = target.position + randDir * repositionDistance;
		}
		aiState = AI_STATE.REPOSITION;
		_stateTimer = repositionDuration;
	}

	private void EnterEvade()
	{
		aiState = AI_STATE.EVADE;
		_stateTimer = evadeDuration;
		_evadeCoolTimer = evadeCoolTime;
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

	// 월드 위치 방향으로 추진. _isBoosting 시 boostSpeed 사용.
	protected void MoveTowardPosition(Vector3 worldPos)
	{
		Vector3 dir = (worldPos - transform.position).normalized;
		float multiplier = speedMultiPlier > 0f ? speedMultiPlier : 1f;
		float speed = _isBoosting ? boostSpeed : baseMoveSpeed;
		float finalForce = speed * multiplier;

		_rb.AddForce(dir * finalForce, ForceMode.Acceleration);

		if (_rb.velocity.magnitude > maxSpeed)
		{
			_rb.velocity = _rb.velocity.normalized * maxSpeed;
		}
	}
}
