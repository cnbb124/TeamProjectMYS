using UnityEngine;
using UnityEngine.UI;

public class GimbalIndicatorUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MissileLockOnSystem lockOnSystem;
    [SerializeField] private RectTransform       indicatorRect;
    [SerializeField] private Image               indicatorImage;

    [Header("Size")]
    [SerializeField] private float baseSize    = 200f; // lockOnAngle 60도 기준 크기
    [SerializeField] private float baseAngle   = 60f;  // 기준 각도

    [Header("Colors")]
    [SerializeField] private Color idleColor      = new Color(0f, 1f, 0.8f, 0.4f); // 기본 청록
    [SerializeField] private Color candidateColor = new Color(1f, 1f, 0f,   0.7f); // 락온 중 노랑
    [SerializeField] private Color lockedColor    = new Color(1f, 0f, 0f,   0.9f); // 락온 완료 빨강

    [Header("Pulse")]
    [SerializeField] private float pulseSpeed     = 3f;
    [SerializeField] private float pulseAmount    = 0.05f; // 크기 진동 폭

    private float _baseDisplaySize;

    private void Start()
    {
        _baseDisplaySize = baseSize;
    }

    private void Update()
    {
        if (lockOnSystem == null) return;

        UpdateSize();
        UpdateColor();
    }

    private void UpdateSize()
    {
        // lockOnAngle 비율에 따라 원 크기 조절
        float sizeRatio = lockOnSystem.lockOnAngle / baseAngle;
        float size      = _baseDisplaySize * sizeRatio;

        // 락온 진행 중일 때 살짝 진동
        if (lockOnSystem.LockOnCandidate != null && !lockOnSystem.IsLocked)
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            size *= (1f + pulse);
        }

        indicatorRect.sizeDelta = Vector2.one * size;
    }

    private void UpdateColor()
    {
        if (lockOnSystem.IsLocked)
            indicatorImage.color = lockedColor;
        else if (lockOnSystem.LockOnCandidate != null)
            indicatorImage.color = Color.Lerp(
                indicatorImage.color, candidateColor, Time.deltaTime * 5f);
        else
            indicatorImage.color = Color.Lerp(
                indicatorImage.color, idleColor, Time.deltaTime * 5f);
    }
}