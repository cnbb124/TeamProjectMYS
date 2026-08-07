using Photon.Pun;
using System.Collections;
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
// IsTargetBlocked()                      타겟이 지형지물 뒤에 있는지 (ATTACK_PASS 사격 게이트용)
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

	[Tooltip("드랍이 결정됐을 때 나올 아이템 개수. 개수만큼 위 목록에서 매번 새로 뽑으므로\n" +
			 "여러 종류가 섞여 나올 수 있음. 0이면 아무것도 안 나옴.")]
	public int dropAmount = 1;

	[Tooltip("드랍이 여러 개일 때 출발 지점을 흩뿌릴 반경. 0이면 전부 같은 자리에서 출발해 1개처럼 겹쳐 보임.")]
	public float dropSpreadRadius = 3f;

	
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



	protected Vector3 _spawnPosition;

	// UnitManager 참조. Start에서 1회만 잡고 OnEnable/OnDisable은 이 필드만 씀(풀 재사용 때마다 재취득 안 함).
	private UnitManager _unitManager;



	// ======================AI설정용============================
	protected Transform _target;
	// _target의 Velocity(Rigidbody.velocity) 참조용. UpdateTarget()에서 _target과 함께 갱신.
	protected Unit _targetUnit;
	private float _targetUpdateTimer = 0f;
	private const float TargetUpdateInterval = 1f;
	protected virtual bool UseGenericAI => true;

	// 범용 AI 사용 여부. EnemyWorker처럼 자체 AI를 쓰는 자식은 false로 override.


	// =========================포톤============================

	// 멀티 소유권(_photonView/IsMine)은 base(Unit)로 통일됨. PhotonNetwork.Instantiate로 스폰된 적만 PhotonView를
	// 가지며 Master가 소유(IsMine=true)해 AI를 돌린다. 남(비Master) 클라에선 IsMine=false라 AI를 안 돌리고,
	// 위치는 PhotonTransformView 동기화로만 갱신됨.

	// 적은 공회전/가속/부스트 엔진 루프 사운드를 등록하지 않음 —
	// 유닛당 루프 3개라 적이 늘수록 Unity의 동시재생 보이스(기본 32개) 한도를 넘겨
	// 총소리·폭발음 같은 중요한 소리가 밀려나기 때문. 엔진음은 플레이어만 가짐.
	protected override bool HasEngineSound
	{
		get
		{
			return false;
		}
	}

	protected override void Awake()
	{
		base.Awake();
		CacheSubUnits();
	}

	// 자식 서브유닛(보스·스테이션에 달린 터렛 등) 캐시. 유닛 계층은 런타임에 안 바뀌므로 Awake에서 한 번만 모음.
	// 자식이 없으면 null로 둬서 ReviveSubUnits가 바로 빠져나가게 함 — 자식 없는 적이 대부분이라 그게 기본 경로임.
	private void CacheSubUnits()
	{
		Unit[] found = GetComponentsInChildren<Unit>(true);

		int count = 0;
		for (int i = 0; i < found.Length; i++)
		{
			if (found[i] != this)
			{
				count++;
			}
		}
		if (count == 0)
		{
			_subUnits = null;
			return;
		}

		_subUnits = new Unit[count];
		int index = 0;
		for (int i = 0; i < found.Length; i++)
		{
			if (found[i] != this)
			{
				_subUnits[index] = found[i];
				index++;
			}
		}
	}

	// OnEnable이 Start보다 항상 먼저 호출되므로, 등록은 여기서 — 죽어서 Unregister된 뒤
	// 풀에서 재사용(SetActive(true))될 때도 매번 다시 등록됨. RegisterEnemy는 중복등록 가드 있어 안전.
	// aiState는 STANDBY로 리셋 — 서브클래스(EnemyShip/EnemyTurretBase)가 각자 OnAIStandby/STANDBY 케이스에서
	// 실제 시작 상태(PATROL/RELOAD 등)로 알아서 전환함.
	protected override void OnEnable()
	{
		base.OnEnable();
		aiState = AI_STATE.STANDBY;
		ReviveSubUnits();
		// 캐시된 것만 씀 — 여기서 .Instance를 새로 부르면 매니저 Awake보다 먼저 instance를 선점해
		// 매니저 초기화를 통째로 스킵시킬 수 있음(project_singleton_pattern 규칙).
		// 최초 1회는 아직 null이라 그냥 넘어가고, 바로 뒤의 Start가 등록을 마무리함.
		_unitManager?.RegisterEnemy(this);
		
	}

	// 서브유닛(터렛 등)은 죽을 때 파괴가 아니라 비활성으로 남으므로, 구조물이 풀에서 다시 나올 때 되살려야 함.
	// 안 그러면 재사용된 보스가 터렛이 꺼진 채로 등장함. 대상은 Awake에서 모아둔 자식 Unit들 —
	// VFX/스피커처럼 런타임에 붙는 자식은 Unit이 아니라 애초에 안 들어옴.
	private void ReviveSubUnits()
	{
		if (_subUnits == null)
		{
			return;
		}

		for (int i = 0; i < _subUnits.Length; i++)
		{
			// 파괴된 자식은 건너뜀 — 유니티 가짜 null로 잡힘.
			if (_subUnits[i] == null || _subUnits[i].gameObject.activeSelf)
			{
				continue;
			}
			_subUnits[i].gameObject.SetActive(true);
		}
	}

	protected override void Start()
	{
		base.Start();
		// 매니저 최초 취득은 Start에서만 — 모든 오브젝트의 Awake가 끝난 게 보장되는 시점.
		_unitManager = UnitManager.Instance;
		_unitManager?.RegisterEnemy(this); // RegisterEnemy에 중복등록 가드 있어 OnEnable과 겹쳐도 안전
		UpdateTarget();
		_spawnPosition = transform.position;
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



	// 위치를 직접 배치하는 스폰 호출부(SpawnManager 등)가 transform.position을 옮긴 직후 호출.
	// OnEnable은 Get() 직후(=재배치 이전) 호출돼서 거기서 캡처하면 죽기 전 위치가 잡혀버림 —
	// 그래서 재배치가 끝난 다음 이 메서드로 명시적으로 갱신함.
	public void RefreshSpawnAnchor()
	{
		_spawnPosition = transform.position;
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

	// 타겟이 지형지물 뒤에 있으면 true. 호출부에서 true면 발사를 건너뜀.
	// 락온 후보 목록은 LockOnSystem이 이미 차폐를 걸러내므로 HasTargetInAttackRange()를 보는 상태는
	// 저절로 공격에서 빠짐. 그걸 안 보는 ATTACK_PASS만 이 함수를 직접 씀.
	// 락온 시스템이 없는 적은 차폐 판정 수단이 없으므로 기존대로 발사함(false).
	protected bool IsTargetBlocked()
	{
		if (_target == null)
		{
			return true;
		}
		if (weaponSystem == null || weaponSystem.lockOnSystem == null)
		{
			return false;
		}
		return weaponSystem.lockOnSystem.IsBlockedByObstacle(_target);
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
	// 부모(전함/터렛 거치대 등)가 SetActive(false)되면 자식 터렛도 같이 비활성화되는데,
	// 그 경우 자식 자신의 Die()는 호출되지 않아서 UnitManager 등록이 안 풀리는 문제가 있었음 —
	// OnDisable은 비활성화 원인(자기 사망 vs 부모 cascade) 무관하게 항상 호출되므로 여기서 처리.
	protected override void OnDisable()
	{
		base.OnDisable();
		// 캐시된 참조만 씀 — 플레이 종료 때 매니저가 먼저 파괴되면 Unity의 fake-null로 조용히 스킵됨.
		// 여기서 .Instance를 부르면 없는 매니저를 적 수만큼 다시 찾으면서 로그가 쏟아짐.
		_unitManager?.UnregisterEnemy(this);
	}

	/// <summary>
	/// 이 적이 '네트워크로 스폰된 더 큰 오브젝트의 자식'인지. 보스·스테이션에 달린 터렛이 여기 해당함.
	/// PUN은 스폰 단위를 InstantiationId(루트 뷰의 ID)로 식별하고 계층의 모든 자식 뷰에 같은 값을 박는다 —
	/// 그래서 자기 ViewID와 InstantiationId가 다르면 자식이라는 뜻.
	/// InstantiationId가 0이면 스폰된 게 아니라 씬에 배치된 것이라 루트로 취급함(PUN도 그때는 ViewID로 지움).
	/// </summary>
	private bool IsNetworkSubUnit
	{
		get
		{
			if (_photonView == null)
			{
				return false;
			}
			return _photonView.InstantiationId != 0 && _photonView.ViewID != _photonView.InstantiationId;
		}
	}

	// 전원이 자기 로컬에서 이 서브유닛을 끔. 파괴가 아니라 비활성이라 구조물 루트는 그대로 살아있음.
	// 풀 재사용 시 되살리는 건 ReviveSubUnits()가 담당.
	[PunRPC]
	public void RpcDisableSubUnit()
	{
		gameObject.SetActive(false);
	}

	// 자식 서브유닛 목록. Awake에서 한 번 모으고 이후 안 바뀜. 자식이 없으면 null.
	private Unit[] _subUnits;

	// 사망 후 풀 반납까지 남은 시간. Die()에서 세팅, OnDying()에서 카운트다운.
	// ※ Die()를 오버라이드하는 서브클래스는 반드시 base.Die()를 호출할 것 — 그래야 이 반납 타이머가 세팅됨.
	private float _deathReturnTimer;

	// 아이템 드랍 1회 보장용. 반납이 지연되는 경로에서 OnDying()이 계속 돌아도 중복 드랍 안 되게 막음.
	private bool _deathItemDropped;

	/// <summary>보스 스폰 조건용 킬카운트에 포함되는 적인지.</summary>
	protected virtual bool CountsTowardKillCount => true;

	protected override void Die()
	{
		// 보상은 min~max 범위에서 랜덤 (같은 적이라도 매번 조금씩 다르게). Random.Range(int)는 max 미포함이라 +1.
		int exp = Random.Range(expRewardMin, expRewardMax + 1);
		int gold = Random.Range(goldRewardMin, goldRewardMax + 1);
		// 킬카운트 + 보상(경험치/골드)은 GameManager가 killer(_lastAttacker) 기준으로 분배.
		GameManager.Instance?.OnEnemyKilled(_lastAttacker, exp, gold, CountsTowardKillCount);

		// 풀 반납(SetActive(false))은 사망 애니가 재생되도록 지연 — OnDying()의 타이머로 처리.
		// 아이템 드랍도 같은 타이머를 타서 사망 연출이 끝난 뒤에 나옴 — 실제 스폰은 DropItem()에서.
		_deathReturnTimer = _deathSequenceDuration;
		_deathItemDropped = false;
		base.Die();
	}

	// 아이템 드랍 — dropChance 확률로 발생. 성공하면 dropAmount 개수만큼 후보 풀 타입에서 매번 새로 뽑아
	// '죽인 사람'에게 보냄(월드에 스폰하지 않음 — 받은 클라가 연출용으로 로컬 풀에서 꺼내 씀).
	// (Random.value는 0~1이라 dropChance=1이면 사실상 항상, 0이면 절대 안 나옴)
	// 무엇을 몇 개 줄지는 픽업 프리팹에 직렬화된 ItemData를 ItemPickupVisual이 읽어가므로 여기선 Init 불필요.
	//
	// 호출 시점 = 사망 연출(VFX)이 다 끝나고 풀 반납 직전. 죽은 판정 즉시가 아니라 여기서 하는 이유는
	// 폭발이 터지는 중에 아이템이 먼저 튀어나오면 연출이 끊겨 보이기 때문임.
	private void DropItem()
	{
		if (_deathItemDropped)
		{
			return;
		}
		_deathItemDropped = true;

		// 스폰은 소유자(Master, 오프라인은 자기 자신)만 — 비소유자도 하면 아이템이 중복 생성됨.
		if (_photonView != null && !_photonView.IsMine)
		{
			return;
		}

		if (dropPoolTypes == null || dropPoolTypes.Length == 0 || dropAmount <= 0 || Random.value >= dropChance)
		{
			return;
		}

		
		


		// 받은 클라가 자기 로컬 풀에서 연출용 오브젝트를 꺼내 날리고, 도착 시점에 인벤토리에 넣음.
		// 개수만큼 매번 새로 뽑음 — 같은 적이 여러 종류를 떨굴 수 있음.
		POOL_TYPE[] dropTypes = new POOL_TYPE[dropAmount];
		for (int i = 0; i < dropAmount; i++)
		{
			dropTypes[i] = dropPoolTypes[Random.Range(0, dropPoolTypes.Length)];
		}

		// 배열로 한 번에 넘김 — 개당 따로 보내면 원격 킬일 때 RPC가 개수만큼 나감.
		// 흩뿌릴 좌표는 받는 쪽이 계산함(연출이라 클라마다 달라도 무방) — 좌표 배열까지 보낼 필요 없음.
		GameManager.Instance?.GiveItemDropToKiller(_lastAttacker, dropTypes, transform.position, dropSpreadRadius);







	}

	// DIE 상태 동안 매 프레임 호출(Unit.UpdateFSM). 사망 애니 시간만큼 지난 뒤 풀에 반납.
	// Update(FSM) 기반이라 일시정지(ShouldPause) 중엔 자동으로 멈춤 —
	// 코루틴 WaitForSeconds는 timeScale 기준이라 우리의 플래그 방식 일시정지를 무시해 부적합했음.
	protected override void OnDying()
	{
		_deathReturnTimer -= Time.deltaTime;
		if (_deathReturnTimer <= 0f)
		{
			// 사망 연출이 끝난 시점 — 반납/파괴보다 먼저 해야 드랍 위치(transform.position)를 읽을 수 있음.
			DropItem();

			// 네트워크 적(PhotonView 있음)은 소유자(Master)가 PhotonNetwork.Destroy로 전원에게서 반납한다.
			// (PhotonPoolAdapter가 실제 파괴 대신 로컬 풀 SetActive(false)로 라우팅 → OnDisable에서 Unregister)
			// 비네트워크 적(PhotonView 없음 — 싱글 씬배치 등)은 기존처럼 로컬 풀 반납.
			if (_photonView != null)
			{
				if (_photonView.IsMine)
				{
					// 구조물의 서브유닛(보스·스테이션에 달린 터렛 등)은 PhotonNetwork.Destroy를 쓰면 안 됨 —
					// PUN은 스폰된 오브젝트를 InstantiationId(=루트 뷰 ID)로 식별하는데 자식 뷰도 그 값을 그대로 갖고 있어서,
					// 자식을 지우라고 보내면 남 클라에서 구조물 루트가 통째로 사라짐(멀쩡한 보스가 게스트 화면에서 증발).
					// 그래서 서브유닛은 파괴 대신 전원이 자기 로컬에서 끄게 함. 실제 풀 반납은 루트가 반납될 때 같이 됨.
					if (IsNetworkSubUnit)
					{
						_photonView.RPC(nameof(RpcDisableSubUnit), RpcTarget.AllBuffered);
					}
					else
					{
						PhotonNetwork.Destroy(gameObject);
					}
				}
				else
				{
					// 죽은 뒤 소유권을 잃으면(방장 퇴장 시 컨트롤러가 0으로 돌아감) 아무도 반납하지
					// 않아 시체가 남음 — 로컬에서라도 치움.
					PoolManager.Instance?.Return(gameObject);
				}
			}
			else
			{
				// (SetActive(false) → OnDisable() → UnitManager.UnregisterEnemy 자동 호출됨)
				PoolManager.Instance?.Return(gameObject);
			}
		}
	}
}
