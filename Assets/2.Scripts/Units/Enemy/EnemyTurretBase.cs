using UnityEngine;

// 고정 포탑 공통 베이스. Enemy 직접 상속 — 이동/순찰/전투패턴 필드 없음.
// UpdateAI : 탐지 범위 체크 → 범위 내 + 공격 범위 → ATTACK_HOLD + 발사. 이외는 STANDBY.
// FixedUpdate : 탐지 범위 내 타겟이 있으면 swivel/mount 회전.
// 자식은 ShootWeapons()만 override해 발사 무기 종류를 지정.
public class EnemyTurretBase : Enemy
{
    [Header("회전할 파츠")]
    [Tooltip("*SOCKET_SWIVEL — 수평(Y축) 좌우 회전")]
    public Transform swivelTransform;
    [Tooltip("*SOCKET_MOUNT — 수직(X축) 상하 회전")]
    public Transform mountTransform;

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

        if (IsTargetInRange(detectRange) && HasTargetInAttackRange())
        {
            aiState = AI_STATE.ATTACK_HOLD;
            ShootWeapons();
        }
        else
        {
            aiState = AI_STATE.STANDBY;
        }

        if (CurState != UNIT_STATE.DIE)
        {
            CurState = UNIT_STATE.IDLE;
        }
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
