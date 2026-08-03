using System.Collections;
using Photon.Pun;
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
//   대기는 GameManager.WaitGameplaySeconds(일시정지 안전). 총알 종류/스탯은 _bulletData(BulletData)가 결정,
//   속도는 패턴 포인트별 speed로 덮어씀.
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
    [SerializeField] private Transform[] _firePoints;

    [Tooltip("탄막 총알의 스탯(데미지/사거리/피격VFX/사운드) 데이터. 발사 시 총알에 주입됨.\n" +
             "속도는 이 데이터 대신 패턴 포인트별 speed로 덮어씀. 반드시 지정할 것(비우면 데미지/사거리 0).")]
    [SerializeField] private BulletData _bulletData;

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

    // 페이즈(Master에서만 돎)가 호출 — 랜덤 패턴 인덱스를 골라 전원이 같은 탄막을 재생하게 함.
    // 멀티: RPC로 남 클라에도 같은 패턴을 재생시킴(게스트 총알은 연출용, 데미지는 Master 권위).
    //       BulletPatternData는 결정적 SO라 인덱스만 넘기면 전원이 같은 탄막을 봄(총알당이 아닌 패턴당 RPC 1번).
    // 싱글/룸 밖: 그냥 로컬 재생.
    public void PlayRandomBulletPattern(int phase)
    {
        if (_isFiringPattern)
        {
            return;
        }
        BulletPatternData[] pool = GetPhasePool(phase);
        if (pool == null || pool.Length == 0)
        {
            return;
        }
        int index = Random.Range(0, pool.Length);

        if (_photonView != null && PhotonNetwork.InRoom)
        {
            _photonView.RPC(nameof(RpcPlayPattern), RpcTarget.Others, phase, index);
        }
        // 재생중 검사·풀 조회·범위 검사는 위에서 이미 끝났으므로 고른 패턴만 넘김.
        BeginPattern(pool[index]);
    }

    // 남 클라 수신 — Master가 고른 패턴을 그대로 로컬 재생(연출). 데미지 권위는 FireOneBullet에서 IsMine으로 갈림.
    // 인덱스만 받으므로 풀 조회와 범위 검사는 여기서 함(보낸 쪽과 배열 길이가 다를 수 있음).
    [PunRPC]
    private void RpcPlayPattern(int phase, int index)
    {
        if (_isFiringPattern)
        {
            return;
        }
        BulletPatternData[] pool = GetPhasePool(phase);
        if (pool == null || index < 0 || index >= pool.Length)
        {
            return;
        }
        BeginPattern(pool[index]);
    }

    // 패턴 하나를 로컬에서 재생 시작. 인스펙터 배열에 빈 칸이 있으면 그 회차는 그냥 넘어감.
    private void BeginPattern(BulletPatternData pattern)
    {
        if (pattern == null)
        {
            return;
        }
        StartCoroutine(PlayPatternRoutine(pattern));
    }

    // 페이즈 번호(1~4) → 해당 패턴 풀. RPC로 넘어온 인덱스를 각 클라가 같은 풀에서 찾게 함.
    private BulletPatternData[] GetPhasePool(int phase)
    {
        switch (phase)
        {
            case 1: return _phase1Patterns;
            case 2: return _phase2Patterns;
            case 3: return _phase3Patterns;
            case 4: return _phase4Patterns;
            default: return null;
        }
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
            PlayWaveShootSound(wave);
            foreach (PatternPoint point in wave.points)
            {
                FireOneBullet(point);
            }
        }

        // 다음 패턴까지 쿨다운.
        yield return GameManager.WaitGameplaySeconds(_patternCooldown);
        _isFiringPattern = false;
    }

    // PatternPoint 하나를 실제 발사. 총알 종류(풀)는 _bulletData.curProjectilePoolType이 결정(WeaponSystem과 동일 컨벤션).
    private void FireOneBullet(PatternPoint point)
    {
        if (weaponSystem == null)
        {
            return;
        }

        Vector3 origin = _firePoint != null ? _firePoint.position : transform.position;

        Vector3 localOffset = new Vector3(point.localDir.x, point.localDir.y, 1f).normalized;
        Vector3 fireDir;
        fireDir = transform.rotation * localOffset;
        //if (point.aimAtPlayer && _target != null)
        //{
        //	// 플레이어 방향을 정면으로 삼고 localDir을 오프셋으로 적용.
        //	Quaternion baseRot = Quaternion.LookRotation((_target.position - origin).normalized);
        //	fireDir = baseRot * localOffset;
        //}
        //else
        //{
        // 보스 자신의 방향 기준.

        //}

        // 총알 스폰은 WeaponSystem 공용 코어(SpawnBullet) 재사용 — 풀 선택/데이터 주입/Init/속도덮어씀을 한 곳에서.
        // 스탯/사거리/피격VFX는 _bulletData 주입, 속도는 포인트별 point.speed로 덮어씀(패턴 SO 설계 의도).
        // hasAuthority = IsMine: Master(또는 싱글)만 데미지 권위, 원격 복제본은 SetDamageAuthority(false)로 연출만.
        weaponSystem.SpawnBullet(origin, fireDir, _bulletData, IsMine, point.speed);
    }

    private void FireBulletsAllFirePos(PatternPoint point)
    {
        if (weaponSystem == null)
        {
            return;
        }
        if (_firePoints.Length <= 0)
        {
            return;
        }
        Vector3 localOffset =  new Vector3(point.localDir.x, point.localDir.y, 1f).normalized;

        for (int i = 0; i < _firePoints.Length; ++i)
        {
            Transform firePoint = _firePoints[i];

            if (firePoint == null)
            {
                continue;
            }

            Vector3 origin = firePoint.position;
            Vector3 fireDir = firePoint.rotation * localOffset;

            weaponSystem.SpawnBullet(
                origin,
                fireDir,
                _bulletData,
                IsMine,
                point.speed
            );
        }



        //if (point.aimAtPlayer && _target != null)
        //{
        //	// 플레이어 방향을 정면으로 삼고 localDir을 오프셋으로 적용.
        //	Quaternion baseRot = Quaternion.LookRotation((_target.position - origin).normalized);
        //	fireDir = baseRot * localOffset;
        //}
        //else
        //{
        // 보스 자신의 방향 기준.

        //}

        // 총알 스폰은 WeaponSystem 공용 코어(SpawnBullet) 재사용 — 풀 선택/데이터 주입/Init/속도덮어씀을 한 곳에서.
        // 스탯/사거리/피격VFX는 _bulletData 주입, 속도는 포인트별 point.speed로 덮어씀(패턴 SO 설계 의도).
        // hasAuthority = IsMine: Master(또는 싱글)만 데미지 권위, 원격 복제본은 SetDamageAuthority(false)로 연출만.


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
            _boss.PlayRandomBulletPattern(1);

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
            _boss.PlayRandomBulletPattern(2);
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
            _boss.PlayRandomBulletPattern(3);
        }
    }

    // HP 10~0% (마지막 페이즈 — 더 이상 전환 없음)
    private class BossPhase4 : BossPhase
    {
        public BossPhase4(EnemyBoss boss) : base(boss) { }
        public override void OnUpdate()
        {
            _boss.PlayRandomBulletPattern(4);
        }
    }


    // 보스는 스스로가 보스 스폰의 결과라 킬카운트에 넣지 않음.
    protected override bool CountsTowardKillCount => false;

    protected override void Die()
    {
        base.Die();
        // 보스 처치는 '방' 전체 사건인데 Die()는 적 소유자(Master)에서만 도달함 —
        // 남 클라에도 전파해야 게스트도 보스BGM 복귀/onBossKilled 구독자가 동작함.
        // (RPC는 여기서 즉시 전송되고 보스 오브젝트는 _deathSequenceDuration 뒤에야 풀 반납되므로,
        //  받는 쪽 보스가 아직 살아있어 안전하게 도달함)
        if (_photonView != null && PhotonNetwork.InRoom)
        {
            _photonView.RPC(nameof(RpcBossKilled), RpcTarget.Others);
        }
        GameManager.Instance?.OnBossKilled();
    }

    // 남 클라 수신 — 보스 처치 결과(킬카운트/BGM 복귀/onBossKilled)를 각자 로컬에서 반영.
    [PunRPC]
    private void RpcBossKilled()
    {
        GameManager.Instance?.OnBossKilled();
    }
}
