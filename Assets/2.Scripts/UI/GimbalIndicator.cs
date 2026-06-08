using UnityEngine;
using UnityEngine.UI;

public class GimbalIndicatorUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform     player;
    [SerializeField] private Rigidbody     playerRb;
    [SerializeField] private RectTransform indicatorRect;
    [SerializeField] private Image         indicatorImage;
    [SerializeField] private RectTransform boundaryRect;   // 이동 범위 제한 패널

    [Header("Settings")]
    [SerializeField] private float minSpeed      = 1f;   // 이 속도 이하면 중앙 고정
    [SerializeField] private float trackSpeed    = 8f;   // 추적 부드러움
    [SerializeField] private float indicatorSize = 80f;  // 인디케이터 크기

    [Header("Color")]
    [SerializeField] private Color normalColor = new Color(0f, 1f, 0.8f, 0.8f);

    private Camera  _cam;
    private Vector2 _centerScreenPos;

    private void Start()
    {
        _cam             = Camera.main;
        _centerScreenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        if (indicatorRect  != null) indicatorRect.sizeDelta = Vector2.one * indicatorSize;
        if (indicatorImage != null) indicatorImage.color    = normalColor;
    }

    private void Update()
    {
        if (player == null || playerRb == null || _cam == null) return;

        UpdatePosition();
    }

    private void UpdatePosition()
    {
        Vector2 targetPos;

        // 속도가 충분할 때만 velocity vector 방향으로 이동
        if (playerRb.velocity.magnitude < minSpeed)
        {
            targetPos = _centerScreenPos;
        }
        else
        {
            // 실제 이동 방향 → 화면 좌표 투영
            Vector3 headingWorldPos = player.position + playerRb.velocity.normalized * 100f;
            Vector3 screenPos       = _cam.WorldToScreenPoint(headingWorldPos);

            // 카메라 뒤면 중앙으로
            if (screenPos.z < 0f)
            {
                targetPos = _centerScreenPos;
            }
            else
            {
                targetPos = new Vector2(screenPos.x, screenPos.y);

                // 패널 경계 안으로 클램프
                if (boundaryRect != null)
                {
                    Vector3[] corners = new Vector3[4];
                    boundaryRect.GetWorldCorners(corners);
                    targetPos.x = Mathf.Clamp(targetPos.x, corners[0].x, corners[2].x);
                    targetPos.y = Mathf.Clamp(targetPos.y, corners[0].y, corners[2].y);
                }
            }
        }

        // 부드럽게 이동
        indicatorRect.position = Vector2.Lerp(
            indicatorRect.position,
            targetPos,
            trackSpeed * Time.deltaTime
        );
    }
}
