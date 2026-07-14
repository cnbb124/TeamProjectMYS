using System.Collections;
using UnityEngine;

// ================================================================
// [EnemyBoss — 보스 전용 AI]
// ================================================================
// EnemyShip을 상속 → 이동/추격/일반공격 AI(aiState)는 그대로 물려받고(A안),
// 그 위에 "HP 페이즈 + 탄막 패턴" 층만 추가한다.
//
// 페이즈(HP 비율): 1[100~70%] → 2[70~40%] → 3[40~10%] → 4[10~0%]
//   - 각 페이즈는 자기 패턴 배열(_phaseNPatterns)에서 랜덤으로 하나 골라 발사.
//   - HP가 다음 임계값 밑으로 내려가면 다음 페이즈로 전환(한 방향).
//   - "페이즈 진입 시 패턴 추가"는 데이터로: 다음 페이즈 배열에 이전 패턴을 같이 넣으면 누적됨.
//
// 탄막 발사: BulletPatternData(웨이브 시퀀스 SO)를 코루틴으로 재생.
//   대기는 GameManager.WaitGameplaySeconds(일시정지 안전). 총알은 PROJECTILE_BULLET 풀에서 발사.
//
// 페이즈는 코드 State 패턴(BossPhase 중첩 클래스)으로 분리 — 상태마다 클래스 하나.
// ================================================================
public class EnemyBoss : EnemyShip
{
	[Header("<size=18>보스 페이즈별 탄막 패턴</size>")]
	[Tooltip("HP 100~70% 구간에서 사용할 패턴들(재생마다 랜덤 선택)")]
	[SerializeField] private BulletPatternData[] _phase1Patterns;
	[Tooltip("HP 70~40% 구간 패턴들")]
	[SerializeField] private BulletPatternData[] _phase2Patterns;
	[Tooltip("HP 40~10% 구간 패턴들")]
	[SerializeField] private BulletPatternData[] _phase3Patterns;
	[Tooltip("HP 10~0% 구간 패턴들")]
	[SerializeField] private BulletPatternData[] _phase4Patterns;

	[Tooltip("패턴 하나 재생이 끝난 뒤 다음 패턴까지 대기 시간(초)")]
	[SerializeField] private float _patternCooldown = 1.5f;

	[Tooltip("탄막 발사 기준 위치(총구). 비우면 보스 본체 위치에서 발사")]
	[SerializeField] private Transform _firePoint;
	
	// 현재 페이즈(State 패턴). 매 프레임 OnUpdate 호출됨.
	private BossPhase _currentPhase;
	
	// 패턴 코루틴이 재생 중인지 — 중복 재생 방지.
	private bool _isFiringPattern;

	/// <summary>현재 HP 비율(0~1). 페이즈 판정용.</summary>
	public float HpRatio => maxHpRemaining > 0 ? (float)curHpRemaining / maxHpRemaining : 0f;
	public bool IsFiringPattern => _isFiringPattern;

	// 풀 재사용(OnEnable은 매 활성화마다 호출)까지 대비해 페이즈/발사상태를 여기서 초기화.
	protected override void OnEnable()
	{
		base.OnEnable();             // Enemy.OnEnable: RegisterEnemy + aiState 리셋
		_isFiringPattern = false;
		SetPhase(new BossPhase1(this)); // 항상 1페이즈부터 시작
	}

	protected override void Update()
	{
		base.Update();               // Enemy/EnemyShip AI(이동·추격·일반공격 유지)
		// 남(비Master) 소유 보스면 탄막 로직을 안 돌린다 — Master만 페이즈/발사를 계산.
		if (ShouldPause || CurState == UNIT_STATE.DIE || !IsMine) return;

		// 타겟이 있을 때만 탄막 로직 가동(플레이어 없으면 발사 안 함).
		if (_target == null) return;
		_currentPhase?.OnUpdate();
	}

	// 페이즈 교체.
	public void SetPhase(BossPhase next)
	{
		_currentPhase?.OnExit();
		_currentPhase = next;
		_currentPhase?.OnEnter();
	}

	// 페이즈가 호출 — 주어진 풀에서 랜덤 패턴 하나를 재생(이미 재생 중이면 무시).
	public void PlayRandomBulletPattern(BulletPatternData[] pool)
	{
		if (_isFiringPattern || pool == null || pool.Length == 0)
		{
			return;
		}
		BulletPatternData pattern = pool[Random.Range(0, pool.Length)];
		if (pattern == null)
		{
			return;
		}
		StartCoroutine(PlayPatternRoutine(pattern));
	}

