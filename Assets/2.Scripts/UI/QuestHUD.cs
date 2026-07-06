using UnityEngine;
using TMPro;

// =====================================================================
// QuestUI — 목표(퀘스트) 진행 표시.
// GameManager.onObjectiveChanged를 구독해 "적 처치 N/M", "목표 파괴 N/M"을 갱신.
// 상시 HUD로 켜두거나, 일시정지 창의 버튼(PauseMenuUI.OnQuest)으로 이 패널을 열어 볼 수 있음.
//
// [부착/연결]
//   1. 목표를 표시할 패널 오브젝트에 부착
//   2. objectiveText : 진행 상황을 출력할 TMP_Text 연결
//  BossSpawnTarget: 중간보스/기지 오브젝트에 컴포넌트 부착. killCountToSpawnBoss도 설정(둘 다 >0이어야 AND 발동).
//  QuestHUD: 패널 만들고 TMP_Text 연결 + QuestHUD 부착(objectiveText 연결). 상시 HUD로 켜두거나 pause에서 열기.
//  PauseMenuUI: questPanel에 위 패널 연결 + "목표" 버튼 OnClick → OnQuest().
//  _deathSequenceDuration: 유닛별 사망 애니 길이에 맞게 조정(기본 1.5초).


// =====================================================================
public class QuestHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text objectiveText;

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onObjectiveChanged += Refresh;
        }
        Refresh();   // 켜질 때 즉시 최신값 반영
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onObjectiveChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        if (objectiveText == null || GameManager.Instance == null)
        {
            return;
        }
        GameManager gm = GameManager.Instance;

        string text = "";
        if (gm.KillGoal > 0)
        {
            text += $"적 처치  {Mathf.Min(gm.KillProgress, gm.KillGoal)} / {gm.KillGoal}\n";
        }
        if (gm.BossTargetsTotal > 0)
        {
            text += $"목표 파괴  {gm.BossTargetsDestroyed} / {gm.BossTargetsTotal}\n";
        }
        if (text.Length == 0)
        {
            text = "진행 중인 목표 없음";
        }
        objectiveText.text = text;
    }
}
