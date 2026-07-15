using Photon.Pun;
using UnityEngine;

// ================================================================
// [Enemy — 최소 베이스]
// ================================================================
// 탐지 / 타겟 갱신 / 공통 이동 유틸만 보유.
// 이동·전투 AI가 필요한 적  → EnemyShip 상속
// 고정 포탑               → EnemyTurretBase 상속
// 자체 AI가 있는 특수 적  → Enemy 직접 상속 (UseGenericAI = false)
//
// UpdateAI() — 1초마다 UnitManager에서 최근접 플레이어로 _target 갱신
//
// ================================================================
// OnEnable()                             aiState=STANDBY 리셋 + UnitManager.RegisterEnemy 호출 (풀 재사용 시도 매번 실행)
// OnDisable()                            UnitManager.UnregisterEnemy 호출 (자기 사망이든 부모 cascade든 항상 호출됨)
// Die()                                  OnEnemyKilled(killer, exp, gold) 위임(킬카운트+보상) + 아이템 드랍 + base.Die() (Unregister는 OnDisable이 처리)
// IsTargetInRange(float range)           단순 거리 비교
// HasTargetInAttackRange()               LockOnSystem.TargetsInLockonRange 수 체크
// ShootWeapons()                         virtual — 자식이 override해 발사 종류 지정
// RotateTowardTarget()                   virtual — 터렛은 swivel/mount 방식으로 override
// RotateTowardPosition(Vector3)          RotateTowards 선회 (rotateSpeed = 도/초)
// MoveTowardTarget()                     _target.position → MoveTowardPosition 위임
// MoveTowardPosition(Vector3)            AddForce + 최대속도 클램프
// ================================================================

public class Enemy : Unit
{
	[Header("<size=14>==========================================</size>")]
	[Header("<size=18>Enemy 보상 설정</size>")]
    [Tooltip("경험치 보상 최소치")]
    public int expRewardMin;
    [Tooltip("경험치 보상 최대치 (최소~최대 사이에서 랜덤 지급)")]
    public int expRewardMax;
    [Tooltip("골드 보상 최소치")]
    public int goldRewardMin;
    [Tooltip("골드 보상 최대치 (최소~최대 사이에서 랜덤 지급)")]
    public int goldRewardMax;
    [Tooltip("드랍 후보 아이템의 풀 타입 목록. 죽을 때 이 중 랜덤 하나를 풀에서 꺼내 드랍.\n" +
             "각 픽업 프리팹에 ItemData가 직렬화돼 있어 Init 없이 자동 세팅됨. PoolManager.poolConfigs에 등록 필요.")]
    public POOL_TYPE[] dropPoolTypes;
    [Tooltip("아이템이 드랍될 확률 (0~1). 1=항상 드랍, 0.3=30% 확률. 실패하면 아무것도 안 나옴.\n" +
             "드랍이 결정되면 위 목록 중 랜덤 하나가 나옴.")]
    [Range(0f, 1f)]
    public float dropChance = 1f;
    [Header("<size=18>Enemy AI 공통 설정</size>")]
   
    [Header("<size=14>1. 탐지 관련 설정</size>")]
    [Tooltip("이 범위 안에 타겟이 들어오면 추격 시작. 공격 진입은 LockOnSystem의 lockOnRange 기준.")]
    public float detectRange = 500f;
    [Tooltip("선회 속도 (도/초). 90 = 1초에 90도 회전.")]
    public float rotateSpeed = 180f;
    [Tooltip("이 각도(도) 이내에 타겟이 있으면 회전하지 않음. 0이면 비활성화.\n" +
             "전함/대형 유닛처럼 세밀한 조준을 안 하는 느낌에 적합.")]
    public float rotateDeadZone = 0f;
	[Header("AI 상태 (참고용, 입력X)")]
	public AI_STATE aiState = AI_STATE.STANDBY;

	[Header("<size=14>2. 공격 관련 설정</size>")]
    [Header("예측 사격 (Bullet 전용 — 미사일은 락온이라 영향 없음)")]
	[Tooltip("0 = 예측 안 함(타겟 현재 위치 그대로 조준), 1 = 완전 예측(타겟 속도 기준 정확히 선조준).\n" +
			 "weaponSystem.curBulletData가 없으면(미사일 전용 함선 등) 값과 무관하게 예측 안 함.")]
	[Range(0f, 1f)]
	public float leadAccuracy = 0f;
	[Header("후방 공격 빈도 설정")]
	[Tooltip("타겟(_target.forward) 기준 이 각도(도) 이상 등 뒤에 있으면 후방으로 판정.\n" +
			 "180=정반대(완전 후방), 90=측면, 0=정면.")]
	[Range(0f, 180f)]
	public float rearAttackAngleThreshold = 110f;
	[Tooltip("후방 판정 시 발사를 건너뛸 확률 (0~1). 0이면 후방 페널티 없음.")]
	[Range(0f, 1f)]
	public float rearAttackSkipChance = 0.7f;
	

    

    

