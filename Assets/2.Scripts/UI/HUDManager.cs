using UnityEngine;
using UnityEngine.UI;
using TMPro;

// HUD는 전투씬 전용임 — 씬마다 HUD 프리팹이 배치돼 있고, 그릴 대상(플레이어)도 그 씬에만 있다.
// 그래서 싱글톤도 DontDestroyOnLoad도 쓰지 않는다.
// (예전엔 DDOL이라 게임오버/로비 씬까지 HUD가 따라가고, 새 씬의 HUD는 중복이라 파괴됐다)
public class HUDManager : MonoBehaviour
{
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
    [Tooltip("게이지 색 (비율에 따라 자동 변화). 가득참(1) → 빈상태(0) 순서로 평가됨")]
    [SerializeField] private Color nitroColorFull   = new Color(0f,   0.85f, 1f,   1f); // 청록 (가득)
    [SerializeField] private Color nitroColorYellow = new Color(1f,   0.92f, 0.1f, 1f); // 노랑
    [SerializeField] private Color nitroColorOrange = new Color(1f,   0.5f,  0.05f,1f); // 주황
    [SerializeField] private Color nitroColorEmpty  = new Color(1f,   0.15f, 0.1f, 1f); // 빨강 (빔)
    [Tooltip("노랑으로 바뀌기 시작하는 비율 (이 위는 청록)")]
    [Range(0f, 1f)] [SerializeField] private float nitroYellowStop = 0.6f;
    [Tooltip("주황으로 바뀌기 시작하는 비율")]
    [Range(0f, 1f)] [SerializeField] private float nitroOrangeStop = 0.3f;

    [Header("XP")]
    [SerializeField] private Image xpFill;
    [SerializeField] private TMP_Text xpText;
    [SerializeField] private TMP_Text levelDisplay;

    // 인스펙터 연결 우선, 비어있으면 GameManager.playerRef에서 자동 폴백 (찾으면 캐싱)
    private Player GetPlayer()
    {
        if (player == null && GameManager.Instance != null)
            player = GameManager.Instance.playerRef;
        return player;
    }

    void Update()
    {
        if (GetPlayer() == null) return;

        UpdateBar(hpFill,     hpText,     player.curHpRemaining,     player.maxHpRemaining);
        UpdateBar(shieldFill, shieldText, player.curShieldRemaining, player.maxShieldCapacity);
        UpdateBar(armorFill,  armorText,  player.curArmorRemaining,  player.maxArmor);

        if (nitroFill != null)
        {
            float nitroRatio = player.maxBoostCapacity > 0f
                ? player.curBoostRemaining / player.maxBoostCapacity : 0f;
            nitroFill.fillAmount = nitroRatio;

            // 색: 청록 → 노랑 → 주황 → 빨강 4단계 보간
            nitroFill.color = EvaluateNitroColor(nitroRatio);
        }

        if (xpFill != null)
            xpFill.fillAmount = player.expToNextLevel > 0
                ? (float)player.exp / player.expToNextLevel : 0f;

        if (xpText != null)
            xpText.text = $"{player.exp} / {player.expToNextLevel}";

        if (levelDisplay != null)
            levelDisplay.text = $"Lv. {player.level}";
    }

    // 부스트 비율(0~1)에 따라 청록→노랑→주황→빨강 4단계 색 보간
    private Color EvaluateNitroColor(float ratio)
    {
        // 스톱이 뒤집혀 들어와도 안전하게
        float yellowStop = Mathf.Clamp01(nitroYellowStop);
        float orangeStop = Mathf.Clamp01(Mathf.Min(nitroOrangeStop, yellowStop));

        if (ratio >= yellowStop)
            // 청록 ↔ 노랑
            return Color.Lerp(nitroColorYellow, nitroColorFull,
                Mathf.InverseLerp(yellowStop, 1f, ratio));

        if (ratio >= orangeStop)
            // 노랑 ↔ 주황
            return Color.Lerp(nitroColorOrange, nitroColorYellow,
                Mathf.InverseLerp(orangeStop, yellowStop, ratio));

        // 주황 ↔ 빨강
        return Color.Lerp(nitroColorEmpty, nitroColorOrange,
            Mathf.InverseLerp(0f, orangeStop, ratio));
    }

    private void UpdateBar(Image fill, TMP_Text label, int cur, int max)
    {
        if (fill == null) return;
        fill.fillAmount = max > 0 ? (float)cur / max : 0f;
        if (label != null)
            label.text = $"{cur} / {max}";
    }
}
