using UnityEngine;
using UnityEngine.UI;

public class GimbalIndicatorUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MissileLockOnSystem lockOnSystem;
    [SerializeField] private RectTransform       indicatorRect;
    [SerializeField] private Image               indicatorImage;
    [SerializeField] private RectTransform       boundaryRect;  // 이동 가능한 패널 경계

    [Header("Tracking")]
    [SerializeField] private float trackSpeed   = 8f;   // 추적 속도

    [Header("Size")]
    [SerializeField] private float baseSize     = 200f;
    [SerializeField] private float baseAngle    = 60f;

    [Header("Colors")]
    [SerializeField] private Color idleColor      = new Color(0f, 1f, 0.8f, 0.4f);
    [SerializeField] private Color candidateColor = new Color(1f, 1f, 0f,   0.7f);
    [SerializeField] private Color lockedColor    = new Color(1f, 0f, 0f,   0.9f);

    [Header("Pulse")]
    [SerializeField] private float pulseSpeed   = 3f;
    [SerializeField] private float pulseAmount  = 0.05f;

    private Camera _cam;
    private Vector2 _centerPos; // 화면 중앙 스크린 좌표

    private void Start()
    {
        _cam = Camera.main;
        _centerPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    private void Update()
    {
        if (lockOnSystem == null || _cam == null) return;

        UpdatePosition();
        UpdateSize();
        UpdateColor();
    }

    private void UpdatePosition()
    {
        // 추적할 타겟 결정 (락온 완료 > 후보 > 없으면 중앙)
        Transform target = lockOnSystem.IsLocked
            ? lockOnSystem.LockedTarget
            : lockOnSystem.LockOnCandidate;

        Vector2 targetScreenPos;

        if (target == null)
        {
            targetScreenPos = _centerPos;
        }
        else
        {
            Vector3 screenPos = _cam.WorldToScreenPoint(target.position);

            // 카메라 뒤에 있으면 중앙으로
            if (screenPos.z < 0f)
            {
                targetScreenPos = _centerPos;
            }
            else
            {
                targetScreenPos = new Vector2(screenPos.x, screenPos.y);

                // 패널 경계 안으로 클램프
                if (boundaryRect != null)
                {
                    Vector3[] corners = new Vector3[4];
                    boundaryRect.GetWorldCorners(corners);
                    float minX = corners[0].x;
                    float minY = corners[0].y;
                    float maxX = corners[2].x;
                    float maxY = corners[2].y;

                    targetScreenPos.x = Mathf.Clamp(targetScreenPos.x, minX, maxX);
                    targetScreenPos.y = Mathf.Clamp(targetScreenPos.y, minY, maxY);
                }
            }
        }

        // 부드럽게 이동
        indicatorRect.position = Vector2.Lerp(
            indicatorRect.position,
            targetScreenPos,
            trackSpeed * Time.deltaTime
        );
    }

    private void UpdateSize()
    {
        float sizeRatio = lockOnSystem.lockOnAngle / baseAngle;
        float size      = baseSize * sizeRatio;

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
