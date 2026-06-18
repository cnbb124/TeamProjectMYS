using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인벤토리 인스펙터 패널.
///
/// [HitPanel]
///   - PlanePanel/PanelImage 하위 4개 파트 이미지 색상으로 파손 상태 표시
///     · Cockpit  → Armor 비율
///     · Body     → Armor 비율
///     · EngineCore → HP 비율
///     · Thruster → HP 비율
///   - Name  : 플레이어 기체 이름 표시
///   - Level : 플레이어 레벨 표시
///   - Gauge : HP 수치 (cur / max)
///
/// [StatPanel]
///   - HPView     : HP 바 + 텍스트
///   - ShieldView : Shield 바 + 텍스트
///   - ArmorView  : Armor 바 + 텍스트
/// </summary>
public class InspectorPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;

    [Header("HitPanel — 파트 이미지 (PlanePanel > PanelImage 하위)")]
    [SerializeField] private Image cockpitImage;    // Armor 비율
    [SerializeField] private Image bodyImage;       // Armor 비율
    [SerializeField] private Image engineCoreImage; // HP 비율
    [SerializeField] private Image thrusterImage;   // HP 비율
    [SerializeField] private Image bulletPosImage;  // HP 비율
    [SerializeField] private Image missilePosImage; // HP 비율

    [Header("HitPanel — 텍스트")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text gaugeText;   // HP 수치 (cur / max)

    [Header("StatPanel — HP")]
    [SerializeField] private Image    hpFill;
    [SerializeField] private TMP_Text hpText;

    [Header("StatPanel — Shield")]
    [SerializeField] private Image    shieldFill;
    [SerializeField] private TMP_Text shieldText;

    [Header("StatPanel — Armor")]
    [SerializeField] private Image    armorFill;
    [SerializeField] private TMP_Text armorText;

    [Header("단계별 색상")]
    [SerializeField] private Color colorHealthy  = new Color(0.0f, 1.0f, 0.2f); // 초록  100~75%
    [SerializeField] private Color colorCaution  = new Color(1.0f, 0.9f, 0.0f); // 노랑   75~50%
    [SerializeField] private Color colorWarning  = new Color(1.0f, 0.5f, 0.0f); // 주황   50~25%
    [SerializeField] private Color colorCritical = new Color(1.0f, 0.1f, 0.1f); // 빨강   25~ 0%

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (player == null) return;

        UpdatePartImages();
        UpdateHitPanel();
        UpdateStatPanel();
    }

    // ── 파트별 이미지 색상 ────────────────────────────────────

    private void UpdatePartImages()
    {
        float hpRatio    = player.maxHpRemaining > 0
            ? (float)player.curHpRemaining / player.maxHpRemaining : 0f;
        float armorRatio = player.maxArmor > 0
            ? (float)player.curArmorRemaining / player.maxArmor : 0f;

        SetPartColor(cockpitImage,    armorRatio);
        SetPartColor(bodyImage,       armorRatio);
        SetPartColor(engineCoreImage, hpRatio);
        SetPartColor(thrusterImage,   hpRatio);
        SetPartColor(bulletPosImage,  hpRatio);
        SetPartColor(missilePosImage, hpRatio);
    }

    private void SetPartColor(Image img, float ratio)
    {
        if (img == null) return;
        img.color = RatioToColor(ratio);
    }

    private Color RatioToColor(float ratio)
    {
        if (ratio > 0.75f) return colorHealthy;
        if (ratio > 0.50f) return colorCaution;
        if (ratio > 0.25f) return colorWarning;
        return colorCritical;
    }

    // ── HitPanel 텍스트 ───────────────────────────────────────

    private void UpdateHitPanel()
    {
        if (levelText != null)
            levelText.text = $"Lv. {player.level}";

        if (gaugeText != null)
            gaugeText.text = $"{player.curHpRemaining} / {player.maxHpRemaining}";
    }

    // ── StatPanel 바 ──────────────────────────────────────────

    private void UpdateStatPanel()
    {
        UpdateBar(hpFill,     hpText,     player.curHpRemaining,     player.maxHpRemaining);
        UpdateBar(shieldFill, shieldText, player.curShieldRemaining, player.maxShieldCapacity);
        UpdateBar(armorFill,  armorText,  player.curArmorRemaining,  player.maxArmor);
    }

    private void UpdateBar(Image fill, TMP_Text label, int cur, int max)
    {
        if (fill != null)
            fill.fillAmount = max > 0 ? (float)cur / max : 0f;

        if (label != null)
            label.text = $"{cur} / {max}";
    }
}
