using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FPD (Flight Parameter Display) 패널 위치 제어.
/// 크로스헤어 위치를 기준으로 FPD 패널 전체를 이동시킴.
/// PFD, CompassUI, Pitch 등 자식 UI가 전부 따라옴.
///
/// [씬 세팅]
/// FPD (빈 오브젝트, 이 스크립트 부착)
///   ├── PFD 패널
///   ├── Compass 패널
///   └── Pitch 패널
///
/// [인스펙터 연결]
/// - crosshairRect : CrosshairUI의 RectTransform
/// - fpdOffset     : 크로스헤어 기준 위치 오프셋 (기본 0,0 — 인스펙터에서 조절)
/// </summary>
public class FPD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform crosshairRect;

    [Header("설정")]
    [SerializeField] private Vector2 fpdOffset = Vector2.zero;

    private RectTransform _rect;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (crosshairRect == null || _rect == null) return;
        _rect.anchoredPosition = crosshairRect.anchoredPosition + fpdOffset;
    }
}
