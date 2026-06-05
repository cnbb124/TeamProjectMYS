using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FuelGaugeIndicator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player    player;
    [SerializeField] private Rigidbody playerRb;

    [Header("Fuel UI")]
    [SerializeField] private Image    fuelFill;
    [SerializeField] private TMP_Text fuelText;
    [SerializeField] private Image    engineIcon;

    [Header("Warning Signals")]
    [SerializeField] private Image engineLowSignal; // EngineLowSignal Image
    [SerializeField] private Image noFuelSignal;    // NoFuelSignal Image
    [SerializeField] private Color engineLowColor  = new Color(1f, 0.5f, 0f, 1f); // 주홍
    [SerializeField] private Color noFuelColor     = Color.red;
    [SerializeField] private float blinkSpeed      = 3f;   // 경고등 깜빡임 속도
    [SerializeField] [Range(0f, 1f)] private float lowThresholdSignal = 0.2f; // 20%

    [Header("Fuel Settings")]
    [SerializeField] private float maxFuel     = 100f;
    [SerializeField] private float consumeRate = 1.5f;

    [Header("Engine Icon Colors")]
    [SerializeField] private Color fullColor   = Color.green;
    [SerializeField] private Color mediumColor = Color.yellow;
    [SerializeField] private Color lowColor    = Color.red;
    [SerializeField] [Range(0f, 1f)] private float mediumThreshold = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float lowThreshold    = 0.25f;

    [Header("Toggle (G Key)")]
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private float         animDuration = 0.3f;
    [SerializeField] private KeyCode       toggleKey    = KeyCode.G;

    private float     _currentFuel;
    private float     _expandedHeight;
    private bool      _isOpen = true;
    private Coroutine _animCoroutine;
    private int       _cachedFuelInt = -1;

    private void Start()
    {
        _currentFuel    = maxFuel;
        _expandedHeight = panelRect != null ? panelRect.sizeDelta.y : 0f;

        // 경고등 초기 OFF
        SetSignal(engineLowSignal, false, engineLowColor);
        SetSignal(noFuelSignal,    false, noFuelColor);
    }

    private void Update()
    {
        ConsumeFuel();
        UpdateUI();
        UpdateWarningSignals();

        if (Input.GetKeyDown(toggleKey))
            Toggle();
    }

    // ==================== 연료 소모 ====================

    private void ConsumeFuel()
    {
        if (player == null || playerRb == null || _currentFuel <= 0f) return;

        float speedRatio = player.maxSpeed > 0f
            ? Mathf.Clamp01(playerRb.velocity.magnitude / player.maxSpeed)
            : 0f;

        _currentFuel = Mathf.Clamp(
            _currentFuel - consumeRate * speedRatio * Time.deltaTime,
            0f, maxFuel);
    }

    public void Refuel(float amount) =>
        _currentFuel = Mathf.Clamp(_currentFuel + amount, 0f, maxFuel);
    public void RefuelFull()         => _currentFuel = maxFuel;
    public float GetFuelRatio()      => _currentFuel / maxFuel;

    // ==================== UI 업데이트 ====================

    private void UpdateUI()
    {
        float ratio   = _currentFuel / maxFuel;
        int   fuelInt = Mathf.RoundToInt(_currentFuel);

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
        float ratio = _currentFuel / maxFuel;

        // No Fuel (0%)
        bool noFuel = _currentFuel <= 0f;
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

        // Engine Low (20% 이하, No Fuel 아닐 때)
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

    private void SetSignal(Image signal, bool active, Color color)
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
