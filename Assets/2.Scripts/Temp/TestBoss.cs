using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 클래스. Enemy 상속.
/// BulletPatternData를 읽어 웨이브 순서대로 탄막 발사.
/// </summary>
public class TestBoss : Enemy
{
    [Header("보스 패턴")]
    [SerializeField] private BulletPatternData[] patterns;  // 페이즈별 패턴 배열
    [SerializeField] private float patternCooldown = 3f;    // 패턴 반복 간격

    [Header("페이즈 전환 HP 비율")]
    [SerializeField] private float phase2Threshold = 0.6f;  // 60% 이하 → 2페이즈
    [SerializeField] private float phase3Threshold = 0.3f;  // 30% 이하 → 3페이즈

    private int _currentPhase   = 0;
    private bool _isShooting    = false;
    private Transform _player;

    protected override void Start()
    {
        base.Start();
        _player = GameObject.FindWithTag("Player")?.transform;
        StartCoroutine(PatternLoop());
    }

    protected override void Update()
    {
        base.Update();
        UpdatePhase();
    }

    // ── 페이즈 전환 ───────────────────────────────────────

    private void UpdatePhase()
    {
        float hpRatio = maxHpRemaining > 0
            ? (float)curHpRemaining / maxHpRemaining : 0f;

        int newPhase = 0;
        if (hpRatio <= phase3Threshold)      newPhase = 2;
        else if (hpRatio <= phase2Threshold) newPhase = 1;

        if (newPhase != _currentPhase)
        {
            _currentPhase = newPhase;
            Debug.Log($"[Boss] 페이즈 {_currentPhase + 1} 진입");
        }
    }

    // ── 패턴 루프 ─────────────────────────────────────────

    private IEnumerator PatternLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(patternCooldown);

            if (patterns == null || patterns.Length == 0) continue;

            // 현재 페이즈에 맞는 패턴 선택 (없으면 마지막 패턴 사용)
            int patternIndex = Mathf.Min(_currentPhase, patterns.Length - 1);
            BulletPatternData data = patterns[patternIndex];

            if (data == null) continue;

            yield return StartCoroutine(ShootPattern(data));
        }
    }

    // ── 패턴 발사 ─────────────────────────────────────────

    private IEnumerator ShootPattern(BulletPatternData data)
    {
        _isShooting = true;

        foreach (PatternWave wave in data.waves)
        {
            yield return new WaitForSeconds(wave.delay);

            foreach (PatternPoint point in wave.points)
            {
                FirePoint(point);
            }
        }

        _isShooting = false;
    }

    // ── 탄 1개 발사 ───────────────────────────────────────

    private void FirePoint(PatternPoint point)
    {
        Vector3 fireDir;
        if (point.aimAtPlayer && _player != null)
        {
            Vector3 toPlayer = (_player.position - transform.position).normalized;
            Quaternion baseRot = Quaternion.LookRotation(toPlayer);
            Vector3 localOffset = new Vector3(point.localDir.x, point.localDir.y, 1f).normalized;
            fireDir = baseRot * localOffset;
        }
        else
        {
            Vector3 localOffset = new Vector3(point.localDir.x, point.localDir.y, 1f).normalized;
            fireDir = transform.rotation * localOffset;
        }

        Projectile proj = PoolManager.Instance.GetBullet();
        if (proj != null)
            proj.Init(transform.position, fireDir, this);
    }
}