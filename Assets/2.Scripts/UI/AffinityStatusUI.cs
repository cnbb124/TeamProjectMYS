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
 * OnEnable에서 AffectionManager.OnAffectionChanged 구독 + 최초 1회 Refresh().
 * 호감도가 어떤 경로(대화/퀘스트/상점/세이브로드)로 바뀌든 매니저 이벤트로 통지받아 자동 갱신.
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

    // AffectionManager 참조. Start에서 1회만 잡고 OnEnable/OnDisable은 이 필드만 씀.
    private AffectionManager _affectionManager;

    // 매니저 최초 취득은 Start에서만 — Awake/OnEnable에서 .Instance를 부르면 매니저 자신의 Awake보다
    // 먼저 instance를 선점해서, 매니저 Awake의 초기화가 통째로 스킵됨.
    private void Start()
    {
        _affectionManager = AffectionManager.Instance;
        if (_affectionManager != null)
            _affectionManager.OnAffectionChanged += HandleAffectionChanged;
        Refresh();
    }

    private void OnEnable()
    {
        // 캐시된 것만 씀. 최초 1회는 아직 null이라 넘어가고 바로 뒤의 Start가 구독을 마무리함.
        if (_affectionManager != null)
            _affectionManager.OnAffectionChanged += HandleAffectionChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (_affectionManager != null)
            _affectionManager.OnAffectionChanged -= HandleAffectionChanged;
    }

    // 내가 표시하는 NPC의 호감도가 바뀐 경우에만 갱신
    private void HandleAffectionChanged(NPC_ID changed, int value)
    {
        if (changed == npc) Refresh();
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
