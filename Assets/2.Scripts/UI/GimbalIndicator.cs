using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 짐벌 인디케이터 UI — '기체가 실제로 향하고 있는 방향(기수)'을 표시.
/// 기체 forward를 화면 좌표로 변환해서 찍는다.
///
/// 크로스헤어(CrosshairUI)가 '내가 조종간으로 가리킨 방향'을 즉시 보여주고,
/// 이 마커가 그걸 뒤늦게 쫓아오는 게 조종 지연의 시각 표현임.
/// 늦게 따라오는 정도는 Player의 timeToMaxTurn(회전이 붙는 시간)과
/// Cinemachine 카메라 댐핑이 만들어냄 — 여기서 따로 보간을 걸지 않는다.
///
/// [인스펙터 연결]
/// - player        : 플레이어 기체 Transform (비우면 GameManager.playerRef로 자동 폴백)
/// - mainCam       : Main Camera (비우면 Camera.main으로 자동 폴백)
/// - indicatorRect : 짐벌 인디케이터 RectTransform (비우면 자기 자신)
/// - indicatorImage: 색상 제어용 Image
/// </summary>
public class GimbalIndicatorUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform     player;
    [SerializeField] private Camera        mainCam;
    [SerializeField] private RectTransform indicatorRect;
    [SerializeField] private Image         indicatorImage;

    [Header("Settings")]
    [Tooltip("기수 방향으로 이 거리만큼 앞의 지점을 화면에 투영함.\n" +
             "멀수록 마커가 덜 흔들리고 원근 왜곡이 줄어듦.")]
    [SerializeField] private float aimDistance = 500f;

    [Tooltip("기수 방향 피치 보정(도). +면 마커가 위로.\n" +
             "카메라/총구 오프셋 때문에 탄착점과 안 맞을 때 미세 조정용.")]
    [SerializeField] private float aimPitchOffset = 0f;

    [Header("Color")]
    [SerializeField] private Color normalColor = new Color(0f, 1f, 0.8f, 0.8f);

    [Header("진단용 (원인 파악 끝나면 끌 것)")]
    [Tooltip("켜면 카메라와 기수 사이 각도 / 속도 / 마커 위치를 콘솔에 찍음.\n" +
             "이 각도가 속도에 따라 변하면 카메라 리그 문제, 안 변하는데 마커만 움직이면 UI 변환 문제.")]
    [SerializeField] private bool _debugLog = false;

    [Tooltip("로그 출력 간격(초). 매 프레임 찍으면 콘솔이 넘침.")]
    [SerializeField] private float _debugInterval = 0.5f;

    private float _debugTimer;

    private Canvas _canvas;
    // anchoredPosition은 '부모' 기준 좌표라, 스크린 좌표를 캔버스가 아니라 부모 rect 기준으로 변환해야 함.
    // (이 마커의 부모는 캔버스 루트가 아니라 CrossHairHUD임 — 캔버스 기준으로 넣으면 부모가 움직이는
    //  순간 그만큼 어긋남)
    private RectTransform _parentRect;

    private void Start()
    {
        if (indicatorRect == null)
            indicatorRect = GetComponent<RectTransform>();

        if (indicatorImage != null)
            indicatorImage.color = normalColor;

        _canvas = GetComponentInParent<Canvas>();
        _parentRect = indicatorRect.parent as RectTransform;
    }

    // Update가 아니라 LateUpdate인 이유:
    // Cinemachine Brain이 LateUpdate에서 카메라를 움직이는데, Update에서 카메라를 읽으면
    // '한 프레임 전 카메라'로 계산하게 됨 → 카메라가 회전할 때마다 마커가 어긋났다 맞았다 하며 떨림.
    // 카메라가 제자리를 잡은 뒤에 읽어야 안 떨림.
    private void LateUpdate()
    {
        // 인스펙터 연결 우선, 비어있으면 GameManager.playerRef / Camera.main으로 자동 폴백
        if (player == null && GameManager.Instance != null && GameManager.Instance.playerRef != null)
            player = GameManager.Instance.playerRef.transform;
        if (mainCam == null) mainCam = Camera.main;

        if (player == null || mainCam == null || _canvas == null || indicatorRect == null || _parentRect == null) return;

        UpdatePosition();
    }

    private void UpdatePosition()
    {
        // 기수(forward)에 피치 보정을 적용한 조준 방향
        // player.right 축으로 회전 → 위/아래로 살짝 기울여 탄착점에 맞춤
        Vector3 aimDir = Quaternion.AngleAxis(-aimPitchOffset, player.right) * player.forward;

        // 기체 위치가 아니라 '카메라 위치'에서 그 방향으로 뻗은 지점을 투영함.
        // 총알은 기수 forward 직선으로 나가므로 그 직선 위 어느 점을 찍어도 가리키는 방향은 같은데,
        // 기체 위치 기준으로 잡으면 카메라가 뒤로 처질 때(가속 시 ZDamping) 카메라~기체 거리가 변해
        // 같은 기수 방향인데도 마커가 화면에서 위아래로 흔들림.
        // 카메라 기준으로 잡으면 카메라 이동이 상쇄돼 회전에만 반응함 — 댐핑을 유지한 채 마커가 고정됨.
        Vector3 aimWorldPos = mainCam.transform.position + aimDir * aimDistance;

        // 월드 좌표 → 스크린 좌표
        Vector3 screenPos = mainCam.WorldToScreenPoint(aimWorldPos);

        // 기수가 카메라 뒤를 향하면 x/y가 뒤집혀 나옴 — 그땐 화면 중앙 유지
        if (screenPos.z < 0f)
        {
            indicatorRect.anchoredPosition = Vector2.zero;
            return;
        }

        // 스크린 좌표 → '부모' 로컬 좌표 (anchoredPosition이 부모 기준이라 캔버스 기준으로 넣으면 안 됨)
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parentRect,
            screenPos,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCam,
            out Vector2 localPos);

        // 보간 없이 그대로 — 지연은 기체 회전(timeToMaxTurn)과 카메라 댐핑이 이미 만들고 있음.
        // 여기서 Lerp를 또 걸면 지연이 두 번 겹쳐서 실제 기수와 어긋난 위치를 가리키게 됨.
        indicatorRect.anchoredPosition = localPos;

        LogDiagnostics(aimDir, localPos);
    }

    // 마커가 속도에 따라 움직이는 원인을 가르는 진단용.
    // camPitchDiff  : 카메라가 보는 방향과 기수 방향 사이 상하 각도차.
    //                 속도에 따라 이 값이 변하면 → 카메라 리그(댐핑/조준) 문제.
    //                 이 값이 일정한데 markerY만 변하면 → UI 좌표 변환 문제.
    // camToShip     : 카메라~기체 거리. 이게 속도에 따라 변하면 Body 댐핑이 실제로 밀리고 있다는 뜻.
    private void LogDiagnostics(Vector3 aimDir, Vector2 localPos)
    {
        if (!_debugLog)
        {
            return;
        }

        _debugTimer -= Time.deltaTime;
        if (_debugTimer > 0f)
        {
            return;
        }
        _debugTimer = _debugInterval;

        float camPitchDiff = Vector3.SignedAngle(mainCam.transform.forward, aimDir, mainCam.transform.right);
        float camToShip = Vector3.Distance(mainCam.transform.position, player.position);
        Rigidbody rb = player.GetComponent<Rigidbody>();
        float speed = rb != null ? rb.velocity.magnitude : 0f;

        Debug.Log($"[Gimbal] 속도 {speed:F1} | 카메라↔기수 각도 {camPitchDiff:F2}° | 카메라~기체 거리 {camToShip:F2} | 마커Y {localPos.y:F1}");
    }

    public void SetVisible(bool visible)
    {
        if (indicatorRect != null)
            indicatorRect.gameObject.SetActive(visible);
    }
}
