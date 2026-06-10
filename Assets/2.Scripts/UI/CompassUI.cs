using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 나침반(Yaw) + 피치 래더(Pitch) UI.
/// CompassUI 오브젝트에 부착.
///
/// [씬 세팅]
/// Compass 오브젝트  : RectMask2D 부착 → 자식으로 CompassTape (빈 오브젝트) 생성
/// Pitch 오브젝트    : RectMask2D 부착 → 자식으로 PitchTape   (빈 오브젝트) 생성
/// </summary>
public class CompassUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;

    [Header("Compass (Yaw — 가로)")]
    [SerializeField] private RectTransform compassRect;         // Compass 오브젝트
    [SerializeField] private RectTransform compassTape;         // CompassTape 자식 오브젝트
    [SerializeField] private float compassPixelsPerDegree = 3f; // 1도당 픽셀
    [SerializeField] private int   compassTickInterval    = 30; // 눈금 간격 (도)

    [Header("Pitch (세로)")]
    [SerializeField] private RectTransform pitchRect;           // Pitch 오브젝트
    [SerializeField] private RectTransform pitchTape;           // PitchTape 자식 오브젝트
    [SerializeField] private float pitchPixelsPerDegree  = 3f;
    [SerializeField] private int   pitchTickInterval     = 10;  // 눈금 간격 (도)

    [Header("Tick Style")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Color tickColor   = new Color(0f, 1f, 0.8f, 1f); // 청록
    [SerializeField] private int   fontSize    = 10;

    // 방위 이름
    private static readonly Dictionary<int, string> _cardinals = new Dictionary<int, string>
    {
        { 0,   "N" }, { 90,  "E" }, { 180, "S" }, { 270, "W" },
        { 360, "N" }
    };

    private void Start()
    {
        BuildCompassTape();
        BuildPitchTape();
    }

    private void Update()
    {
        if (player == null) return;

        UpdateCompass();
        UpdatePitch();
    }

    // ── 나침반 테이프 생성 ────────────────────────────────────

    /// <summary>
    /// 0~360도 눈금을 두 벌(-360~+720) 생성해 좌우 이동 시 끊김 없이 보이도록 함.
    /// </summary>
    private void BuildCompassTape()
    {
        if (compassTape == null) return;

        // -360 ~ +720 범위로 두 벌 생성 (wraparound 대응)
        for (int deg = -360; deg <= 720; deg += compassTickInterval)
        {
            int normalDeg = ((deg % 360) + 360) % 360;
            string label  = _cardinals.ContainsKey(normalDeg)
                ? _cardinals[normalDeg]
                : normalDeg.ToString();

            CreateTick(compassTape, label,
                new Vector2(deg * compassPixelsPerDegree, 0f),
                isVertical: false);
        }
    }

    // ── 피치 테이프 생성 ──────────────────────────────────────

    private void BuildPitchTape()
    {
        if (pitchTape == null) return;

        for (int deg = -90; deg <= 90; deg += pitchTickInterval)
        {
            string label = deg == 0 ? "—" : deg.ToString();
            CreateTick(pitchTape, label,
                new Vector2(0f, deg * pitchPixelsPerDegree),
                isVertical: true);
        }
    }

    // ── 눈금 오브젝트 생성 ────────────────────────────────────

    private void CreateTick(RectTransform parent, string label,
                            Vector2 anchoredPos, bool isVertical)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = isVertical ? new Vector2(28f, 16f) : new Vector2(24f, 20f);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text                = label;
        tmp.fontSize            = fontSize;
        tmp.color               = tickColor;
        tmp.alignment           = TextAlignmentOptions.Center;
        tmp.enableWordWrapping  = false;
        if (font != null) tmp.font = font;
    }

    // ── 나침반 업데이트 ───────────────────────────────────────

    private void UpdateCompass()
    {
        if (compassTape == null) return;

        float yaw = player.eulerAngles.y; // 0~360
        // 테이프를 Yaw 반대 방향으로 이동 (yaw 증가 → 테이프 왼쪽으로)
        compassTape.anchoredPosition = new Vector2(
            -yaw * compassPixelsPerDegree,
            compassTape.anchoredPosition.y);
    }

    // ── 피치 업데이트 ─────────────────────────────────────────

    private void UpdatePitch()
    {
        if (pitchTape == null) return;

        // eulerAngles.x는 0~360 → -180~+180으로 변환
        float pitch = player.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;

        // pitch 증가(기수 상승) → 테이프 아래로
        pitchTape.anchoredPosition = new Vector2(
            pitchTape.anchoredPosition.x,
            -pitch * pitchPixelsPerDegree);
    }
}
