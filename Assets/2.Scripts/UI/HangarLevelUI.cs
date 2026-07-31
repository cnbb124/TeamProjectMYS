/*
 * [HangarLevelUI]
 * 격납고 MenuBarPanel의 LevelUI — 플레이어 레벨/경험치 표시.
 * playerRef의 level/exp/expToNextLevel을 읽어 게이지·텍스트 갱신. 함선이 없으면 PlayerProfile.
 *
 * [부착 위치]
 * LevelUI에 부착.
 *
 * [인스펙터 연결]
 * - xpFill    : LevelGauge (Image, Image Type = Filled, Horizontal) — 경험치 비율 막대
 * - levelText : 레벨 텍스트 (TMP) — "Lv. 3" 형식
 * - xpText    : (선택) 경험치 수치 텍스트 (TMP) — "40 / 100" 형식
 *
 * [동작]
 * - OnEnable(탭/패널 켜질 때) + 매 프레임 갱신 → 레벨업 시 즉시 반영
 */

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HangarLevelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image    xpFill;     // LevelGauge (Filled)
    [SerializeField] private TMP_Text levelText;  // "Lv. N"
    [SerializeField] private TMP_Text xpText;     // (선택) "exp / next"

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        // 격납고에서도 레벨/경험치가 바뀔 수 있으니 매 프레임 갱신 (비용 미미)
        Refresh();
    }

    /// <summary>현재 플레이어의 레벨/경험치로 UI 갱신.</summary>
    public void Refresh()
    {
        // 함선이 없는 씬(정거장)에서는 PlayerProfile 값으로 표시
        Player p = GameManager.Instance != null ? GameManager.Instance.playerRef : null;
        if (p == null && !PlayerProfile.HasData) return;

        int level = p != null ? p.level : PlayerProfile.level;
        int exp = p != null ? p.exp : PlayerProfile.exp;
        int expToNext = p != null ? p.expToNextLevel : PlayerProfile.expToNextLevel;

        if (xpFill != null)
            xpFill.fillAmount = expToNext > 0
                ? (float)exp / expToNext
                : 0f;

        if (levelText != null)
            levelText.text = $"Lv. {level}";

        if (xpText != null)
            xpText.text = $"{exp} / {expToNext}";
    }
}
