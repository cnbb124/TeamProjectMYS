using UnityEngine;

// ================================================================
// [EnemyShip — 이동형 적 AI 베이스]
// ================================================================
// Enemy 베이스의 탐지/유틸을 상속받아 순찰·추격·전투 AI를 추가.
// 전투기/순양함/보스 등 이동하는 적은 전부 이 클래스를 상속.
//
// Update()
//   └── UpdateAI()
//         ├── base.UpdateAI()             target 1초 갱신
//         ├── _stateTimer / _evadeCoolTimer 감소
//         ├── AI_STATE → UNIT_STATE 동기화
//         └── switch(aiState) → 각 OnAI*() 호출
//
// FixedUpdate()
//   └── base.FixedUpdate()               angularVelocity 리셋
//       ├── 부스터 자동 사용 (CHASE / ATTACK_PASS / EVADE)
//       └── switch(aiState) → 회전 + 이동
//               ATTACK_CHASE : minAttackDistance 이상일 때만 전진
//               ATTACK_PASS  : passOffsetStartDist 이내 진입 시 측면 오프셋 조향, 통과 후 REPOSITION
//               EVADE        : 반대 방향으로 회전 + 이동
//               RELOAD       : CHASE 이동과 동일 (타겟 추적, 발사 없음)
//
// TakeDamage() override
//   ├── DODGE: CanDodge + 확률 → 데미지 무효화
//   └── EVADE: CanEvade + 확률 → 도주 시작 (데미지는 받음)
//
// ================================================================
// PickCombatPattern()      attackChaseWeight / attackPassWeight / attackHoldWeight 가중치 선택
// EnterAttackChase()       ATTACK_CHASE 진입
// EnterAttackHold()        ATTACK_HOLD 진입
// EnterAttackPass()        ATTACK_PASS 진입 (좌우 방향 결정 포함)
// EnterReposition()        REPOSITION 진입 + _repositionTarget 선정
// EnterEvade()             EVADE 진입 + 쿨타임 세팅
// EnterReload(float)       RELOAD 진입 + 타이머 세팅
// PickNewPatrolPoint()     spawnPosition 기준 랜덤 순찰 지점 선정
// ================================================================

// ATTACK_PASS 궤도 오프셋 방향 설정.
// Random: 진입마다 좌우 랜덤 선택 / Right: 항상 오른쪽 / Left: 항상 왼쪽.
// (Enemy 기준 좌우 — 카메라 시점 기준 아님)
public enum PassOffsetDir { Random, Right, Left }

[RequireComponent(typeof(Rigidbody))]
public class EnemyShip : Enemy
{
    [Header("<size=18>순찰 설정</size>")]
    [Tooltip("스폰 위치 기준 수평 순찰 반경")]
    public float patrolRadius = 500f;
    [Tooltip("순찰 지점 도착 판정 거리")]
    public float patrolArriveDist = 2f;
    [Tooltip("스폰 위치 기준 수직 순찰 범위 (±). 0이면 수평면 고정.")]
    public float patrolHeightRange = 100f;

    [Header("<size=18>기동 패턴</size>")]
    [Header("<size=14>가중치  (합계 기반 확률 / 모두 0이면 기본 ATTACK_CHASE)</size>")]
    [Tooltip("돌진 후 타겟을 지나쳐 재배치하는 패턴")]
    [Range(0f, 1f)]
    public float attackPassWeight = 1f;
    [Tooltip("타겟을 추적하며 사격하는 패턴 (ATTACK_CHASE)")]
    [Range(0f, 1f)]
    public float attackChaseWeight = 1f;
    [Tooltip("제자리 정지 후 타겟을 공격하는 패턴")]
    [Range(0f, 1f)]
    public float attackHoldWeight = 1f;

