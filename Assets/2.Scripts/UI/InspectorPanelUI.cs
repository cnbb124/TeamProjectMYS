using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인벤토리 인스펙터 패널.
///
/// [HitPanel]
///   - ShipImage  : HP 비율에 따라 색상 단계 변경 (초록→노랑→주황→빨강)
///   - Name       : 플레이어 기체 이름 표시 (고정 텍스트 or 추후 연동)
///   - Level      : 플레이어 레벨 표시
///   - Gauge      : HP 수치 (cur / max)
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

    [Header("HitPanel")]
    [SerializeField] private Image    shipImage;
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

    private int _lastStage = -1;

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

        UpdateShipImage();
        UpdateHitPanel();
        UpdateStatPanel();
    }

    // ── 기체 이미지 색상 ──────────────────────────────────────

    private void UpdateShipImage()
    {
        if (shipImage == null) return;

        float ratio = player.maxHpRemaining > 0
            ? (float)player.curHpRemaining / player.maxHpRemaining
            : 0f;

        int stage = GetStage(ratio);
        if (stage == _lastStage) return;

        _lastStage      = stage;
        shipImage.color = StageToColor(stage);
    }

    private int GetStage(float ratio)
    {
        if (ratio > 0.75f) return 1;
        if (ratio > 0.50f) return 2;
        if (ratio > 0.25f) return 3;
        return 4;
    }

    private Color StageToColor(int stage)
    {
        switch (stage)
        {
            case 1:  return colorHealthy;
            case 2:  return colorCaution;
            case 3:  return colorWarning;
            default: return colorCritical;
        }
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
