using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 짐벌 인디케이터 UI.
/// 플레이어 속도 방향을 기준으로 크로스헤어 위치에서 오프셋으로 표시.
/// 속도가 minSpeed 이하면 크로스헤어 위치에 고정.
///
/// [인스펙터 연결]
/// - player        : 플레이어 기체 Transform
/// - playerRb      : 플레이어 Rigidbody
/// - indicatorRect : 짐벌 인디케이터 RectTransform
/// - crosshairRect : CrosshairUI의 RectTransform (기준점)
/// - indicatorImage: 색상 제어용 Image
/// </summary>
public class GimbalIndicatorUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform     player;
    [SerializeField] private Rigidbody     playerRb;
    [SerializeField] private RectTransform indicatorRect;
    [SerializeField] private RectTransform crosshairRect; // 크로스헤어 위치 기준
    [SerializeField] private Image         indicatorImage;

    [Header("Settings")]
    [SerializeField] private float minSpeed      = 1f;    // 이 속도 이하면 중앙 고정
    [SerializeField] private float maxSpeed      = 1000f; // 정규화 기준 최대 속도
    [SerializeField] private float screenRange   = 150f;  // 중앙에서 최대 이탈 거리 (픽셀)
    [SerializeField] private float trackSpeed    = 10f;   // 추적 부드러움
    [SerializeField] private float indicatorSize = 80f;

    [Header("Color")]
    [SerializeField] private Color normalColor = new Color(0f, 1f, 0.8f, 0.8f);

    private void Start()
    {
        if (indicatorImage != null) indicatorImage.color     = normalColor;
        if (indicatorRect  != null) indicatorRect.anchoredPosition = Vector2.zero;
    }

    private void Update()
    {
        // 인스펙터 연결 우선, 비어있으면 GameManager.playerRef에서 자동 폴백
        if (player == null || playerRb == null)
        {
            Player p = GameManager.Instance != null ? GameManager.Instance.playerRef : null;
            if (p != null)
            {
                if (player == null)   player   = p.transform;
                if (playerRb == null) playerRb = p.GetComponent<Rigidbody>();
            }
        }

        if (player == null || playerRb == null) return;

        UpdateFPM();
    }

    private void UpdateFPM()
    {
        Vector2 targetOffset;

        if (playerRb.velocity.magnitude < minSpeed)
        {
            // 속도 없으면 중앙
            targetOffset = Vector2.zero;
        }
        else
        {
            // 속도를 플레이어 로컬 좌표로 변환
            // X = 우측 이탈, Y = 상하 이탈, Z = 전진
            Vector3 localVel = player.InverseTransformDirection(playerRb.velocity);

            float reference = maxSpeed > 0f ? maxSpeed : 1f;

            // Z(전진) 제외하고 X, Y 이탈만 스크린 오프셋으로 매핑
            targetOffset = new Vector2(
                (localVel.x / reference) * screenRange,
                (localVel.y / reference) * screenRange
            );

            // screenRange 범위 안으로 클램프
            if (targetOffset.magnitude > screenRange)
                targetOffset = targetOffset.normalized * screenRange;
        }

        // 크로스헤어 위치 기준으로 오프셋 적용
        Vector2 basePos = crosshairRect != null
            ? crosshairRect.anchoredPosition
            : Vector2.zero;

        indicatorRect.anchoredPosition = Vector2.Lerp(
            indicatorRect.anchoredPosition,
            basePos + targetOffset,
            trackSpeed * Time.deltaTime
        );
    }

    public void SetVisible(bool visible)
    {
        if (indicatorRect != null)
            indicatorRect.gameObject.SetActive(visible);
    }
}
