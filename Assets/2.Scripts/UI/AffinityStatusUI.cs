/*
 * [AffinityStatusUI]
 * 호감도 레벨/등급/게이지/마커 표시. AffectionManager(점수) + AffinityTierTable(레벨/등급) 기반.
 *   - levelText    : "Lv N"
 *   - tierNameText : 등급 이름 (Drifter~Beloved)
 *   - gaugeFill    : 현재 티어 안 진행률 (0~1 채움)
 *   - marker       : 전체 관계 진행도(레벨 1→max)에 따라 트랙 위를 좌→우 이동 + 색 변화
 *
 * [부착] 호감도 표시 영역(상단 바 / Bond 패널 등)
 *
 * [인스펙터 연결]
 * - npc        : NPC_ID
 * - tierTable  : AffinityTierTable 에셋
 * - levelText / tierNameText / gaugeFill : 선택적 (있는 것만 연결)
 * - marker / track : 마커 RectTransform + 기준 트랙 RectTransform (마커는 트랙 좌측 기준 이동)
 * - markerImage / markerGradient : (선택) 진행도에 따른 마커 색 변화
 *
 * [갱신]
 * OnEnable + 외부에서 Refresh() 호출 (AffinityTalkUI.onAffinityChanged에 연결).
 */

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AffinityStatusUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private NPC_ID            npc;
    [SerializeField] private AffinityTierTable tierTable;

    [Header("Text")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text tierNameText;

    [Header("Gauge (현재 티어 진행률)")]
    [SerializeField] private Image gaugeFill;

    [Header("Marker (전체 진행도)")]
    [SerializeField] private RectTransform marker;
    [SerializeField] private RectTransform track;     // 마커가 이동할 기준 레일
    [SerializeField] private Image         markerImage;
    [SerializeField] private Gradient      markerGradient;

    private void OnEnable()
    {
        Refresh();
    }

    /// <summary>현재 호감도로 레벨/등급/게이지/마커 갱신.</summary>
    public void Refresh()
    {
        if (tierTable == null) return;

        int points = AffectionManager.Instance != null
            ? AffectionManager.Instance.GetAffection(npc) : 0;

        int   level = tierTable.GetLevel(points);
        float prog  = tierTable.GetProgress01(points); // 현재 티어 내 0~1

        if (levelText != null)    levelText.text    = $"Lv {level}";
        if (tierNameText != null) tierNameText.text = tierTable.GetTierName(level);
        if (gaugeFill != null)    gaugeFill.fillAmount = prog;

        // 전체 진행도 0~1 (레벨1=0 ~ 만렙=1) → 마커 위치/색
        float overall = tierTable.maxLevel > 1
            ? Mathf.Clamp01((level - 1 + prog) / (tierTable.maxLevel - 1))
            : 1f;

        if (marker != null && track != null)
        {
            float width = track.rect.width;
            Vector2 pos = marker.anchoredPosition;
            pos.x = overall * width;
            marker.anchoredPosition = pos;
        }

        if (markerImage != null && markerGradient != null)
            markerImage.color = markerGradient.Evaluate(overall);
    }
}
