using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Player player;

    [Header("HP")]
    [SerializeField] private Image hpFill;
    [SerializeField] private TMP_Text hpText;

    [Header("Armor")]
    [SerializeField] private Image armorFill;
    [SerializeField] private TMP_Text armorText;

    [Header("Nitro (Boost)")]
    [SerializeField] private Image nitroFill;

    [Header("XP")]
    [SerializeField] private Image xpFill;
    [SerializeField] private TMP_Text xpText;
    [SerializeField] private TMP_Text levelDisplay;

    void Update()
    {
        if (player == null) return;

        UpdateBar(hpFill,    hpText,    player.curHpRemaining,    player.maxHpRemaining);
        UpdateBar(armorFill, armorText, player.curArmorRemaining, player.maxArmor);

        if (nitroFill != null)
            nitroFill.fillAmount = player.maxBoostRemaining > 0f
                ? player.curBoostRemaining / player.maxBoostRemaining : 0f;

        if (xpFill != null)
            xpFill.fillAmount = player.expToNextLevel > 0
                ? (float)player.exp / player.expToNextLevel : 0f;

        if (xpText != null)
            xpText.text = $"{player.exp} / {player.expToNextLevel}";

        if (levelDisplay != null)
            levelDisplay.text = $"Lv. {player.level}";
    }

    private void UpdateBar(Image fill, TMP_Text label, int cur, int max)
    {
        if (fill == null) return;
        fill.fillAmount = max > 0 ? (float)cur / max : 0f;
        if (label != null)
            label.text = $"{cur} / {max}";
    }
}