    protected Transform _target;
    // _target의 Velocity(Rigidbody.velocity) 참조용. UpdateTarget()에서 _target과 함께 갱신.
    protected Unit _targetUnit;

    protected Vector3 _spawnPosition;

    private float _targetUpdateTimer = 0f;
    private const float TargetUpdateInterval = 1f;

    // 범용 AI 사용 여부. EnemyWorker처럼 자체 AI를 쓰는 자식은 false로 override.
    protected virtual bool UseGenericAI => true;

    // 멀티 소유권 판정. PhotonView 없으면(싱글 씬배치/오프라인 등) 항상 내 것 → 기존 단일 동작 그대로.
    // PhotonNetwork.Instantiate로 스폰된 적만 PhotonView를 가지며 Master가 소유(IsMine=true)해 AI를 돌린다.
    // 남(비Master) 클라에선 IsMine=false라 AI를 안 돌리고, 위치는 PhotonTransformView 동기화로만 갱신됨.
    private PhotonView _photonView;
    protected bool IsMine => _photonView == null || _photonView.IsMine;

    protected override void Awake()
    {
        base.Awake();
        _photonView = GetComponent<PhotonView>();
    }

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
        _spawnPosition = transform.position;
    }

    // 위치를 직접 배치하는 스폰 호출부(SpawnManager 등)가 transform.position을 옮긴 직후 호출.
    // OnEnable은 Get() 직후(=재배치 이전) 호출돼서 거기서 캡처하면 죽기 전 위치가 잡혀버림 —
    // 그래서 재배치가 끝난 다음 이 메서드로 명시적으로 갱신함. (ScenePlaced는 재배치를 안 하므로 호출 불필요)
    public void RefreshSpawnAnchor()
    {
        _spawnPosition = transform.position;
    }

    // 사망 후 풀 반납까지 남은 시간. Die()에서 세팅, OnDying()에서 카운트다운.
    // ※ Die()를 오버라이드하는 서브클래스는 반드시 base.Die()를 호출할 것 — 그래야 이 반납 타이머가 세팅됨.
    private float _deathReturnTimer;

    protected override void Die()
    {
        // 보상은 min~max 범위에서 랜덤 (같은 적이라도 매번 조금씩 다르게). Random.Range(int)는 max 미포함이라 +1.
        int exp  = Random.Range(expRewardMin,  expRewardMax  + 1);
        int gold = Random.Range(goldRewardMin, goldRewardMax + 1);
        // 킬카운트 + 보상(경험치/골드)은 GameManager가 killer(_lastAttacker) 기준으로 분배.
        GameManager.Instance?.OnEnemyKilled(_lastAttacker, exp, gold);

        // 아이템 드랍 — dropChance 확률로 발생. 성공 시 후보 풀 타입 중 랜덤 하나를 꺼내 죽은 자리에 배치.
        // (Random.value는 0~1이라 dropChance=1이면 사실상 항상, 0이면 절대 안 나옴)
        // 픽업은 프리팹에 직렬화된 ItemData로 OnEnable에서 자기 초기화하므로 여기선 Init 불필요.
        if (dropPoolTypes != null && dropPoolTypes.Length > 0 && Random.value < dropChance)
        {
            POOL_TYPE dropType = dropPoolTypes[Random.Range(0, dropPoolTypes.Length)];
            // 아이템 드랍도 네트워크 오브젝트 — Master가 스폰하면 전원에게 동기화(어댑터가 로컬 풀로 라우팅).
            // Die()는 적 소유자(Master, 오프라인은 자기 자신)에서만 도달하므로 여기서 스폰하면 됨.
            // 드랍 픽업 프리팹에도 PhotonView 필요(적과 동일).
            if (_photonView != null)
            {
                PhotonNetwork.Instantiate(dropType.ToString(), transform.position, Quaternion.identity);
            }
            else
            {
                // 비네트워크(PhotonView 없는 싱글 씬배치 적 등)는 기존처럼 로컬 풀 드랍.
                GameObject drop = PoolManager.Instance?.Get(dropType);
                if (drop != null)
                {
                    drop.transform.SetPositionAndRotation(transform.position, Quaternion.identity);
                }
            }
            
        }

        // 풀 반납(SetActive(false))은 사망 애니가 재생되도록 지연 — OnDying()의 타이머로 처리.
        _deathReturnTimer = _deathSequenceDuration;
        base.Die();
    }

    // DIE 상태 동안 매 프레임 호출(Unit.UpdateFSM). 사망 애니 시간만큼 지난 뒤 풀에 반납.
    // Update(FSM) 기반이라 일시정지(ShouldPause) 중엔 자동으로 멈춤 —
    // 코루틴 WaitForSeconds는 timeScale 기준이라 우리의 플래그 방식 일시정지를 무시해 부적합했음.
    protected override void OnDying()
    {
        _deathReturnTimer -= Time.deltaTime;
        if (_deathReturnTimer <= 0f)
        {
            // 네트워크 적(PhotonView 있음)은 소유자(Master)가 PhotonNetwork.Destroy로 전원에게서 반납한다.
            // (PhotonPoolAdapter가 실제 파괴 대신 로컬 풀 SetActive(false)로 라우팅 → OnDisable에서 Unregister)
            // 비네트워크 적(PhotonView 없음 — 싱글 씬배치 등)은 기존처럼 로컬 풀 반납.
            if (_photonView != null)
            {
                if (_photonView.IsMine)
                {
                    PhotonNetwork.Destroy(gameObject);
                }
            }
            else
            {
                // (SetActive(false) → OnDisable() → UnitManager.UnregisterEnemy 자동 호출됨)
                PoolManager.Instance?.Return(gameObject);
            }
        }
    }

    protected override void Update()
    {
        base.Update();
        //일시정지중,죽었을시, AI사용안할시, 남(비Master) 소유 적일시 AI사용안함
        if (ShouldPause || CurState == UNIT_STATE.DIE || !UseGenericAI || !IsMine)
        {
            return;
        }
        UpdateAI();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        // 물리 스핀 방지 — 프리즈(일시정지/게임오버) 중이 아닐 때만.
        // base.FixedUpdate()가 ShouldPause 시 isKinematic=true로 얼리는데, kinematic 바디엔
        // angularVelocity 설정이 불가(에러)하고, 어차피 프리즈 중엔 물리 스핀도 안 생겨 리셋이 불필요.
        if (_rb != null && !_rb.isKinematic) _rb.angularVelocity = Vector3.zero;
        if (ShouldPause || CurState == UNIT_STATE.DIE || !UseGenericAI)
        {
            return;
        }
    }

    // 1초마다 _target 갱신. 자식이 base.UpdateAI() 호출로 공유.
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
        if (_target == null)
        {
            return false;
        }
        Vector3 toEnemy = (transform.position - _target.position).normalized;
        float dot = Vector3.Dot(_target.forward, toEnemy);
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
        _target = UnitManager.Instance.GetNearestPlayer(transform.position);
        _targetUnit = _target != null ? _target.GetComponent<Unit>() : null;
    }

    // 타겟의 현재 위치 + (속도 * 도달시간)으로 예측 조준점 계산.
    // leadAccuracy로 보정(0=예측없음~1=완전예측). bulletSpeed가 0 이하면 예측 안 함(미사일 전용 함선 대비).
    protected Vector3 GetPredictedAimPoint()
    {
        if (_target == null)
        {
            return Vector3.zero;
        }
        if (leadAccuracy <= 0f || _targetUnit == null || weaponSystem == null || weaponSystem.curBulletData == null)
        {
            return _target.position;
        }
        float bulletSpeed = weaponSystem.curBulletData.speed;
        if (bulletSpeed <= 0f)
        {
            return _target.position;
        }

        // 반복 수렴 예측: leadTime을 '현재 위치'가 아니라 '예측 명중점'까지의 거리로 다시 계산하는 걸
        // 몇 번 반복한다. 타겟이 비행 중 이동해 명중점까지 거리가 현재 거리와 달라지는 오차를 없앤다.
        // (1회만 하면 crossing/고속 타겟에서 덜 앞서 조준해 뒤로 빗나감 — 3회면 사실상 참값에 수렴)
        Vector3 predictedPos = _target.position;
        for (int i = 0; i < 3; i++)
        {
            float leadTime = Vector3.Distance(transform.position, predictedPos) / bulletSpeed;
            predictedPos = _target.position + _targetUnit.Velocity * leadTime;
        }
        return Vector3.Lerp(_target.position, predictedPos, leadAccuracy);
    }

    protected bool IsTargetInRange(float range)
    {
        if (_target == null)
        {
            return false;
        }
        return Vector3.Distance(transform.position, _target.position) <= range;
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
        if (_target == null)
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
        if (_target == null)
        {
            return;
        }
        MoveTowardPosition(_target.position);
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
