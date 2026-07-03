using UnityEngine;

// =====================================================================
// EffectAutoReturn
//
// 파티클 기반 이펙트 프리팹 루트에 부착.
// ParticleSystem 설정(Stop Action, Duration, Emission OFF 등)을
// Awake에서 자동으로 처리하므로 인스펙터에서 별도 설정 불필요.
//
// 반납 경로 2중화:
//   ① OnParticleSystemStopped 콜백 — 정상 종료 시 즉시 반납
//   ② 타임아웃 폴백 — 활성화 후 (최장 파티클 수명 + 여유시간)이 지나도록 콜백이
//      안 오면 강제 반납. Pause/PauseAndCatchup cullingMode에서 화면 밖 파티클이
//      얼어붙어 콜백이 영영 안 오는 경우에도 풀 반납이 보장됨.
//
// 파티클 담당자가 할 일: 루트 오브젝트에 이 컴포넌트 하나만 부착하면 끝.
// effectType은 VFXManager.PlayEffect() 호출 시 자동 주입.
// =====================================================================
public class EffectAutoReturn : MonoBehaviour
{
    [HideInInspector]
    public EFFECT_TYPE effectType;

    [Tooltip("파티클 종료 콜백이 안 와도 (최장 파티클 수명 + 이 여유시간(초)) 뒤엔 강제 반납")]
    [SerializeField] private float _timeoutMargin = 0.5f;

    private float _maxLifetime;   // 자식 PS 중 최장 수명(duration + startLifetime). 더미 duration + 타임아웃 반납 기준.
    private float _returnAt;      // 이 시각 지나면 콜백 없이도 강제 반납

    private void Awake()
    {
        // 원샷 반납 전용: 루트 포함 모든 하위 PS의 loop 강제 off.
        // 하나라도 loop면 종료 콜백(OnParticleSystemStopped)이 영원히 안 와서 반납이 안 됨(풀 손실).
        // (true) = 비활성 자식도 포함해 나중에 켜져도 안전.
        ParticleSystem[] allSystems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem child in allSystems)
        {
            var childMain = child.main;
            childMain.loop = false;
        }

        // 자식 PS 중 최장 수명 계산 (더미 PS duration + 타임아웃 반납 양쪽에 사용).
        // 더미 PS 추가 전에 계산해야 자기 자신(더미)이 안 섞임.
        _maxLifetime = 0f;
        foreach (ParticleSystem child in allSystems)
        {
            float dur = child.main.duration + child.main.startLifetime.constantMax;
            if (dur > _maxLifetime)
            {
                _maxLifetime = dur;
            }
        }

        ParticleSystem ps = GetComponent<ParticleSystem>();

        if (ps == null)
        {
            // 루트에 PS 없음 → 종료 콜백 트리거용 더미 PS 자동 생성
            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var emission = ps.emission;
            emission.enabled = false;   // 파티클 발생 없음

            var shape = ps.shape;
            shape.enabled = false;      // 발사 형태 없음

            ParticleSystemRenderer psRenderer = GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null)
            {
                psRenderer.enabled = false;   // 렌더링 없음
            }

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = true;
            main.duration = Mathf.Max(_maxLifetime, 0.1f);
            main.stopAction = ParticleSystemStopAction.Callback;
        }
        else
        {
            // 루트에 PS 이미 있음 → Stop Action과 Loop만 강제 설정
            var main = ps.main;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Callback;
        }
    }

    // 활성화(스폰/풀 재사용)마다 타임아웃 재설정.
    private void OnEnable()
    {
        _returnAt = Time.time + _maxLifetime + _timeoutMargin;
    }

    // 폴백: 콜백이 안 와도 타임아웃 지나면 강제 반납(Pause 계열 cullingMode 대비).
    private void Update()
    {
        if (Time.time >= _returnAt)
        {
            ReturnToPool();
        }
    }

    // 정상 종료 콜백.
    private void OnParticleSystemStopped()
    {
        ReturnToPool();
    }

    // 반납. 이중 호출(콜백+타임아웃)돼도 VFXManager.ReturnEffect가 activeInHierarchy로 가드하므로 안전.
    private void ReturnToPool()
    {
        if (VFXManager.Instance == null)
        {
            return;
        }
        VFXManager.Instance.ReturnEffect(effectType, gameObject);
    }
}
