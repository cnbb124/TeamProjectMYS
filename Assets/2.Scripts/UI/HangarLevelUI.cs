/*
 * [HangarLevelUI]
 * 격납고 MenuBarPanel의 LevelUI — 플레이어 레벨/경험치 표시.
 * GameManager.Instance.playerRef의 level/exp/expToNextLevel을 읽어 게이지·텍스트 갱신.
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
        Player p = GameManager.Instance != null ? GameManager.Instance.playerRef : null;
        if (p == null) return;

        if (xpFill != null)
            xpFill.fillAmount = p.expToNextLevel > 0
                ? (float)p.exp / p.expToNextLevel
                : 0f;

        if (levelText != null)
            levelText.text = $"Lv. {p.level}";

        if (xpText != null)
            xpText.text = $"{p.exp} / {p.expToNextLevel}";
    }
}
