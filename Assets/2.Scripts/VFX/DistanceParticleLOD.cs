using UnityEngine;

// =====================================================================
// DistanceParticleLOD
//
// 카메라와의 거리에 따라 하위 파티클 부하를 단계적으로 낮추는 컴포넌트.
// 미사일 트레일(2.5-stage missile) 루트나 BoostPos처럼
// 대상 ParticleSystem들이 전부 하위에 모여있는 오브젝트에 부착.
//
// 동작 원리:
//   - Awake에서 하위 전체 PS의 원본 값(rateOverTime/rateOverDistance/버스트/maxParticles/렌더러 상태) 캐싱.
//   - 배율 적용은 항상 이 원본 기준으로 재계산 → 배율이 중첩 적용되지 않고,
//     파티클 간 상대 비율(fire 700 : smoke 100 등)도 안 망가짐.
//   - MinMaxCurve 4개 모드(Constant/TwoConstants/Curve/TwoCurves) 전부 대응.
//   - 버스트("터질 때 한 방에 N개")도 같은 배율로 조절 — 폭발/머즐플래시처럼 원샷 이펙트는
//     rateOverTime이 0이라 이게 없으면 배율을 아무리 낮춰도 전혀 안 줄어듦.
//
// 레벨 구성:
//   HIGH   = 원본 그대로 (별도 설정 없음)
//   MEDIUM = 이미션/최대파티클 배율 + 렌더러 온오프
//   LOW    = 동일 (더 낮은 배율)
//   OFF    = 이미션 0 + 렌더러 끔. 시뮬레이션은 기본적으로 살려둬서
//            살아있는 파티클은 자연 소멸하고, 복귀 시 Play() 없이 그대로 이어짐
//            (원샷 파티클이 멋대로 리플레이되는 문제 원천 차단).
//            _stopWhenOff 켜면 Stop+Clear까지 수행 — 이때는 멈춘 PS만 골라서 복귀 재생.
//
// 풀링 대응: OnEnable(풀 재사용)마다 원본 상태로 복원 후 현재 거리로 즉시 재평가.
//            렌더러 enabled는 SetActive를 넘어 유지되므로 복원 없이는 꺼진 채 재사용될 수 있음.
// 멀티 대응: 순수 로컬 비주얼 처리 — 각 클라이언트가 자기 Camera.main 기준으로만 판단.
//            네트워크 동기화 대상 아님(PhotonView 불필요, 소유권 무관하게 모든 클라에서 동작).
// 부하 분산: 거리 체크는 _checkInterval 간격 + 오브젝트마다 랜덤 위상이라
//            미사일이 수십 발 날아도 같은 프레임에 몰리지 않음.
// =====================================================================
public class DistanceParticleLOD : MonoBehaviour
{
    public enum LOD_LEVEL
    {
        HIGH = 0,
        MEDIUM = 1,
        LOW = 2,
        OFF = 3,
    }

    // MEDIUM/LOW 레벨별 배율 설정 (HIGH는 항상 원본이라 설정 없음)
    [System.Serializable]
    public class LevelSetting
    {
        [Tooltip("이미션 배율 — 원본 rateOverTime/rateOverDistance/버스트 개수 × 이 값")]
        [Range(0f, 1f)] public float emissionMultiplier = 0.5f;

        [Tooltip("Max Particles 배율 — 원본 × 이 값 (최소 1 보장)")]
        [Range(0f, 1f)] public float maxParticlesMultiplier = 0.5f;

        [Tooltip("이 레벨에서 렌더러를 켤지 여부 (원본이 꺼져있던 렌더러는 안 켬)")]
        public bool rendererEnabled = true;
    }

    // 파티클별 원본 값 캐시. 적용은 항상 이 원본 기준 재계산이라 몇 번을 갈아타도 값이 안 변질됨
    private class ParticleCache
    {
        public ParticleSystem ps;
        public ParticleSystemRenderer psRenderer;
        public ParticleSystem.MinMaxCurve baseRateOverTime;
        public ParticleSystem.MinMaxCurve baseRateOverDistance;
        public ParticleSystem.Burst[] baseBursts;   // 원본 버스트. 버스트 안 쓰는 PS면 길이 0
        public int baseMaxParticles;
        public bool baseRendererEnabled;
        public bool stoppedByLOD;   // _stopWhenOff로 이 컴포넌트가 직접 멈춘 PS만 복귀 시 재생
    }

    [Header("거리 기준 (카메라와 본 오브젝트간 거리)")]
    [Tooltip("이 거리 미만이면 HIGH (원본 그대로)")]
    [SerializeField] private float _mediumStartDistance = 150f;

    [Tooltip("이 거리 미만이면 MEDIUM")]
    [SerializeField] private float _lowStartDistance = 400f;

    [Tooltip("이 거리 미만이면 LOW, 이상이면 OFF")]
    [SerializeField] private float _offStartDistance = 800f;

    [Tooltip("가까워져서 레벨이 올라갈 땐 이 여유거리만큼 확실히 들어와야 인정 — 경계에서 파닥거림 방지")]
    [SerializeField] private float _hysteresisMargin = 20f;

