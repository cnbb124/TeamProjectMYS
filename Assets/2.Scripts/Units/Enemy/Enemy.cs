using UnityEngine;

// ================================================================
// [Enemy — 최소 베이스]
// ================================================================
// 탐지 / 타겟 갱신 / 공통 이동 유틸만 보유.
// 이동·전투 AI가 필요한 적  → EnemyShip 상속
// 고정 포탑               → EnemyTurretBase 상속
// 자체 AI가 있는 특수 적  → Enemy 직접 상속 (UseGenericAI = false)
//
// UpdateAI() — 1초마다 UnitManager에서 최근접 플레이어로 target 갱신
//
// ================================================================
// Start()                                UnitManager.RegisterEnemy 호출
// Die()                                  UnitManager.UnregisterEnemy 호출 + base.Die()
// IsTargetInRange(float range)           단순 거리 비교
// HasTargetInAttackRange()               LockOnSystem.TargetsInLockonRange 수 체크
// ShootWeapons()                         virtual — 자식이 override해 발사 종류 지정
// RotateTowardTarget()                   virtual — 터렛은 swivel/mount 방식으로 override
// RotateTowardPosition(Vector3)          RotateTowards 선회 (rotateSpeed = 도/초)
// MoveTowardTarget()                     target.position → MoveTowardPosition 위임
// MoveTowardPosition(Vector3)            AddForce + 최대속도 클램프
// ================================================================

public class Enemy : Unit
{
    [Header("<size=22>Enemy AI 설정</size>")]
    [Tooltip("이 범위 안에 타겟이 들어오면 추격 시작. 공격 진입은 LockOnSystem의 lockOnRange 기준.")]
    public float detectRange = 500f;
    [Tooltip("선회 속도 (도/초). 90 = 2초에 180도 회전.")]
    public float rotateSpeed = 180f;
    [Tooltip("이 각도(도) 이내에 타겟이 있으면 회전하지 않음. 0이면 비활성화.\n" +
             "전함/대형 유닛처럼 세밀한 조준을 안 하는 느낌에 적합.")]
    public float rotateDeadZone = 0f;

    [Header("<size=14>AI 상태 (참고용, 입력X)</size>")]
    public AI_STATE aiState = AI_STATE.STANDBY;

    protected Transform target;
    protected Vector3 spawnPosition;

    private float _targetUpdateTimer = 0f;
    private const float TargetUpdateInterval = 1f;

    // 범용 AI 사용 여부. EnemyWorker처럼 자체 AI를 쓰는 자식은 false로 override.
    protected virtual bool UseGenericAI => true;

    protected override void Start()
    {
        base.Start();
        UpdateTarget();
        spawnPosition = transform.position;
        UnitManager.Instance?.RegisterEnemy(this);
    }

    protected override void Die()
    {
        UnitManager.Instance?.UnregisterEnemy(this);
        base.Die();
    }

    protected override void Update()
    {
        base.Update();
        //일시정지중,죽었을시, AI사용안할시 AI사용안함
        if (ShouldPause || CurState == UNIT_STATE.DIE || !UseGenericAI)
        {
            return;
        }
        UpdateAI();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        // 물리 스핀 방지 — 상태 무관 항상 리셋
        if (_rb != null) _rb.angularVelocity = Vector3.zero;
        if (ShouldPause || CurState == UNIT_STATE.DIE || !UseGenericAI)
        {
            return;
        }
    }

    // 1초마다 target 갱신. 자식이 base.UpdateAI() 호출로 공유.
    protected virtual void UpdateAI()
    {
        _targetUpdateTimer -= Time.deltaTime;
        if (_targetUpdateTimer <= 0f)
        {
            UpdateTarget();
            _targetUpdateTimer = TargetUpdateInterval;
        }
    }

    // 자식이 override해 발사 종류 지정.
    protected virtual void ShootWeapons() { }

    private void UpdateTarget()
    {
        if (UnitManager.Instance == null)
        {
            return;
        }
        target = UnitManager.Instance.GetNearestPlayer(transform.position);
    }

    protected bool IsTargetInRange(float range)
    {
        if (target == null)
        {
            return false;
        }
        return Vector3.Distance(transform.position, target.position) <= range;
    }

    protected bool HasTargetInAttackRange()
    {
        if (weaponSystem == null || weaponSystem.lockOnSystem == null)
        {
            return false;
        }
        return weaponSystem.lockOnSystem.TargetsInLockonRange.Count > 0;
    }

    // 터렛은 swivel/mount 방식으로 override.
    protected virtual void RotateTowardTarget()
    {
        if (target == null)
        {
            return;
        }
        RotateTowardPosition(target.position);
    }

    // rotateSpeed 도/초 기준 일정 선회 속도. 즉시 스냅 없음.
    protected void RotateTowardPosition(Vector3 worldPos)
    {
        Vector3 dir = (worldPos - transform.position).normalized;
        if (dir == Vector3.zero)
        {
            return;
        }
        if (rotateDeadZone > 0f && Vector3.Angle(transform.forward, dir) <= rotateDeadZone)
        {
            return;
        }
        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRot, rotateSpeed * Time.fixedDeltaTime);
    }

    protected void MoveTowardTarget()
    {
        if (target == null)
        {
            return;
        }
        MoveTowardPosition(target.position);
    }

    protected void MoveTowardPosition(Vector3 worldPos)
    {
        Vector3 dir = (worldPos - transform.position).normalized;
        float multiplier = speedMultiPlier > 0f ? speedMultiPlier : 1f;
        float speed = _isBoosting ? boostSpeed : baseMoveSpeed;
        if (_rb == null) return;
        _rb.AddForce(dir * speed * multiplier, ForceMode.Acceleration);
        if (_rb.velocity.magnitude > maxSpeed)
        {
            _rb.velocity = _rb.velocity.normalized * maxSpeed;
        }
    }
}