    [Header("<size=14>지속 시간 (초)</size>")]
    [Tooltip("ATTACK_CHASE 지속 시간. 0이면 범위 이탈 전까지 유지.")]
    public float attackChaseDuration = 2f;
    [Tooltip("ATTACK_HOLD 지속 시간. 0이면 범위 이탈 전까지 유지.")]
    public float attackHoldDuration = 3f;
    [Tooltip("ATTACK_PASS 최대 지속 시간. 만료 시 강제 REPOSITION 전환.")]
    public float attackPassDuration = 4f;
    [Tooltip("REPOSITION 지속 시간. 도착 또는 만료 시 CHASE 복귀.")]
    public float repositionDuration = 2.5f;
    [Tooltip("EVADE 지속 시간.")]
    public float evadeDuration = 1.5f;
    [Tooltip("재배치 목표 거리 (플레이어 기준)")]
    public float repositionDistance = 200f;
    [Tooltip("발사 후 재장전 대기 시간 (초). 0이면 비활성화.")]
    public float reloadDuration = 0f;

    [Header("<size=18>전투 설정</size>")]
    [Header("<size=14>ATTACK_CHASE 최소 접근 거리</size>")]
    [Tooltip("타겟과 이 거리 이하로 좁혀지면 전진 멈춤. 0이면 비활성화.")]
    public float minAttackDistance = 200f;

    [Header("<size=14>ATTACK_PASS 궤도 오프셋</size>")]
    [Tooltip("ATTACK_PASS 진입 시 타겟 기준 어느 쪽으로 비껴갈지 결정.\n" +
             "· Random : 진입마다 좌/우 랜덤 선택\n" +
             "· Right  : 항상 Enemy 기준 오른쪽으로 통과\n" +
             "· Left   : 항상 Enemy 기준 왼쪽으로 통과\n" +
             "(Enemy 기준 좌우 — 카메라 시점 기준 아님)")]
    public PassOffsetDir passOffsetDir = PassOffsetDir.Random;

    [Tooltip("이 거리 이내로 접근하기 시작할 때부터 측면 조향 시작 (단위: 유닛).\n" +
             "클수록 일찍 방향을 틀어 완만한 곡선으로 비껴감.\n" +
             "0 이하로 설정하면 오프셋 없이 직선 돌진.\n" +
             "※ passHorizontalDist 의 3배 이상 권장.")]
    public float passOffsetStartDist = 200f;

    [Tooltip("통과 시 타겟 기준 좌우(수평) 이탈 폭 (단위: 유닛).\n" +
             "클수록 타겟 옆을 더 넓게 비껴 지나감.\n" +
             "0 이하로 설정하면 좌우 오프셋 없이 직선 돌진.\n" +
             "※ 상하(수직) 오프셋은 passVerticalRange 가 담당.")]
    public float passHorizontalDist = 60f;

    [Tooltip("통과 시 타겟 기준 상하 오프셋 최대 범위 (단위: 유닛).\n" +
             "진입마다 ±범위 안에서 랜덤하게 결정됨.\n" +
             "0이면 상하 변화 없이 수평으로만 비껴감.")]
    public float passVerticalRange = 40f;

    [Header("<size=18>특수 기동</size>")]
    [Header("<size=14>EVADE 설정</size>")]
    [Tooltip("피격 시 EVADE 진입 확률 (0~1)")]
    [Range(0f, 1f)]
    public float evadeChance = 0.4f;
    [Tooltip("EVADE 쿨타임 (초).")]
    public float evadeCoolTime = 5f;

    [Header("<size=14>DODGE 설정 (피격 시 순간 무적 / 쿨타임·무적시간은 Unit.dodgeCoolTime 공용)</size>")]
    [Tooltip("피격 시 DODGE 발동 확률 (0~1)")]
    [Range(0f, 1f)]
    public float dodgeProbability = 0.1f;

    // 서브클래스에서 false로 override하면 해당 반응 비활성화
    protected virtual bool CanDodge => true;
    protected virtual bool CanEvade => true;

    protected Vector3 patrolTarget;

    private float _stateTimer = 0f;
    private float _evadeCoolTimer = 0f;
    private Vector3 _repositionTarget;
    private AI_STATE _prevAiState;
    // ATTACK_PASS 진입 시 결정. +1 = 오른쪽(Right), -1 = 왼쪽(Left).
    private float _passOffsetSign = 1f;
    // ATTACK_PASS 진입 시 Random.Range(-passVerticalRange, passVerticalRange)로 결정.
    private float _passVerticalOffset = 0f;