    [Header("갱신 주기")]
    [Tooltip("거리 체크 간격(초). 오브젝트마다 랜덤 위상으로 분산됨")]
    [SerializeField] private float _checkInterval = 0.2f;

    [Header("레벨별 설정")]
    [SerializeField] private LevelSetting _mediumSetting = new LevelSetting();

    [SerializeField] private LevelSetting _lowSetting = new LevelSetting
    {
        emissionMultiplier = 0.2f,
        maxParticlesMultiplier = 0.2f,
        rendererEnabled = true,
    };

    [Header("OFF 동작")]
    [Tooltip("OFF에서 Stop+Clear까지 할지. 끄면 이미션 0 + 렌더러만 꺼서 남은 파티클은 자연 소멸시킴")]
    [SerializeField] private bool _stopWhenOff = false;

    private ParticleCache[] _caches;
    private ParticleSystem.Burst[] _burstScratch;   // SetBursts에 넘길 공용 버퍼(매 적용마다 할당 방지)
    private LOD_LEVEL _currentLevel = LOD_LEVEL.HIGH;
    private float _nextCheckTime;
    private bool _initialized;

    private void Awake()
    {
        // 비활성 자식 포함 전체 캐싱 — 나중에 켜지는 파티클도 대상에 포함됨
        ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
        _caches = new ParticleCache[systems.Length];
        int maxBurstCount = 0;

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            ParticleCache cache = new ParticleCache();

            cache.ps = ps;
            cache.psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            cache.baseRateOverTime = ps.emission.rateOverTime;
            cache.baseRateOverDistance = ps.emission.rateOverDistance;
            cache.baseMaxParticles = ps.main.maxParticles;
            cache.baseRendererEnabled = cache.psRenderer != null && cache.psRenderer.enabled;

            int burstCount = ps.emission.burstCount;
            cache.baseBursts = new ParticleSystem.Burst[burstCount];
            if (burstCount > 0)
            {
                ps.emission.GetBursts(cache.baseBursts);
                if (burstCount > maxBurstCount)
                {
                    maxBurstCount = burstCount;
                }
            }

            _caches[i] = cache;
        }

        // 버스트 재설정용 공용 버퍼 — 적용할 때마다 배열을 새로 만들면 GC가 발생하므로 최대 크기로 1번만 잡음
        _burstScratch = new ParticleSystem.Burst[maxBurstCount];

