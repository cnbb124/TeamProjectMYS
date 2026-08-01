using UnityEngine;
using Cinemachine;

// =====================================================================
// [외부 참조 가이드]
// =====================================================================
//   CameraManager.Instance.SetFollowTarget(transform) : vCam이 따라갈 대상 지정
//     → 로컬 플레이어(내 함선)가 Start()에서 자기 자신을 넘긴다. (Player.cs 참고)
// =====================================================================

// =====================================================================
// CameraManager — 게임플레이 카메라(vCam)의 추적 대상을 런타임에 지정하는 단일 진입점.
//
// [왜 필요한가]
//   멀티플레이에선 플레이어 함선이 PhotonNetwork.Instantiate로 런타임에 스폰되므로,
//   인스펙터에 미리 vCam.Follow를 드래그해둘 수 없다. 스폰된 '내 함선'(IsMine)이
//   Start()에서 SetFollowTarget(자기 자신)을 호출해 vCam을 자기에게 붙인다.
//   싱글플레이(씬 배치 플레이어)도 IsMine=true라 동일하게 동작한다.
//   (Find 계열 미사용 — 캐시된 참조라 O(1)이고, vCam이 여러 개여도 모호함이 없다.)
//
// [에디터 세팅]
//   1. 게임플레이 씬의 빈 오브젝트(또는 vCam과 같은 오브젝트)에 이 스크립트 부착.
//   2. _virtualCamera 필드에 씬의 CinemachineVirtualCamera 드래그.
//      (비워두면 Awake에서 씬에서 1개 자동 탐색 — vCam이 여러 개면 명시 지정 권장)
// =====================================================================
public class CameraManager : MonoBehaviour
{
    // 프로젝트 표준 싱글톤(씬 전용 — vCam이 게임플레이 씬과 짝이라 씬을 넘기면 안 됨, CameraShaker와 동일).
    private static CameraManager instance;
    // Awake에서만 세팅됨. Awake 전엔 null이므로 최초 접근은 Start부터 할 것.
    public static CameraManager Instance => instance;

    [Tooltip("게임플레이 vCam. 비우면 Awake에서 씬에서 자동 탐색(여러 개면 명시 지정 권장).")]
    [SerializeField] private CinemachineVirtualCamera _virtualCamera;

    [Tooltip("추적 대상 지정 시 Follow뿐 아니라 LookAt도 같이 설정. vCam Aim이 Composer면 켜고, POV/Do Nothing 등 커스텀 조준이면 끈다.")]
    [SerializeField] private bool _bindLookAt = true;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            // DontDestroyOnLoad 사용 안 함 (씬 전용 매니저 — 위 주석 참고)
        }
        else if (instance != this)
        {
            Debug.LogWarning("중복된 CameraManager 발견. 파괴 후 실행");
            Destroy(gameObject);
            return;
        }

        if (_virtualCamera == null)
        {
            _virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        }
    }

    // 함선이 DDOL이라 다른 씬에서 태어났으면 Player.Start의 등록이 여기선 안 돎 — 카메라가 직접 회수함.
    private void Start()
    {
        if (_virtualCamera != null && _virtualCamera.Follow != null)
        {
            return;
        }

        Player player = GameManager.Instance != null ? GameManager.Instance.playerRef : null;
        if (player != null && player.CameraAimProxy != null)
        {
            SetFollowTarget(player.CameraAimProxy);
        }
    }

    /// <summary>vCam이 따라갈 대상 지정(로컬 플레이어가 스폰 시 자기 자신을 넘김). _bindLookAt이면 LookAt도 같이.</summary>
    public void SetFollowTarget(Transform target)
    {
        if (_virtualCamera == null)
        {
            Debug.LogWarning("[CameraManager] vCam 미지정 — 추적 대상 설정 불가");
            return;
        }
        _virtualCamera.Follow = target;
        if (_bindLookAt)
        {
            _virtualCamera.LookAt = target;
        }
    }
}
