using UnityEngine;
using UnityEngine.UI;

public class GimbalIndicatorUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform     player;
    [SerializeField] private Rigidbody     playerRb;
    [SerializeField] private RectTransform indicatorRect;
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
        if (indicatorRect  != null) indicatorRect.sizeDelta  = Vector2.one * indicatorSize;
        if (indicatorImage != null) indicatorImage.color     = normalColor;
        if (indicatorRect  != null) indicatorRect.anchoredPosition = Vector2.zero;
    }

    private void Update()
    {
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

        // 부드럽게 중앙 기준 이동
        indicatorRect.anchoredPosition = Vector2.Lerp(
            indicatorRect.anchoredPosition,
            targetOffset,
            trackSpeed * Time.deltaTime
        );
    }

    public void SetVisible(bool visible)
    {
        if (indicatorRect != null)
            indicatorRect.gameObject.SetActive(visible);
    }
}
