using UnityEngine;

// =====================================================================
// EffectAutoReturn
//
// 파티클 기반 이펙트 프리팹 루트에 부착.
// ParticleSystem 설정(Stop Action, Duration, Emission OFF 등)을
// Awake에서 자동으로 처리하므로 인스펙터에서 별도 설정 불필요.
//
// 파티클 담당자가 할 일:
//   루트 오브젝트에 이 컴포넌트 하나만 부착하면 끝.
//
// effectType은 VFXManager.PlayEffect() 호출 시 자동 주입.
// =====================================================================
public class EffectAutoReturn : MonoBehaviour
{
    [HideInInspector]
    public EFFECT_TYPE effectType;

    private void Awake()
    {
        // 원샷 반납 전용 컴포넌트: 루트 포함 모든 하위 PS의 loop를 강제로 끔.
        // 하나라도 loop면 종료 콜백(OnParticleSystemStopped)이 영원히 안 와서 반납이 안 됨(풀 손실).
        // (true) = 비활성 자식도 포함해 나중에 켜져도 안전.
        ParticleSystem[] allSystems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem child in allSystems)
        {
            var childMain = child.main;
            childMain.loop = false;
        }

        ParticleSystem ps = GetComponent<ParticleSystem>();

        if (ps == null)
        {
            // 자식 ParticleSystem 중 가장 긴 Duration 자동 계산
            // 더미 PS를 추가하기 전에 계산해야 자기 자신(더미)이 섞이지 않음
            float maxDuration = 0f;
            ParticleSystem[] childSystems = GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem child in childSystems)
            {
                float dur = child.main.duration + child.main.startLifetime.constantMax;
                if (dur > maxDuration)
                {
                    maxDuration = dur;
                }
            }

            // 루트에 PS 없음 → 더미 PS 자동 생성
            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // 파티클 발생 없음
            var emission = ps.emission;
            emission.enabled = false;

            // 발사 형태 없음
            var shape = ps.shape;
            shape.enabled = false;

            // 렌더링 없음
            ParticleSystemRenderer psRenderer = GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null)
            {
                psRenderer.enabled = false;
            }

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = true;
            main.duration = Mathf.Max(maxDuration, 0.1f);
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

    private void OnParticleSystemStopped()
    {
        if (VFXManager.Instance == null)
        {
            return;
        }

        VFXManager.Instance.ReturnEffect(effectType, gameObject);
    }
}
