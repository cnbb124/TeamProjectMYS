using UnityEngine;

// =====================================================================
// BossSpawnTarget — 보스 스폰 '파괴 목표' 마커.
// 중간보스 / 기지처럼 "이 특정 오브젝트를 부숴야 보스가 나온다"는 대상에 부착.
//
// 동작:
//   Start()     — GameManager에 자기 등록 (파괴 목표 수 +1)
//   OnDisable() — 파괴(사망 시 SetActive(false) 또는 Destroy) 시 통지 (파괴 목표 달성 +1)
//   → 모든 파괴 목표가 부서지고 + 킬 조건도 충족되면 GameManager가 보스 스폰(AND).
//
// 
// ※ 풀링으로 재사용되는 오브젝트가 아닌, 씬당 한 번 죽는 목표(중간보스/기지)에 적합.
// =====================================================================
public class BossSpawnTarget : MonoBehaviour
{
    private GameManager _gm;

    private void Start()
    {
        _gm = GameManager.Instance;
        _gm?.RegisterBossTarget(gameObject);
    }

    private void OnDisable()
    {
        //등록된 경우만 통지됨(내부에서 집합 체크).
        _gm?.NotifyBossTargetDestroyed(gameObject);
    }
}
