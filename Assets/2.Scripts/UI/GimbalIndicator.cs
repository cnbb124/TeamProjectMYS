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
    [Tooltip("총구에서 이 거리만큼 앞의 지점을 화면에 투영함 = '몇 거리에서 맞출 것인가'.\n" +
             "총알이 실제로 지나가는 선 위의 점이라, 이 거리에서는 마커에 정확히 맞음.\n" +
             "주로 교전하는 거리를 넣을 것. 이보다 가깝거나 멀면 조금씩 어긋남.")]
    [SerializeField] private float aimDistance = 500f;

    [Tooltip("기수 방향 피치 보정(도). +면 마커가 위로.\n" +
             "카메라/총구 오프셋 때문에 탄착점과 안 맞을 때 미세 조정용.")]
    [SerializeField] private float aimPitchOffset = 0f;

    [Tooltip("추가 미세보정(픽셀). X=좌우, Y=상하(음수면 아래로).\n" +
             "총구 기준 투영이 이미 자동으로 맞춰주므로 보통 0으로 둘 것.\n" +
             "그래도 눈으로 볼 때 조금 어긋나면 이걸로 밀면 됨.")]
    [SerializeField] private Vector2 aimScreenOffset = Vector2.zero;

    [Header("Color")]
    [SerializeField] private Color normalColor = new Color(0f, 1f, 0.8f, 0.8f);

    private Canvas _canvas;
    // anchoredPosition은 '부모' 기준 좌표라, 스크린 좌표를 캔버스가 아니라 부모 rect 기준으로 변환해야 함.
    // (이 마커의 부모는 캔버스 루트가 아니라 CrossHairHUD임 — 캔버스 기준으로 넣으면 부모가 움직이는
    //  순간 그만큼 어긋남)
    private RectTransform _parentRect;

    // 회전 보정값(Player.AimRotation)을 읽기 위한 참조. player Transform과 같은 오브젝트.
    // 없으면(=Player가 아닌 걸 물려놨으면) 보정 없이 raw 회전으로 폴백함 — 이땐 떨림이 남음.
    private Player _playerUnit;

    // 총구 위치를 얻기 위한 참조. 총구는 파츠 장착 시 런타임으로 붙어서 여기 등록됨.
    private WeaponSystem _weapons;

    /// <summary>
    /// 총구가 카메라보다 아래에 있어서 생기는 화면상 어긋남(픽셀).
    /// 이 마커는 이미 반영해서 그려지고, 크로스헤어가 이 값을 받아 같이 내려가면 둘이 겹침.
    /// 총구를 못 찾으면 0 — 그땐 예전처럼 카메라 기준으로 그려지므로 보정할 것도 없음.
    /// </summary>
    public Vector2 MuzzleParallaxOffset { get; private set; }

    private void Start()
    {
        if (indicatorRect == null)
            indicatorRect = GetComponent<RectTransform>();

        if (player != null)
        {
            _playerUnit = player.GetComponent<Player>();
            _weapons = player.GetComponent<WeaponSystem>();
        }

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
        {
            player = GameManager.Instance.playerRef.transform;
            _playerUnit = GameManager.Instance.playerRef;
            _weapons = GameManager.Instance.playerRef.weaponSystem;
        }
        if (mainCam == null) mainCam = Camera.main;

        if (player == null || mainCam == null || _canvas == null || indicatorRect == null || _parentRect == null) return;

        UpdatePosition();
    }

    private void UpdatePosition()
    {
        // 기수(forward)에 피치 보정을 적용한 조준 방향
        // 기체의 right 축으로 회전 → 위/아래로 살짝 기울여 탄착점에 맞춤
        //
        // transform.rotation을 직접 안 쓰고 Player.AimRotation을 쓰는 이유:
        // 기체 회전은 FixedUpdate(50Hz)에서만 갱신되는데 카메라는 렌더 프레임마다 갱신됨.
        // 이 마커는 '기수와 카메라의 각도차'로 위치가 정해지므로 그 주기 차이가 계단 떨림으로 드러남.
        // AimRotation은 물리 스텝 사이 경과시간만큼 앞질러 계산한 값이라 프레임 단위로 매끄러움.
        Quaternion aimRot = _playerUnit != null ? _playerUnit.AimRotation : player.rotation;
        Vector3 aimDir = Quaternion.AngleAxis(-aimPitchOffset, aimRot * Vector3.right) * (aimRot * Vector3.forward);

        // 투영 원점 = '총구'. 총알이 실제로 지나가는 선 위의 점을 찍어야 마커가 진짜 탄착을 가리킴.
        // 카메라 원점으로 잡으면 카메라 위치가 상쇄돼 버려서, 총구가 카메라보다 아래라는 사실이
        // 반영되지 않고 마커가 항상 실제 탄착보다 위에 뜬다.
        // 총구가 없으면(무기 미장착) 예전처럼 카메라 원점으로 폴백 — 방향만이라도 맞게.
        Vector3 muzzlePos = mainCam.transform.position;   // 폴백: 총구를 못 찾으면 카메라 원점
        bool hasMuzzle = false;
        if (_weapons != null && _weapons.TryGetMuzzleCenter(out Vector3 foundMuzzle))
        {
            muzzlePos = foundMuzzle;
            hasMuzzle = true;
        }

        if (!TryProjectToLocal(muzzlePos + aimDir * aimDistance, out Vector2 localPos))
        {
            // 기수가 카메라 뒤를 향하면 x/y가 뒤집혀 나옴 — 그땐 화면 중앙 유지
            // (보정 오프셋은 그대로 유지 — 안 그러면 이 순간에만 마커가 튐)
            indicatorRect.anchoredPosition = aimScreenOffset;
            return;
        }

        // 크로스헤어에게 넘길 어긋남 = (총구 기준 위치) - (카메라 기준 위치).
        // 크로스헤어는 조종간 기울기를 화면에 그대로 찍는 거라 총구 기준으로 바꿀 대상이 없어서,
        // 이 차이만큼 같이 내려줘야 둘이 겹친다.
        if (hasMuzzle && TryProjectToLocal(mainCam.transform.position + aimDir * aimDistance, out Vector2 camBasedPos))
        {
            MuzzleParallaxOffset = localPos - camBasedPos;
        }
        else
        {
            MuzzleParallaxOffset = Vector2.zero;
        }

        // 보간 없이 그대로 — 지연은 기체 회전(timeToMaxTurn)과 카메라 댐핑이 이미 만들고 있음.
        // 여기서 Lerp를 또 걸면 지연이 두 번 겹쳐서 실제 기수와 어긋난 위치를 가리키게 됨.
        indicatorRect.anchoredPosition = localPos + aimScreenOffset;
    }

    // 월드 좌표 → 부모 rect 기준 로컬 좌표.
    // anchoredPosition이 부모 기준이라 캔버스 기준으로 넣으면 안 됨.
    // 카메라 뒤쪽 지점은 x/y가 뒤집혀 나오므로 false를 돌려줌.
    private bool TryProjectToLocal(Vector3 worldPos, out Vector2 localPos)
    {
        Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);
        if (screenPos.z < 0f)
        {
            localPos = Vector2.zero;
            return false;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parentRect,
            screenPos,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCam,
            out localPos);
        return true;
    }

    public void SetVisible(bool visible)
    {
        if (indicatorRect != null)
            indicatorRect.gameObject.SetActive(visible);
    }
}
