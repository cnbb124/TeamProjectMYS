using UnityEngine;

/// <summary>
/// 크로스헤어 UI — '내가 조종간으로 가리킨 방향'을 표시.
/// 마우스/패드 스틱 기울기를 화면 중앙 기준 오프셋으로 그대로 찍는다. 보간 없이 즉시 반영됨.
/// 기관총 발사 시 크로스헤어 확대, 투명도 변경.
///
/// 기체가 실제로 향한 방향(기수)은 GimbalIndicatorUI가 표시한다 —
/// 그쪽이 이 크로스헤어를 뒤늦게 쫓아오는 게 보여야 조종 지연이 눈에 드러남.
///
/// [인스펙터 연결]
/// - crosshairRect  : 크로스헤어 RectTransform
/// - canvasGroup    : 투명도 제어용 CanvasGroup
/// - aimScreenRange : 조종간을 최대로 꺾었을 때 화면 중앙에서 벗어나는 거리(px)
/// </summary>
public class CrosshairUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform crosshairRect;

    [Header("Scale")]
    [SerializeField] private float normalScale  = 1f;
    [SerializeField] private float firingScale  = 1.4f;
    [SerializeField] private float scaleSpeed   = 10f;

    [Header("Opacity")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float normalOpacity = 0.8f;
    [SerializeField] private float aimOpacity    = 1f;

    [Header("Aim")]
    [Tooltip("조종간을 최대로 꺾었을 때 화면 중앙에서 벗어나는 거리(픽셀).\n" +
             "클수록 마우스 입력이 크게 보임. 화면 절반보다 크면 크로스헤어가 화면 밖으로 나감.")]
    [SerializeField] private float aimScreenRange = 220f;

    private float _targetScale;
    private float _targetOpacity;

    private void Start()
    {
        if (crosshairRect == null)
            crosshairRect = GetComponent<RectTransform>();

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
        if (crosshairRect == null || InputManager.Instance == null)
            return;

        // 조종간 기울기(-1~1)를 화면 중앙 기준 오프셋으로 변환.
        // Lerp를 걸지 않음 — 이건 '입력 그 자체'라 한 프레임도 늦으면 안 됨.
        // 월드 좌표 변환도 필요 없음(입력은 애초에 화면 기준 값이라 카메라 뒤 문제도 없음).
        crosshairRect.anchoredPosition = InputManager.Instance.MouseStick * aimScreenRange;
    }
}
