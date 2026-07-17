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

    // GameManager 참조. Start에서 1회만 잡고 OnEnable/OnDisable은 이 필드만 씀.
    private GameManager _gameManager;

    // 매니저 최초 취득은 Start에서만 — Awake/OnEnable에서 .Instance를 부르면 매니저 자신의 Awake보다
    // 먼저 instance를 선점해서, 매니저 Awake의 초기화 블록(itemDatabase.Init 등)이 통째로 스킵됨.
    private void Start()
    {
        _gameManager = GameManager.Instance;
        if (_gameManager != null)
        {
            _gameManager.onObjectiveChanged += Refresh;
        }
        Refresh();
    }

    private void OnEnable()
    {
        // 캐시된 것만 씀. 최초 1회는 아직 null이라 그냥 넘어가고 바로 뒤의 Start가 구독을 마무리함.
        if (_gameManager != null)
        {
            _gameManager.onObjectiveChanged += Refresh;
        }
        Refresh();   // 켜질 때 즉시 최신값 반영
    }

    private void OnDisable()
    {
        if (_gameManager != null)
        {
            _gameManager.onObjectiveChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        if (objectiveText == null || _gameManager == null)
        {
            return;
        }
        GameManager gm = _gameManager;

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
