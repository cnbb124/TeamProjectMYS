using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FuelGaugeIndicator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;

    [Header("Fuel UI")]
    [SerializeField] private Image    fuelFill;
    [SerializeField] private TMP_Text fuelText;
    [SerializeField] private Image    engineIcon;

    [Header("Warning Signals")]
    [SerializeField] private TMP_Text engineLowSignal;
    [SerializeField] private TMP_Text noFuelSignal;
    [SerializeField] private Color engineLowColor  = new Color(1f, 0.5f, 0f, 1f); // 주홍
    [SerializeField] private Color noFuelColor     = Color.red;
    [SerializeField] private float blinkSpeed      = 3f;
    [SerializeField] [Range(0f, 1f)] private float lowThresholdSignal = 0.2f; // 20%

    [Header("Engine Icon Colors")]
    [SerializeField] private Color fullColor   = Color.green;
    [SerializeField] private Color mediumColor = Color.yellow;
    [SerializeField] private Color lowColor    = Color.red;
    [SerializeField] [Range(0f, 1f)] private float mediumThreshold = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float lowThreshold    = 0.25f;

    [Header("Toggle (InputManager - G Key)")]
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private float         animDuration = 0.3f;

    private float     _expandedHeight;
    private bool      _isOpen = true;
    private Coroutine _animCoroutine;
    private int       _cachedFuelInt = -1;

    private void Start()
    {
        _expandedHeight = panelRect != null ? panelRect.sizeDelta.y : 0f;

        SetSignal(engineLowSignal, false, engineLowColor);
        SetSignal(noFuelSignal,    false, noFuelColor);
    }

    private void Update()
    {
        // 인스펙터 연결 우선, 비어있으면 GameManager.playerRef에서 자동 폴백
        if (player == null && GameManager.Instance != null)
            player = GameManager.Instance.playerRef;

        if (player == null) return;

        UpdateUI();
        UpdateWarningSignals();

        if (InputManager.Instance != null && InputManager.Instance.fuelGaugeToggle)
            Toggle();
    }

    // ==================== UI 업데이트 ====================

    private void UpdateUI()
    {
        float ratio   = player.maxFuelCapacity > 0f
            ? player.curFuelRemaining / player.maxFuelCapacity
            : 0f;
        int fuelInt = Mathf.RoundToInt(ratio * 100f);

        if (fuelFill != null)
            fuelFill.fillAmount = ratio;

        if (fuelInt != _cachedFuelInt)
        {
            _cachedFuelInt = fuelInt;
            if (fuelText != null) fuelText.text = $"{fuelInt}%";
        }

        // 엔진 아이콘 색상
        if (engineIcon != null)
        {
            Color target;
            if (ratio > mediumThreshold)
                target = Color.Lerp(mediumColor, fullColor,
                    (ratio - mediumThreshold) / (1f - mediumThreshold));
            else if (ratio > lowThreshold)
                target = Color.Lerp(lowColor, mediumColor,
                    (ratio - lowThreshold) / (mediumThreshold - lowThreshold));
            else
                target = lowColor;

            engineIcon.color = Color.Lerp(engineIcon.color, target, Time.deltaTime * 5f);
        }
    }

    // ==================== 경고등 ====================

    private void UpdateWarningSignals()
    {
        float ratio  = player.maxFuelCapacity > 0f
            ? player.curFuelRemaining / player.maxFuelCapacity
            : 0f;
        bool noFuel  = player.curFuelRemaining <= 0f;

        if (noFuelSignal != null)
        {
            noFuelSignal.gameObject.SetActive(noFuel);
            if (noFuel)
            {
                Color c = noFuelColor;
                c.a = Mathf.Abs(Mathf.Sin(Time.time * blinkSpeed));
                noFuelSignal.color = c;
            }
        }

        bool engineLow = !noFuel && ratio <= lowThresholdSignal;
        if (engineLowSignal != null)
        {
            engineLowSignal.gameObject.SetActive(engineLow);
            if (engineLow)
            {
                Color c = engineLowColor;
                c.a = Mathf.Abs(Mathf.Sin(Time.time * blinkSpeed));
                engineLowSignal.color = c;
            }
        }
    }

    private void SetSignal(TMP_Text signal, bool active, Color color)
    {
        if (signal == null) return;
        signal.gameObject.SetActive(active);
        signal.color = color;
    }

    // ==================== G키 토글 ====================

    private void Toggle()
    {
        if (panelRect == null)
        {
            Debug.LogWarning("[FuelGaugeIndicator] panelRect가 연결되지 않았습니다!");
            return;
        }

        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _isOpen        = !_isOpen;
        _animCoroutine = StartCoroutine(Animate(_isOpen ? _expandedHeight : 0f));
    }

    private IEnumerator Animate(float targetHeight)
    {
        float startHeight = panelRect.sizeDelta.y;
        float elapsed     = 0f;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
            panelRect.sizeDelta = new Vector2(
                panelRect.sizeDelta.x,
                Mathf.Lerp(startHeight, targetHeight, t));
            yield return null;
        }

        panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, targetHeight);
    }
}
