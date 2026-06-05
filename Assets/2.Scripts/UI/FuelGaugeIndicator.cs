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
    [SerializeField] private Image    fuelFill;    // Fuel Image (Filled)
    [SerializeField] private TMP_Text fuelText;    // 연료 % 텍스트
    [SerializeField] private Image    engineIcon;  // 엔진 아이콘 이미지

    [Header("Fuel Settings")]
    [SerializeField] private float maxFuel     = 100f;
    [SerializeField] private float consumeRate = 1.5f; // 최대속도 기준 초당 소모 %

    [Header("Engine Icon Colors")]
    [SerializeField] private Color fullColor   = Color.green;
    [SerializeField] private Color mediumColor = Color.yellow;
    [SerializeField] private Color lowColor    = Color.red;
    [SerializeField] [Range(0f, 1f)] private float mediumThreshold = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float lowThreshold    = 0.25f;

    [Header("Toggle (Z Key)")]
    [SerializeField] private RectTransform panelRect;     // 접히는 패널
    [SerializeField] private float         animDuration = 0.3f;
    [SerializeField] private KeyCode       toggleKey    = KeyCode.Z;

    private float     _currentFuel;
    private float     _expandedHeight;
    private bool      _isOpen = true;
    private Coroutine _animCoroutine;
    private int       _cachedFuelInt = -1;

    private void Start()
    {
        _currentFuel    = maxFuel;
        _expandedHeight = panelRect != null ? panelRect.sizeDelta.y : 0f;
    }

    private void Update()
    {
        ConsumeFuel();
        UpdateUI();

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

        float consume = consumeRate * speedRatio * Time.deltaTime;
        _currentFuel  = Mathf.Clamp(_currentFuel - consume, 0f, maxFuel);
    }

    // 외부에서 연료 보충 시 호출 (아이템 등)
    public void Refuel(float amount)  => _currentFuel = Mathf.Clamp(_currentFuel + amount, 0f, maxFuel);
    public void RefuelFull()          => _currentFuel = maxFuel;
    public float GetFuelRatio()       => _currentFuel / maxFuel;

    // ==================== UI 업데이트 ====================

    private void UpdateUI()
    {
        float ratio  = _currentFuel / maxFuel;
        int   fuelInt = Mathf.RoundToInt(_currentFuel);

        if (fuelFill != null)
            fuelFill.fillAmount = ratio;

        // 값 바뀔 때만 TMP 갱신
        if (fuelInt != _cachedFuelInt)
        {
            _cachedFuelInt = fuelInt;
            if (fuelText != null) fuelText.text = $"{fuelInt}%";
        }

        // 엔진 아이콘 색상 부드럽게 전환
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

    // ==================== Z키 토글 ====================

    private void Toggle()
    {
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _isOpen = !_isOpen;
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
                Mathf.Lerp(startHeight, targetHeight, t)
            );
            yield return null;
        }

        panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, targetHeight);
    }
}