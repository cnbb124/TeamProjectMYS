using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    [Header("Target")]
    [SerializeField] private Player player;

    [Header("HP")]
    [SerializeField] private Image hpFill;
    [SerializeField] private TMP_Text hpText;

    [Header("Shield")]
    [SerializeField] private Image shieldFill;
    [SerializeField] private TMP_Text shieldText;

    [Header("Armor")]
    [SerializeField] private Image armorFill;
    [SerializeField] private TMP_Text armorText;

    [Header("Nitro (Boost)")]
    [SerializeField] private Image nitroFill;
    [Tooltip("게이지 충분할 때 색 (채도 높은 청록/파랑)")]
    [SerializeField] private Color nitroFullColor  = new Color(0f, 0.85f, 1f, 1f);
    [Tooltip("게이지 거의 다 닳았을 때 색 (빨강)")]
    [SerializeField] private Color nitroEmptyColor = new Color(1f, 0.15f, 0.1f, 1f);
    [Tooltip("이 비율 아래로 내려가면 빨강으로 변하기 시작 (위는 청록 유지)")]
    [Range(0f, 1f)]
    [SerializeField] private float nitroLowThreshold = 0.35f;

    [Header("XP")]
    [SerializeField] private Image xpFill;
    [SerializeField] private TMP_Text xpText;
    [SerializeField] private TMP_Text levelDisplay;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (player == null) return;

        UpdateBar(hpFill,     hpText,     player.curHpRemaining,     player.maxHpRemaining);
        UpdateBar(shieldFill, shieldText, player.curShieldRemaining, player.maxShieldCapacity);
        UpdateBar(armorFill,  armorText,  player.curArmorRemaining,  player.maxArmor);

        if (nitroFill != null)
        {
            float nitroRatio = player.maxBoostCapacity > 0f
                ? player.curBoostRemaining / player.maxBoostCapacity : 0f;
            nitroFill.fillAmount = nitroRatio;

            // 색: 임계값 위는 청록 유지, 아래로 내려갈수록 빨강으로
            // t = 1(청록) ~ 0(빨강). nitroLowThreshold에서 1, 0에서 0이 되게 remap
            float t = nitroLowThreshold > 0f
                ? Mathf.Clamp01(nitroRatio / nitroLowThreshold) : 1f;
            nitroFill.color = Color.Lerp(nitroEmptyColor, nitroFullColor, t);
        }

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
