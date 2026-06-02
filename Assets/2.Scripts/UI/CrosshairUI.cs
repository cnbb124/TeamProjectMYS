using UnityEngine;

public class CrosshairUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform crosshairRect;

    [Header("Scale")]
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float aimScale    = 1.4f;  // 우클릭 시 커지는 배율
    [SerializeField] private float scaleSpeed  = 10f;   // 전환 속도

    [Header("Opacity")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float normalOpacity = 0.8f;
    [SerializeField] private float aimOpacity    = 1f;

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
        bool isAiming = Input.GetMouseButton(1);

        _targetScale   = isAiming ? aimScale    : normalScale;
        _targetOpacity = isAiming ? aimOpacity  : normalOpacity;

        // 스케일 부드럽게 전환
        float currentScale = crosshairRect.localScale.x;
        float nextScale    = Mathf.Lerp(currentScale, _targetScale, scaleSpeed * Time.deltaTime);
        crosshairRect.localScale = Vector3.one * nextScale;

        // 투명도 부드럽게 전환
        if (canvasGroup != null)
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, _targetOpacity, scaleSpeed * Time.deltaTime);
    }
}