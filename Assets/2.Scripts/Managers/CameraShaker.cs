using UnityEngine;
using Cinemachine;

// =====================================================================
// CameraShaker — Cinemachine Impulse 기반 카메라 흔들림(타격감) 단일 진입점.
//
// [원리]
//   게임플레이 카메라(vcam)의 CinemachineImpulseListener가 이 소스가 쏜 임펄스를 받아 흔들린다.
//   거리 감쇠(멀수록 약함)는 ImpulseSource의 Impulse Definition(Dissipation Distance)이 자동 처리.
//
// [트리거 2종 — 서로 다른 상황 커버]
//   ShakeExplosion(pos, radius) : 폭발 — 반경 비례. Missile.Explode()에서 호출.
//                                 근처에서 큰 폭발이 터지면 데미지를 안 받아도 흔들림("세상의 무게").
//   ShakeDamage(pos, dmg, crit) : 피격 — 받은 데미지 비례(크리면 증폭). Player.OnHitReaction()에서 호출.
//                                 총알 등 폭발이 아닌 피해도 흔들림("내가 맞았다는 피드백").
//
// [에디터 세팅 — 필수]
//   1. 게임플레이 vcam에 Extension > CinemachineImpulseListener 추가 (Gain 1 정도부터 튜닝).
//   2. 이 스크립트가 붙은 오브젝트에 CinemachineImpulseSource 컴포넌트 추가 → _impulseSource에 연결
//      (비워두면 Awake에서 같은 오브젝트의 컴포넌트를 자동 탐색).
//   3. ImpulseSource > Impulse Definition:
//      - Dissipation Distance : 폭발 흔들림이 닿는 거리(게임 스케일에 맞게 조정).
//      - Raw Signal(Noise Profile) : 6D Shake 등 지정 시 더 풍부한 흔들림.
// =====================================================================
public class CameraShaker : MonoBehaviour
{
    // 프로젝트 표준 싱글톤 패턴. DontDestroyOnLoad는 사용 안 함 —
    // ImpulseSource가 게임플레이 씬의 카메라와 짝이라 씬을 넘기면 안 되는 "씬 전용 매니저"(SpawnManager와 동일).
    private static CameraShaker instance;
    // Awake에서만 세팅됨. Awake 전엔 null이므로 최초 접근은 Start부터 할 것.
    // (예전엔 여기서 FindObjectOfType으로 찾아줬는데, 그게 매니저 자신의 Awake보다 먼저
    //  instance를 채워버려서 Awake의 초기화 블록이 통째로 스킵되는 버그를 만들었음)
    public static CameraShaker Instance => instance;

    [Tooltip("비우면 같은 오브젝트의 CinemachineImpulseSource 자동 탐색")]
    [SerializeField] private CinemachineImpulseSource _impulseSource;

    [Header("세기 튜닝")]
    [Tooltip("폭발 반경(x축) → 흔들림 세기(y축) 곡선. 실제 반경대는 대략 클러스터 1~2.5 / 유도 4 / 덤·핵 150.\n" +
        "작은 반경은 낮게, 큰 반경(핵 등)은 급상승하게 그리면 '핵은 확, 잔챙이는 약하게'가 됨.\n" +
        "인스펙터에서 곡선을 직접 조절해 튜닝.")]
    [SerializeField] private AnimationCurve _explosionRadiusToStrength =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(5f, 0.08f),
            new Keyframe(50f, 0.5f),
            new Keyframe(150f, 2.5f));
    [Tooltip("받은 데미지 1당 임펄스 세기")]
    [SerializeField] private float _damageStrengthPerDamage1Point = 0.012f;
    [Tooltip("크리티컬 피격 시 세기 배율")]
    [SerializeField] private float _critMultiplier = 1.6f;
    [Tooltip("임펄스 세기 상한 (과도한 흔들림 방지). 곡선 최고값이 안 잘리게 그보다 높게.")]
    [SerializeField] private float _maxStrength = 3f;
    [Tooltip("이 값 미만 세기는 무시 (미세 흔들림/먼 폭발 컷)")]
    [SerializeField] private float _minStrength = 0.02f;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            // DontDestroyOnLoad 사용 안 함 (씬 전용 매니저 — 위 주석 참고)
        }
        else if (instance != this)
        {
            Debug.LogWarning("중복된 CameraShaker 발견. 파괴 후 실행");
            Destroy(gameObject);
            return;
        }

        if (_impulseSource == null)
        {
            _impulseSource = GetComponent<CinemachineImpulseSource>();
        }
    }

    // 월드 위치에서 임펄스 발생. 거리 감쇠는 Cinemachine이 처리.
    public void ShakeAt(Vector3 worldPos, float strength)
    {
        if (_impulseSource == null || strength < _minStrength)
        {
            return;
        }
        strength = Mathf.Min(strength, _maxStrength);
        // 방향을 매번 랜덤화 → 같은 세기라도 다른 느낌의 흔들림
        Vector3 velocity = Random.onUnitSphere * strength;
        _impulseSource.GenerateImpulseAt(worldPos, velocity);
    }

    // 폭발 — 반경→세기 곡선으로 결정(비선형). 근처 폭발이면 데미지 없어도 흔들림.
    // 곡선을 급상승형으로 그리면 큰 폭발(핵)은 확 세지고 작은 폭발은 약해짐.
    public void ShakeExplosion(Vector3 worldPos, float radius)
    {
        ShakeAt(worldPos, _explosionRadiusToStrength.Evaluate(radius));
    }

    // 피격 — 받은 데미지 비례(크리면 증폭). 카메라 근처(플레이어)에서 발생해 거의 그대로 전달됨.
    public void ShakeDamage(Vector3 worldPos, int damage, bool isCritical)
    {
        float strength = damage * _damageStrengthPerDamage1Point;
        if (isCritical)
        {
            strength *= _critMultiplier;
        }
        ShakeAt(worldPos, strength);
    }
}
