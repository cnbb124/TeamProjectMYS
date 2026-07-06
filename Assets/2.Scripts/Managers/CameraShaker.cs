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
    public static CameraShaker Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<CameraShaker>();
                if (instance == null)
                {
                    Debug.Log("씬에 CameraShaker 누락! 하이어라키에 추가 필요");
                }
            }
            return instance;
        }
    }

    [Tooltip("비우면 같은 오브젝트의 CinemachineImpulseSource 자동 탐색")]
    [SerializeField] private CinemachineImpulseSource _impulseSource;

    [Header("세기 튜닝")]
    [Tooltip("폭발 반경 1당 임펄스 세기")]
    [SerializeField] private float _explosionStrengthPerRadius = 0.06f;
    [Tooltip("받은 데미지 1당 임펄스 세기")]
    [SerializeField] private float _damageStrengthPerHp = 0.012f;
    [Tooltip("크리티컬 피격 시 세기 배율")]
    [SerializeField] private float _critMultiplier = 1.6f;
    [Tooltip("임펄스 세기 상한 (과도한 흔들림 방지)")]
    [SerializeField] private float _maxStrength = 1.5f;
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

    // 폭발 — 반경 비례. 근처 폭발이면 데미지 없어도 흔들림.
    public void ShakeExplosion(Vector3 worldPos, float radius)
    {
        ShakeAt(worldPos, radius * _explosionStrengthPerRadius);
    }

    // 피격 — 받은 데미지 비례(크리면 증폭). 카메라 근처(플레이어)에서 발생해 거의 그대로 전달됨.
    public void ShakeDamage(Vector3 worldPos, int damage, bool isCritical)
    {
        float strength = damage * _damageStrengthPerHp;
        if (isCritical)
        {
            strength *= _critMultiplier;
        }
        ShakeAt(worldPos, strength);
    }
}