    protected override void Start()
    {
        base.Start();
        aiState = AI_STATE.PATROL;
        PickNewPatrolPoint();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate(); // angularVelocity 리셋
        if (ShouldPause || CurState == UNIT_STATE.DIE || !UseGenericAI)
        {
            return;
        }

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
                //선회용값 
                MoveTowardPosition(transform.position + transform.forward);
                break;

            case AI_STATE.ATTACK_CHASE:
            {
                RotateTowardTarget();
                float dist = target != null ? Vector3.Distance(transform.position, target.position) : float.MaxValue;
                if (minAttackDistance > 0f && dist <= minAttackDistance)
                {
                    // 최소 거리 도달 — HOLD / PASS / REPOSITION 랜덤 전환
                    int rand = Random.Range(0, 3);
                    if (rand == 0)
                    {
                        EnterAttackHold();
                    }
                    else if (rand == 1)
                    {
                        EnterAttackPass();
                    }
                    else
                    {
                        EnterReposition();
                    }
                }
                else if (minAttackDistance > 0f && dist <= minAttackDistance * 2f)
                {
                    // 브레이킹 구간 (minAttackDistance 2배 이내) — 감속만
                    _rb.velocity = Vector3.MoveTowards(_rb.velocity, Vector3.zero, baseMoveSpeed * 5f * Time.fixedDeltaTime);
                }
                else
                {
                    MoveTowardPosition(transform.position + transform.forward);
                }
                break;
            }

            case AI_STATE.ATTACK_HOLD:
                RotateTowardTarget();
                break;

            case AI_STATE.ATTACK_PASS:
            {
                if (target == null)
                {
                    break;
                }

                float distToTarget = Vector3.Distance(transform.position, target.position);

                // ── 조향 목표 계산 ─────────────────────────────────────────────
                // passOffsetStartDist 밖   : 타겟을 향해 직선 접근 (오프셋 없음)
                // passOffsetStartDist 이내 : 타겟 옆을 향해 점진적으로 방향 전환
                //   t = 0 (passOffsetStartDist 진입 직후) → t = 1 (타겟에 가장 근접)
                //   오프셋량 = passHorizontalDist * t  (가까워질수록 점점 벌어짐)
                // passHorizontalDist or passOffsetStartDist 가 0 이하면 직선 돌진.
                // ────────────────────────────────────────────────────────────────
                Vector3 aimPoint;

                if (passHorizontalDist > 0f
                    && passOffsetStartDist > 0f
                    && distToTarget <= passOffsetStartDist)
                {
                    Vector3 dirToTarget = (target.position - transform.position).normalized;

                    // 접근 방향에 수평으로 수직인 벡터 계산 (dirToTarget × 월드 UP)
                    // dirToTarget 이 거의 수직(↑/↓)이면 외적이 영벡터에 가까워지므로 fallback 처리
                    Vector3 perp = Vector3.Cross(dirToTarget, Vector3.up);
                    if (perp.sqrMagnitude < 0.01f)
                    {
                        perp = Vector3.Cross(dirToTarget, Vector3.forward);
                    }
                    // _passOffsetSign: +1 = 오른쪽, -1 = 왼쪽 (EnterAttackPass에서 결정)
                    perp = perp.normalized * _passOffsetSign;

                    // passOffsetStartDist 경계에서 오프셋 0, 타겟에 가까울수록 1 로 증가
                    float t = Mathf.Clamp01(1f - distToTarget / passOffsetStartDist);
                    // 수평(좌우) + 수직(상하) 오프셋을 독립적으로 합산
                    aimPoint = target.position
                        + perp            * (passHorizontalDist * t)
                        + Vector3.up      * (_passVerticalOffset * t);
                }
                else
                {
                    // 오프셋 범위 밖이거나 비활성화 — 타겟 직선 접근
                    aimPoint = target.position;
                }

                RotateTowardPosition(aimPoint);
                MoveTowardPosition(transform.position + transform.forward);
                break;
            }

            case AI_STATE.REPOSITION:
                RotateTowardPosition(_repositionTarget);
                MoveTowardPosition(_repositionTarget);
                break;

            case AI_STATE.EVADE:
                if (target != null)
                {
                    Vector3 awayDir = (transform.position - target.position).normalized;
                    Vector3 awayTarget = transform.position + awayDir * 500f;
                    RotateTowardPosition(awayTarget);
                    // 이동은 현재 기수 방향(transform.forward) 기준 — 선회하면서 그 방향으로 가속
                    MoveTowardPosition(transform.position + transform.forward);
                }
                break;

            case AI_STATE.DODGE:
                // 진입 시 Impulse 이미 적용됨. 별도 이동 없음.
                break;

            case AI_STATE.RELOAD:
                RotateTowardTarget();
                MoveTowardPosition(transform.position + transform.forward);
                break;
        }
    }

    protected override void UpdateAI()
    {
        base.UpdateAI(); // target 1초 갱신

        if (_stateTimer > 0f)
        {
            _stateTimer -= Time.deltaTime;
        }
        if (_evadeCoolTimer > 0f)
        {
            _evadeCoolTimer -= Time.deltaTime;
        }

        // AI_STATE → UNIT_STATE 동기화. DODGE/DIE 중에는 Unit FSM이 우선.
        if (curState != UNIT_STATE.DODGE && curState != UNIT_STATE.DIE)
        {
            bool isStationary = (aiState == AI_STATE.STANDBY || aiState == AI_STATE.ATTACK_HOLD);
            CurState = isStationary ? UNIT_STATE.IDLE : UNIT_STATE.MOVING;
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
            case AI_STATE.ATTACK_CHASE:
                OnAIAttackChase();
                break;
            case AI_STATE.ATTACK_HOLD:
                OnAIAttackHold();
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
            case AI_STATE.RELOAD:
                OnAIReload();
                break;
        }
    }

    public override void TakeDamage(DamageInfo info)
    {
        if (IsInvincible)
        {
            return;
        }

        // DODGE: 확률+쿨타임 충족 시 데미지 무효화.
        if (CanDodge && _dodgeCooldownTimer <= 0f && Random.value < dodgeProbability)
        {
            _prevAiState = aiState;
            aiState = AI_STATE.DODGE;
            Vector3 dodgeDir = (Random.value < 0.5f) ? transform.right : -transform.right;
            _rb.AddForce(dodgeDir * dodgeForce, ForceMode.Impulse);
            CurState = UNIT_STATE.DODGE;
            return;
        }

        // EVADE: 확률+쿨타임 충족 시 도주 시작 (데미지는 그대로 받음).
        if (CanEvade && _evadeCoolTimer <= 0f && Random.value < evadeChance)
        {
            EnterEvade();
        }

        base.TakeDamage(info);
    }

    //=============== OnAI* 메서드 ===============

    protected virtual void OnAIStandby()
    {
        if (IsTargetInRange(detectRange))
            aiState = AI_STATE.CHASE;
        else
            aiState = AI_STATE.PATROL;
    }

    protected virtual void OnAIPatrol()
    {
        if (IsTargetInRange(detectRange))
        {
            aiState = AI_STATE.CHASE;
            return;
        }
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

    // ATTACK_CHASE 처리. attackChaseDuration 만료 시 PickCombatPattern 재호출.
    protected virtual void OnAIAttackChase()
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
        if (attackChaseDuration > 0f && _stateTimer <= 0f)
        {
            PickCombatPattern();
            return;
        }
        ShootWeapons();
        if (reloadDuration > 0f && HasTargetInAttackRange())
            EnterReload(reloadDuration);
    }

    // ATTACK_HOLD 처리. 제자리 정지 + 발사. attackHoldDuration 만료 시 PickCombatPattern 재호출.
    protected virtual void OnAIAttackHold()
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
        if (attackHoldDuration > 0f && _stateTimer <= 0f)
        {
            PickCombatPattern();
            return;
        }
        ShootWeapons();
        if (reloadDuration > 0f && HasTargetInAttackRange())
            EnterReload(reloadDuration);
    }

    // 플레이어를 향해 돌진. dot < 0 = 뒤로 지나침 → EnterReposition.
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
        ShootWeaponsOnPass();
    }

	/// <summary>
	/// 체이스와 동일, 공격만X
	/// </summary>
	protected virtual void OnAIReload()
    {
        
		if (!IsTargetInRange(detectRange))
		{
			aiState = AI_STATE.PATROL;
			return;
		}
		if (_stateTimer <= 0f)
		{
			PickCombatPattern();
		}
	}

    // ATTACK_PASS 전용 발사. 기본값 empty = 총알 미사용.
    // 미사일을 유지하려는 서브클래스는 override해서 Shoot(MISSILE)만 호출.
    protected virtual void ShootWeaponsOnPass() { }

    // 재배치 목표 도착 or 타이머 만료 → CHASE 복귀.
    protected virtual void OnAIReposition()
    {
        float dist = Vector3.Distance(transform.position, _repositionTarget);
        if (dist <= patrolArriveDist || _stateTimer <= 0f)
        {
            aiState = AI_STATE.CHASE;
        }
    }

    // 도망 중. 타이머 만료 → 전투 재개.
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

    // UNIT_STATE.DODGE 종료 감지 → 이전 AI_STATE 복귀.
    protected virtual void OnAIDodge()
    {
        if (CurState != UNIT_STATE.DODGE)
        {
            aiState = _prevAiState;
        }
    }

    //=============== 패턴 선택 ===============

    // 가중치 기반 전투 패턴 선택. 모두 0이면 기본 ATTACK_CHASE.
    protected virtual void PickCombatPattern()
    {
        float total = attackPassWeight + attackChaseWeight + attackHoldWeight;
        if (total <= 0f)
        {
            EnterAttackChase();
            return;
        }
        float rand = Random.Range(0f, total);
        if (rand < attackPassWeight)
        {
            EnterAttackPass();
        }
        else if (rand < attackPassWeight + attackChaseWeight)
        {
            EnterAttackChase();
        }
        else
        {
            EnterAttackHold();
        }
    }

    protected void EnterAttackChase()
    {
        aiState = AI_STATE.ATTACK_CHASE;
        _stateTimer = attackChaseDuration;
    }

    protected void EnterAttackHold()
    {
        aiState = AI_STATE.ATTACK_HOLD;
        _stateTimer = attackHoldDuration;
    }

    protected void EnterAttackPass()
    {
        aiState = AI_STATE.ATTACK_PASS;
        _stateTimer = attackPassDuration;

        // 인스펙터 설정에 따라 좌우 방향 결정. Random이면 50% 확률로 선택.
        switch (passOffsetDir)
        {
            case PassOffsetDir.Right: _passOffsetSign =  1f; break;
            case PassOffsetDir.Left:  _passOffsetSign = -1f; break;
            default:                  _passOffsetSign = (Random.value < 0.5f) ? 1f : -1f; break;
        }

        // 상하 오프셋을 ±passVerticalRange 범위에서 랜덤 결정.
        _passVerticalOffset = (passVerticalRange > 0f)
            ? Random.Range(-passVerticalRange, passVerticalRange)
            : 0f;
    }

    protected void EnterReposition()
    {
        if (target != null)
        {
            // 플레이어 반대 방향으로 직진 — 자신의 현재 위치 기준으로 멀어짐
            Vector3 awayDir = (transform.position - target.position).normalized;
            _repositionTarget = transform.position + awayDir * repositionDistance;
        }
        aiState = AI_STATE.REPOSITION;
        _stateTimer = repositionDuration;
    }

    protected void EnterEvade()
    {
        aiState = AI_STATE.EVADE;
        _stateTimer = evadeDuration;
        _evadeCoolTimer = evadeCoolTime;
    }

    protected void EnterReload(float duration)
    {
        aiState = AI_STATE.RELOAD;
        _stateTimer = duration;
    }
    // spawnPosition 기준 patrolRadius(수평), patrolHeightRange(수직) 안의 랜덤 지점을 새 순찰 목표로 선정.
    protected void PickNewPatrolPoint()
    {
        Vector2 rand = Random.insideUnitCircle * patrolRadius;
        float yOffset = Random.Range(-patrolHeightRange, patrolHeightRange);
        patrolTarget = spawnPosition + new Vector3(rand.x, yOffset, rand.y);
    }
}
