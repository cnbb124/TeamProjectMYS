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
// OnEnable()                             aiState=STANDBY 리셋 + UnitManager.RegisterEnemy 호출 (풀 재사용 시도 매번 실행)
// OnDisable()                            UnitManager.UnregisterEnemy 호출 (자기 사망이든 부모 cascade든 항상 호출됨)
// Die()                                  GameManager.OnEnemyKilled() 호출 + base.Die() (Unregister는 OnDisable이 처리)
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

    [Header("<size=18>AI 상태 (참고용, 입력X)</size>")]
    public AI_STATE aiState = AI_STATE.STANDBY;

    [Header("<size=18>후방 공격 빈도 감소</size>")]
    [Tooltip("타겟(target.forward) 기준 이 각도(도) 이상 등 뒤에 있으면 후방으로 판정.\n" +
             "180=정반대(완전 후방), 90=측면, 0=정면.")]
    [Range(0f, 180f)]
    public float rearAttackAngleThreshold = 110f;
    [Tooltip("후방 판정 시 발사를 건너뛸 확률 (0~1). 0이면 후방 페널티 없음.")]
    [Range(0f, 1f)]
    public float rearAttackSkipChance = 0.7f;

    [Header("<size=18>예측 사격 (Bullet 전용 — 미사일은 락온이라 영향 없음)</size>")]
    [Tooltip("0 = 예측 안 함(타겟 현재 위치 그대로 조준), 1 = 완전 예측(타겟 속도 기준 정확히 선조준).\n" +
             "weaponSystem.curBulletData가 없으면(미사일 전용 함선 등) 값과 무관하게 예측 안 함.")]
    [Range(0f, 1f)]
    public float leadAccuracy = 0f;

    protected Transform target;
    // target의 Velocity(Rigidbody.velocity) 참조용. UpdateTarget()에서 target과 함께 갱신.
    protected Unit targetUnit;

    protected Vector3 spawnPosition;

    private float _targetUpdateTimer = 0f;
    private const float TargetUpdateInterval = 1f;

    // 범용 AI 사용 여부. EnemyWorker처럼 자체 AI를 쓰는 자식은 false로 override.
    protected virtual bool UseGenericAI => true;

    // OnEnable이 Start보다 항상 먼저 호출되므로, 등록은 여기서 — 죽어서 Unregister된 뒤
    // 풀에서 재사용(SetActive(true))될 때도 매번 다시 등록됨. RegisterEnemy는 중복등록 가드 있어 안전.
    // aiState는 STANDBY로 리셋 — 서브클래스(EnemyShip/EnemyTurretBase)가 각자 OnAIStandby/STANDBY 케이스에서
    // 실제 시작 상태(PATROL/RELOAD 등)로 알아서 전환함.
    protected override void OnEnable()
    {
        base.OnEnable();
        aiState = AI_STATE.STANDBY;
        UnitManager.Instance?.RegisterEnemy(this);
    }

    // 부모(전함/터렛 거치대 등)가 SetActive(false)되면 자식 터렛도 같이 비활성화되는데,
    // 그 경우 자식 자신의 Die()는 호출되지 않아서 UnitManager 등록이 안 풀리는 문제가 있었음 —
    // OnDisable은 비활성화 원인(자기 사망 vs 부모 cascade) 무관하게 항상 호출되므로 여기서 처리.
    protected override void OnDisable()
    {
        base.OnDisable();
        UnitManager.Instance?.UnregisterEnemy(this);
    }

    protected override void Start()
    {
        base.Start();
        UpdateTarget();
        spawnPosition = transform.position;
    }

    // 위치를 직접 배치하는 스폰 호출부(SpawnManager 등)가 transform.position을 옮긴 직후 호출.
    // OnEnable은 Get() 직후(=재배치 이전) 호출돼서 거기서 캡처하면 죽기 전 위치가 잡혀버림 —
    // 그래서 재배치가 끝난 다음 이 메서드로 명시적으로 갱신함. (ScenePlaced는 재배치를 안 하므로 호출 불필요)
    public void RefreshSpawnAnchor()
    {
        spawnPosition = transform.position;
    }

    protected override void Die()
    {
        GameManager.Instance?.OnEnemyKilled();
        // 풀 등록 여부와 무관하게 SetActive(false)로 정리 — 죽은 적이 씬에 계속 남아있던 문제 해결.
        // (SetActive(false) → OnDisable() → UnitManager.UnregisterEnemy 자동 호출됨)
        PoolManager.Instance?.Return(gameObject);
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

    // 타겟의 후방(등 뒤)에서 공격 중이면 rearAttackSkipChance 확률로 true.
    // 호출부에서 true면 ShootWeapons()/ShootWeaponsOnPass() 호출을 건너뜀.
    protected bool ShouldSkipAttackFromBehind()
    {
        if (target == null)
        {
            return false;
        }
        Vector3 toEnemy = (transform.position - target.position).normalized;
        float dot = Vector3.Dot(target.forward, toEnemy);
        float dotThreshold = Mathf.Cos(rearAttackAngleThreshold * Mathf.Deg2Rad);
        if (dot >= dotThreshold)
        {
            return false;
        }
        return Random.value < rearAttackSkipChance;
    }

    // 미사일 쏘는 서브클래스(MissileShip/FighterShip/터렛 등)가 발사 전에 호출.
    // 락온이 필요한 타입인데 락온이 안 되어 있으면 false — Enemy AI만 이 체크를 거침, Player는 자유 발사.
    protected bool CanFireMissile()
    {
        return weaponSystem.lockOnSystem == null || weaponSystem.HasValidLockOn();
    }

    private void UpdateTarget()
    {
        if (UnitManager.Instance == null)
        {
            return;
        }
        target = UnitManager.Instance.GetNearestPlayer(transform.position);
        targetUnit = target != null ? target.GetComponent<Unit>() : null;
    }

    // 타겟의 현재 위치 + (속도 * 도달시간)으로 예측 조준점 계산.
    // leadAccuracy로 보정(0=예측없음~1=완전예측). bulletSpeed가 0 이하면 예측 안 함(미사일 전용 함선 대비).
    protected Vector3 GetPredictedAimPoint()
    {
        if (target == null)
        {
            return Vector3.zero;
        }
        if (leadAccuracy <= 0f || targetUnit == null || weaponSystem == null || weaponSystem.curBulletData == null)
        {
            return target.position;
        }
        float bulletSpeed = weaponSystem.curBulletData.speed;
        if (bulletSpeed <= 0f)
        {
            return target.position;
        }
        float distance = Vector3.Distance(transform.position, target.position);
        float leadTime = distance / bulletSpeed;
        Vector3 fullPredictedPos = target.position + targetUnit.Velocity * leadTime;
        return Vector3.Lerp(target.position, fullPredictedPos, leadAccuracy);
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
        RotateTowardPosition(GetPredictedAimPoint());
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