	// BulletPatternData의 웨이브를 순서대로 재생. 대기는 일시정지 안전(WaitGameplaySeconds).
	private IEnumerator PlayPatternRoutine(BulletPatternData pattern)
	{
		_isFiringPattern = true;

		foreach (PatternWave wave in pattern.waves)
		{
			if (wave.delay > 0f)
			{
				yield return GameManager.WaitGameplaySeconds(wave.delay);
			}
			// 재생 중 사망 시 중단.
			if (CurState == UNIT_STATE.DIE)
			{
				break;
			}
			foreach (PatternPoint point in wave.points)
			{
				FireOneBullet(point);
			}
		}

		// 다음 패턴까지 쿨다운.
		yield return GameManager.WaitGameplaySeconds(_patternCooldown);
		_isFiringPattern = false;
	}

	// PatternPoint 하나를 실제 발사. 총알은 PROJECTILE_BULLET 풀에서 꺼냄(TestBoss와 동일 방식).
	private void FireOneBullet(PatternPoint point)
	{
		if (PoolManager.Instance == null)
		{
			return;
		}

		Vector3 origin = _firePoint != null ? _firePoint.position : transform.position;

		Vector3 localOffset = new Vector3(point.localDir.x, point.localDir.y, 1f).normalized;
		Vector3 fireDir;
		if (point.aimAtPlayer && _target != null)
		{
			// 플레이어 방향을 정면으로 삼고 localDir을 오프셋으로 적용.
			Quaternion baseRot = Quaternion.LookRotation((_target.position - origin).normalized);
			fireDir = baseRot * localOffset;
		}
		else
		{
			// 보스 자신의 방향 기준.
			fireDir = transform.rotation * localOffset;
		}

		Projectile proj = PoolManager.Instance.GetProjectile(POOL_TYPE.PROJECTILE_BULLET);
		if (proj != null)
		{
			proj.Init(origin, fireDir, this);
		}
	}

	// =====================================================================
	// 페이즈 상태들 (코드 State 패턴). 상태마다 클래스 하나.
	// 각 페이즈: 자기 패턴 풀을 랜덤 재생하고, HP가 다음 임계값 밑이면 다음 페이즈로 전환.
	// =====================================================================
	public abstract class BossPhase
	{
		protected EnemyBoss _boss;
		public BossPhase(EnemyBoss boss) { _boss = boss; }
		public virtual void OnEnter() { }
		public abstract void OnUpdate();
		public virtual void OnExit() { }

	}

	// HP 100~70%
	private class BossPhase1 : BossPhase
	{
		public BossPhase1(EnemyBoss boss) : base(boss) { }
		public override void OnUpdate()
		{
			if (_boss.HpRatio <= 0.7f)
			{
				_boss.SetPhase(new BossPhase2(_boss)); return;
			}
			_boss.PlayRandomBulletPattern(_boss._phase1Patterns);
			
		}

	}

	// HP 70~40%
	private class BossPhase2 : BossPhase
	{
		public BossPhase2(EnemyBoss boss) : base(boss) { }
		public override void OnUpdate()
		{
			if (_boss.HpRatio <= 0.4f)
			{
				_boss.SetPhase(new BossPhase3(_boss)); return;
			}
			_boss.PlayRandomBulletPattern(_boss._phase2Patterns);
		}
	}

	// HP 40~10%
	private class BossPhase3 : BossPhase
	{
		public BossPhase3(EnemyBoss boss) : base(boss) { }
		public override void OnUpdate()
		{
			if (_boss.HpRatio <= 0.1f)
			{
				_boss.SetPhase(new BossPhase4(_boss)); return;
			}
			_boss.PlayRandomBulletPattern(_boss._phase3Patterns);
		}
	}

	// HP 10~0% (마지막 페이즈 — 더 이상 전환 없음)
	private class BossPhase4 : BossPhase
	{
		public BossPhase4(EnemyBoss boss) : base(boss) { }
		public override void OnUpdate()
		{
			_boss.PlayRandomBulletPattern(_boss._phase4Patterns);
		}
	}

}
