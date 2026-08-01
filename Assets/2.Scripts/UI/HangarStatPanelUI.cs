/*
 * [HangarStatPanelUI]
 * 격납고 StatPanel — 현재 플레이어가 타고 있는 기체의 스펙(HP/Shield/Armor)을 표시.
 * 격납고 기준이라 "최대치(max)"를 기준 상한(referenceMax)에 대한 비율로 게이지에 채움.
 *
 * [부착 위치]
 * HangarSlot > StatPanel 에 부착. (탭으로 HangarSlot이 켜질 때 OnEnable에서 자동 갱신)
 *
 * [데이터 소스]
 * playerOverride. 비우면 GameManager.Instance.playerRef 사용.
 *
 * [인스펙터 연결 — HP/Shield/Armor 각각]
 * - ~Fill    : Image (Image Type = Filled, Horizontal) — 게이지 막대
 * - ~Text    : 스탯 수치 텍스트 (TMP)
 * - ~Percent : 비율(%) 텍스트 (TMP)
 * referenceMax(상한)는 게이지를 꽉 채우는 기준값. 기체 스펙 비교용 — 인스펙터에서 조정.
 */

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HangarStatPanelUI : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private Player playerOverride;   // 비우면 GameManager.playerRef 사용

    [Header("HP")]
    [SerializeField] private Image    hpFill;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text hpPercent;
    [SerializeField] private float    hpReferenceMax = 150f;

    [Header("Shield")]
    [SerializeField] private Image    shieldFill;
    [SerializeField] private TMP_Text shieldText;
    [SerializeField] private TMP_Text shieldPercent;
    [SerializeField] private float    shieldReferenceMax = 150f;

    [Header("Armor")]
    [SerializeField] private Image    armorFill;
    [SerializeField] private TMP_Text armorText;
    [SerializeField] private TMP_Text armorPercent;
    [SerializeField] private float    armorReferenceMax = 100f;

    // 한 번 잡은 기체. 잡기 전까지만 Update에서 재시도하는 용도.
    private Player _player;

    private void OnEnable()
    {
        // 탭으로 HangarSlot이 켜질 때마다 현재 기체 스탯으로 갱신
        Player.onLoadoutApplied -= HandleLoadoutApplied;
        Player.onLoadoutApplied += HandleLoadoutApplied;
        Refresh();
    }

    private void OnDisable()
    {
        Player.onLoadoutApplied -= HandleLoadoutApplied;
        // 다음에 켜질 때 다시 잡게 함 — 씬이 바뀌면 이전 함선 참조가 죽어 있음
        _player = null;
    }

    // 파츠가 씌워지는 건 스폰 다음 프레임이라, 스폰 직후에 읽으면 프리팹 기본값만 잡힘.
    private void HandleLoadoutApplied(Player player)
    {
        Refresh();
    }

    /// <summary>현재 기체 스탯으로 게이지/텍스트 갱신. 함선이 없으면 PlayerProfile로 계산.</summary>
    public void Refresh()
    {
        Player p = GetPlayer();
        _player = p;

        // 함선이 있으면 그쪽이 정확함(레벨 보너스까지 반영됨).
        // 정거장처럼 함선이 없는 씬에서는 기준 기체 + 장착 파츠로 계산해서 보여줌.
        int hp     = p != null ? p.maxHpRemaining     : PlayerProfile.GetMaxHp();
        int shield = p != null ? p.maxShieldCapacity  : PlayerProfile.GetMaxShield();
        int armor  = p != null ? p.maxArmor           : PlayerProfile.GetMaxArmor();

        if (p == null && !PlayerProfile.HasData)
        {
            return;
        }

        ApplyStat(hpFill,     hpText,     hpPercent,     hp,     hpReferenceMax);
        ApplyStat(shieldFill, shieldText, shieldPercent, shield, shieldReferenceMax);
        ApplyStat(armorFill,  armorText,  armorPercent,  armor,  armorReferenceMax);
    }

    private Player GetPlayer()
    {
        if (playerOverride != null) return playerOverride;
        if (GameManager.Instance != null) return GameManager.Instance.playerRef;
        return null;
    }

    // 게이지(비율) + 수치 텍스트 + 퍼센트 텍스트 한 번에 적용
    private void ApplyStat(Image fill, TMP_Text valueText, TMP_Text percentText, int value, float referenceMax)
    {
        float ratio = referenceMax > 0f ? Mathf.Clamp01(value / referenceMax) : 0f;

        if (fill != null)        fill.fillAmount   = ratio;
        if (valueText != null)   valueText.text    = value.ToString();
        if (percentText != null) percentText.text  = $"{Mathf.RoundToInt(ratio * 100f)}%";
    }
}