        _initialized = true;
    }

    // 활성화(스폰/풀 재사용)마다 원본 상태로 복원 후 현재 거리 기준으로 즉시 재평가
    private void OnEnable()
    {
        if (!_initialized)
        {
            return;
        }

        _nextCheckTime = Time.time + Random.Range(0f, _checkInterval);

        _currentLevel = LOD_LEVEL.HIGH;
        ApplyLevel(LOD_LEVEL.HIGH);
        EvaluateAndApply(true);
    }

    private void Update()
    {
        if (Time.time < _nextCheckTime)
        {
            return;
        }
        _nextCheckTime = Time.time + _checkInterval;

        EvaluateAndApply(false);
    }

    private void EvaluateAndApply(bool force)
    {
        // 로컬 클라이언트가 실제로 보고 있는 카메라 기준 (멀티에서도 각자 자기 화면 기준)
        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        float distance = Vector3.Distance(cam.transform.position, transform.position);
        LOD_LEVEL target = EvaluateLevel(distance);

        if (!force)
        {
            if (target == _currentLevel)
            {
                return;
            }

            // 디테일 상승(가까워짐) 방향은 히스테리시스 여유거리만큼 확실히 들어와야 전환
            if (target < _currentLevel)
            {
                target = EvaluateLevel(distance + _hysteresisMargin);
                if (target >= _currentLevel)
                {
                    return;
                }
            }
        }

        _currentLevel = target;
        ApplyLevel(target);
    }

    private LOD_LEVEL EvaluateLevel(float distance)
    {
        if (distance < _mediumStartDistance)
        {
            return LOD_LEVEL.HIGH;
        }
        if (distance < _lowStartDistance)
        {
            return LOD_LEVEL.MEDIUM;
        }
        if (distance < _offStartDistance)
        {
            return LOD_LEVEL.LOW;
        }
        return LOD_LEVEL.OFF;
    }

    private void ApplyLevel(LOD_LEVEL level)
    {
        if (_caches == null)
        {
            return;
        }

        for (int i = 0; i < _caches.Length; i++)
        {
            ParticleCache cache = _caches[i];
            if (cache == null || cache.ps == null)
            {
                continue;
            }

            switch (level)
            {
                case LOD_LEVEL.HIGH:
                    ApplyMultiplier(cache, 1f, 1f, true);
                    ResumeIfStoppedByLOD(cache);
                    break;

                case LOD_LEVEL.MEDIUM:
                    ApplyMultiplier(cache, _mediumSetting.emissionMultiplier, _mediumSetting.maxParticlesMultiplier, _mediumSetting.rendererEnabled);
                    ResumeIfStoppedByLOD(cache);
                    break;

                case LOD_LEVEL.LOW:
                    ApplyMultiplier(cache, _lowSetting.emissionMultiplier, _lowSetting.maxParticlesMultiplier, _lowSetting.rendererEnabled);
                    ResumeIfStoppedByLOD(cache);
                    break;

                case LOD_LEVEL.OFF:
                    ApplyOff(cache);
                    break;
            }
        }
    }

    // 원본 캐시 × 배율로 재계산해서 적용 (원본은 절대 수정 안 됨)
    private void ApplyMultiplier(ParticleCache cache, float emissionMultiplier, float maxParticlesMultiplier, bool rendererOn)
    {
        var emission = cache.ps.emission;
        emission.rateOverTime = ScaleCurve(cache.baseRateOverTime, emissionMultiplier);
        emission.rateOverDistance = ScaleCurve(cache.baseRateOverDistance, emissionMultiplier);
        ApplyBursts(cache, emissionMultiplier);

        var main = cache.ps.main;
        main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(cache.baseMaxParticles * maxParticlesMultiplier));

        if (cache.psRenderer != null)
        {
            cache.psRenderer.enabled = cache.baseRendererEnabled && rendererOn;
        }
    }

    // 버스트 개수를 원본 × 배율로 재계산해서 적용. 시각(time)/반복(cycleCount·repeatInterval)/확률은 원본 그대로 둠.
    // 폭발·머즐플래시 같은 원샷 이펙트는 rateOverTime이 0이고 버스트로만 뿜기 때문에 이걸 안 건드리면 LOD가 무효임.
    // ⚠️ 버스트는 PS가 재생을 시작하는 순간 한 번에 나가므로, 이 값은 재생 전에 정해져 있어야 함
    //    (OnEnable에서 즉시 재평가하는 이유 — 풀에서 꺼내 재생되기 전에 레벨이 확정됨).
    private void ApplyBursts(ParticleCache cache, float multiplier)
    {
        int count = cache.baseBursts.Length;
        if (count == 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            ParticleSystem.Burst burst = cache.baseBursts[i];
            burst.count = ScaleBurstCount(burst.count, multiplier);
            _burstScratch[i] = burst;
        }

        var emission = cache.ps.emission;
        emission.SetBursts(_burstScratch, count);
    }

    private void ApplyOff(ParticleCache cache)
    {
        // 이미션 0 + 렌더러 끔. 시뮬레이션은 살려둬서 남은 파티클 자연 소멸 → 복귀 시 Play() 불필요
        ApplyMultiplier(cache, 0f, 1f, false);

        if (_stopWhenOff && cache.ps.isPlaying)
        {
            cache.stoppedByLOD = true;
            cache.ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    // OFF에서 이 컴포넌트가 직접 멈춘 PS만 재생 재개.
    // 이미 자연 종료된 원샷 파티클(발사 플래시 등)은 stoppedByLOD가 안 찍혀서 리플레이 안 됨.
    private void ResumeIfStoppedByLOD(ParticleCache cache)
    {
        if (cache.stoppedByLOD)
        {
            cache.stoppedByLOD = false;
            cache.ps.Play(false);
        }
    }

    // 버스트 개수 전용 배율 — 배율이 0보다 크면 최소 1개는 남김(원본이 1개 이상일 때).
    // 안 그러면 3개짜리 작은 버스트가 LOW(0.2배)에서 0.6 → 0이 돼 그 파티클만 통째로 사라져 이펙트가 어색해짐.
    // maxParticles에 Mathf.Max(1, ...)을 두는 것과 같은 취지.
    private static ParticleSystem.MinMaxCurve ScaleBurstCount(ParticleSystem.MinMaxCurve original, float multiplier)
    {
        ParticleSystem.MinMaxCurve scaled = ScaleCurve(original, multiplier);

        // OFF(배율 0)는 진짜로 0개여야 하므로 하한을 적용하지 않음
        if (multiplier <= 0f)
        {
            return scaled;
        }

        switch (original.mode)
        {
            case ParticleSystemCurveMode.Constant:
                if (original.constant >= 1f && scaled.constant < 1f)
                {
                    scaled.constant = 1f;
                }
                break;

            case ParticleSystemCurveMode.TwoConstants:
                if (original.constantMax >= 1f && scaled.constantMax < 1f)
                {
                    scaled.constantMax = 1f;
                }
                break;
        }

        return scaled;
    }

    // MinMaxCurve 모드별 배율 적용 — 원본 struct 복사본에 배율만 반영 (곡선 에셋 자체는 공유, 수정 안 함)
    private static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve original, float multiplier)
    {
        ParticleSystem.MinMaxCurve scaled = original;

        switch (original.mode)
        {
            case ParticleSystemCurveMode.Constant:
                scaled.constant = original.constant * multiplier;
                break;

            case ParticleSystemCurveMode.TwoConstants:
                scaled.constantMin = original.constantMin * multiplier;
                scaled.constantMax = original.constantMax * multiplier;
                break;

            default:
                // Curve / TwoCurves — 곡선 형태는 유지하고 배율만 조절
                scaled.curveMultiplier = original.curveMultiplier * multiplier;
                break;
        }

        return scaled;
    }
}
