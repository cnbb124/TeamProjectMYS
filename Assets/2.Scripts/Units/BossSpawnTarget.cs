using UnityEngine;

// =====================================================================
// BossSpawnTarget — 보스 스폰 '파괴 목표' 마커.
// 중간보스 / 기지처럼 "이 특정 오브젝트를 부숴야 보스가 나온다"는 대상에 부착.
//
// 동작:
//   OnEnable()  — GameManager에 자기 등록 (파괴 목표 수 +1)
//   OnDisable() — 파괴(사망 시 SetActive(false) 또는 Destroy) 시 통지 (파괴 목표 달성 +1)
//   → 모든 파괴 목표가 부서지고 + 킬 조건도 충족되면 GameManager가 보스 스폰(AND).
//
// ※ 등록이 Start가 아니라 OnEnable인 이유: 풀에서 재사용되는 오브젝트는 두 번째 활성화부터
//    Start가 호출되지 않아, Start에 두면 재사용 시 등록이 통째로 누락됨
//    (목표 0개 → targetsDone이 항상 true → 킬카운트만으로 보스가 나와버림).
//    OnEnable은 매 활성화마다 호출되므로 풀 스폰/씬 배치 양쪽 다 정상 동작함.
//    (GameManager가 HashSet으로 중복 등록을 걸러주므로 여러 번 불려도 안전)
// =====================================================================
public class BossSpawnTarget : MonoBehaviour
{
    private GameManager _gm;
    // 등록에 성공했을 때만 파괴 통지를 보냄 — 등록 실패(매니저 없음)인데 통지만 나가서
    // 목표 수가 어긋나는 걸 막음.
    private bool _registered;

    private void OnEnable()
    {
        // 매니저가 아직 없으면(초기화 순서) 등록을 건너뜀 — 이 경우 파괴 통지도 안 보냄.
        _gm = GameManager.Instance;
        if (_gm == null)
        {
            _registered = false;
            return;
        }
        _gm.RegisterBossTarget(gameObject);
        _registered = true;
    }

    private void OnDisable()
    {
        if (!_registered)
        {
            return;
        }
        _registered = false;
        // 씬 언로드/앱 종료로 인한 비활성은 GameManager가 자체 플래그로 걸러냄(파괴로 안 셈).
        _gm?.NotifyBossTargetDestroyed(gameObject);
    }
}
