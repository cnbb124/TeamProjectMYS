using UnityEngine;

/// <summary>
/// 크로스헤어 UI.
/// 플레이어 기체의 forward 방향을 화면 좌표로 변환해 크로스헤어를 실제 조준점에 표시.
/// 기관총 발사 시 크로스헤어 확대, 투명도 변경.
///
/// [인스펙터 연결]
/// - crosshairRect : 크로스헤어 RectTransform
/// - player        : 플레이어 기체 Transform
/// - mainCam       : Main Camera
/// - canvasGroup   : 투명도 제어용 CanvasGroup
/// - aimDistance   : 조준점까지의 거리 (기본 500)
/// </summary>
public class CrosshairUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform crosshairRect;
    [SerializeField] private Transform     player;
    [SerializeField] private Camera        mainCam;

    [Header("Scale")]
    [SerializeField] private float normalScale  = 1f;
    [SerializeField] private float firingScale  = 1.4f;
    [SerializeField] private float scaleSpeed   = 10f;

    [Header("Opacity")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float normalOpacity = 0.8f;
    [SerializeField] private float aimOpacity    = 1f;

    [Header("Aim")]
    [SerializeField] private float aimDistance    = 500f; // 조준점까지 거리
    [SerializeField] private float aimPitchOffset = 0f;   // 조준 피치 보정(도). +면 조준점이 위로,
                                                          // 카메라/총구 오프셋 때문에 탄착이 안 맞을 때 미세 조정

    private Canvas _canvas;
    private float _targetScale;
    private float _targetOpacity;

    private void Start()
    {
        if (crosshairRect == null)
            crosshairRect = GetComponent<RectTransform>();

        _canvas        = GetComponentInParent<Canvas>();
        _targetScale   = normalScale;
        _targetOpacity = normalOpacity;
    }

    private void Update()
    {
        UpdatePosition();

        bool isFiring  = Input.GetMouseButton(0);
        _targetScale   = isFiring ? firingScale  : normalScale;
        _targetOpacity = isFiring ? aimOpacity   : normalOpacity;

        float currentScale = crosshairRect.localScale.x;
        float nextScale    = Mathf.Lerp(currentScale, _targetScale, scaleSpeed * Time.deltaTime);
        crosshairRect.localScale = Vector3.one * nextScale;

        if (canvasGroup != null)
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, _targetOpacity, scaleSpeed * Time.deltaTime);
    }

    private void UpdatePosition()
    {
        // 인스펙터 연결 우선, 비어있으면 자동 폴백 (playerRef / Camera.main)
        if (player == null && GameManager.Instance != null && GameManager.Instance.playerRef != null)
            player = GameManager.Instance.playerRef.transform;
        if (mainCam == null) mainCam = Camera.main;

        if (player == null || mainCam == null || _canvas == null) return;

        // 기수(forward)에 피치 보정을 적용한 조준 방향
        // player.right 축으로 회전 → 위/아래로 살짝 기울여 탄착점에 맞춤
        Vector3 aimDir = Quaternion.AngleAxis(-aimPitchOffset, player.right) * player.forward;

        // 보정된 방향으로 aimDistance 만큼 앞의 월드 좌표
        Vector3 aimWorldPos = player.position + aimDir * aimDistance;

        // 월드 좌표 → 스크린 좌표
        Vector3 screenPos = mainCam.WorldToScreenPoint(aimWorldPos);

        // 기체가 카메라 뒤에 있으면 화면 중앙 유지
        if (screenPos.z < 0f)
        {
            crosshairRect.anchoredPosition = Vector2.zero;
            return;
        }

        // 스크린 좌표 → Canvas 로컬 좌표
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.GetComponent<RectTransform>(),
            screenPos,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCam,
            out Vector2 localPos);

        // anchoredPosition으로 통일 (카메라 뒤 경로·GimbalIndicator 기준점과 일치시킴)
        crosshairRect.anchoredPosition = localPos;
    }
}