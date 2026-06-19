using UnityEngine;

// 고정 포탑 공통 베이스. Enemy 직접 상속 — 이동/순찰/전투패턴 필드 없음.
// UpdateAI : ATTACK_HOLD ↔ RELOAD 사이클 관리.
//   ATTACK_HOLD : 발사. 타겟 이탈 시 즉시 RELOAD(0f). attackHoldDuration 만료 시 RELOAD(reloadDuration).
//   RELOAD : 타이머 만료 + 타겟 범위 내 → EnterAttackHold. 타겟 없으면 대기 유지.
// FixedUpdate : 탐지 범위 내 타겟이 있으면 swivel/mount 회전.
// 자식은 ShootWeapons()만 override해 발사 무기 종류를 지정.
public class EnemyTurretBase : Enemy
{
    [Header("<size=18>회전할 파츠</size>")]
    [Tooltip("*SOCKET_SWIVEL — 수평(Y축) 좌우 회전")]
    public Transform swivelTransform;
    [Tooltip("*SOCKET_MOUNT — 수직(X축) 상하 회전")]
    public Transform mountTransform;

    [Header("<size=18>공격 타이밍</size>")]
    [Tooltip("ATTACK_HOLD 지속 시간(초). 이 시간만큼 발사 후 강제 대기.\n" +
             "0이면 타겟이 범위를 벗어날 때까지 계속 발사.")]
    public float attackHoldDuration = 3f;
    [Tooltip("ATTACK_HOLD 종료 후 RELOAD 대기 시간(초).\n" +
             "타겟이 범위 내에 있어도 이 시간만큼 발사를 멈춤.\n" +
             "0이면 즉시 재공격.")]
    public float reloadDuration = 2f;

    private float _stateTimer = 0f;

    protected override void Start()
    {
        base.Start();
        baseMoveSpeed = 0f;
        maxSpeed = 0f;
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate(); // angularVelocity 리셋
        if (ShouldPause || CurState == UNIT_STATE.DIE)
        {
            return;
        }
        if (target != null && IsTargetInRange(detectRange))
        {
            RotateTowardTarget();
        }
    }

    protected override void UpdateAI()
    {
        base.UpdateAI(); // target 1초 갱신

        if (_stateTimer > 0f)
        {
            _stateTimer -= Time.deltaTime;
        }

        switch (aiState)
        {
            case AI_STATE.ATTACK_HOLD:
                if (!IsTargetInRange(detectRange) || !HasTargetInAttackRange())
                {
                    // 타겟 이탈 — 쿨타임 없이 Reload (타겟 복귀 시 즉시 재공격)
                    EnterReload(0f);
                    break;
                }
                if (attackHoldDuration > 0f && _stateTimer <= 0f)
                {
                    // 공격 지속시간 만료 — reloadDuration 쿨타임
                    EnterReload(reloadDuration);
                    break;
                }
                ShootWeapons();
                break;
            case AI_STATE.RELOAD:
                if (_stateTimer <= 0f)
                {
                    if (IsTargetInRange(detectRange) && HasTargetInAttackRange())
                    {
                        EnterAttackHold();
                    }
                }
                break;
        }

        if (CurState != UNIT_STATE.DIE)
        {
            CurState = UNIT_STATE.IDLE;
        }
    }

    private void EnterAttackHold()
    {
        aiState = AI_STATE.ATTACK_HOLD;
        _stateTimer = attackHoldDuration;
    }

    /// <summary>
    /// 레거시
    /// </summary>
    /// <param name="duration"></param>
    private void EnterStandby(float duration)
    {
        aiState = AI_STATE.STANDBY;
        _stateTimer = duration;
    }

    protected void EnterReload(float duration)
    {
        aiState = AI_STATE.RELOAD;
        _stateTimer = duration;
    }

    // 루트 전체 대신 swivel(좌우)/mount(상하)만 회전.
    protected override void RotateTowardTarget()
    {
        if (target == null)
        {
            return;
        }

        // 수평(Y축) — Swivel이 좌우로만 회전
        if (swivelTransform != null)
        {
            Vector3 flatTarget = target.position;
            flatTarget.y = swivelTransform.position.y;
            Quaternion rotY = Quaternion.LookRotation(flatTarget - swivelTransform.position, swivelTransform.up);
            swivelTransform.rotation = Quaternion.RotateTowards(
                swivelTransform.rotation, rotY, rotateSpeed * Time.fixedDeltaTime);
            swivelTransform.localEulerAngles = new Vector3(0f, swivelTransform.localEulerAngles.y, 0f);
        }

        // 수직(X축) — Mount가 상하로만 회전
        if (mountTransform != null)
        {
            Vector3 dir = target.position - mountTransform.position;
            Vector3 up = swivelTransform != null ? swivelTransform.up : transform.up;
            Quaternion rotX = Quaternion.LookRotation(dir, up);
            mountTransform.rotation = Quaternion.RotateTowards(
                mountTransform.rotation, rotX, rotateSpeed * Time.fixedDeltaTime);
            mountTransform.localEulerAngles = new Vector3(mountTransform.localEulerAngles.x, 0f, 0f);
        }
    }
}
