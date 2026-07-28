using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Photon.Pun;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// V 키로 XR 콕핏 카메라와 프로젝트의 기존 Cinemachine 3인칭 카메라를 전환합니다.
/// </summary>
public sealed class CockpitViewSwitcher : MonoBehaviour
{
    private struct HudCanvasState
    {
        public RenderMode renderMode;
        public Camera worldCamera;
        public float planeDistance;
        public bool overrideSorting;
        public int sortingOrder;
        public Transform parent;
        public int siblingIndex;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    public static event Action<bool> OnViewChanged;

    [Header("Input")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.V;

    [Tooltip("VR 시점이 좌석과 어긋났을 때 현재 자세를 기준으로 다시 맞추는 키. " +
             "앉은 키·자세가 사람마다 달라 시작 위치가 틀어질 수 있으므로 언제든 이 키로 재정렬한다.")]
    [SerializeField] private KeyCode _recenterKey = KeyCode.F8;

    [Header("Start View")]
    [SerializeField] private bool _startInCockpit = false;

    [Header("Cameras (optional auto setup)")]
    [Tooltip("XR Rig 아래의 카메라. 비어 있으면 이 오브젝트의 자식에서 찾습니다.")]
    [SerializeField] private Camera _vrCamera;

    [Tooltip("기존 3인칭 Main Camera. 비어 있으면 CinemachineBrain이 붙은 씬 카메라를 찾습니다.")]
    [SerializeField] private Camera _thirdPersonCamera;

    [Tooltip("기존 Main Camera의 CinemachineBrain. 비어 있으면 씬에서 찾습니다.")]
    [SerializeField] private CinemachineBrain _thirdPersonBrain;

    [Tooltip("원본 3인칭 시점을 만드는 Virtual Camera. 비어 있으면 씬에서 찾습니다.")]
    [SerializeField] private CinemachineVirtualCamera _thirdPersonVirtualCamera;

    [Header("Optional")]
    [Tooltip("3인칭 시점에서 숨길 콕핏 메시 루트. 비워 두면 항상 표시합니다.")]
    [SerializeField] private GameObject _cockpitVisualRoot;
    [SerializeField] private bool _hideCockpitInThirdPerson;
    [SerializeField] private bool _logSetup = true;

    [Header("VR HUD")]
    [Tooltip("VR HUD를 기체 정면에 고정할 때 CameraPoint로부터 떨어뜨릴 거리입니다.")]
    [SerializeField] private float _vrHudDistance = 6f;

    [Tooltip("1920x1080 HUD Canvas를 VR 월드 공간에 표시할 때 사용할 크기입니다.")]
    [SerializeField] private float _vrHudScale = 0.004f;

    private AudioListener _vrListener;
    private AudioListener _thirdPersonListener;
    private bool _isCockpitView;
    private bool _initialized;
    private Player _reverseThrusterOwner;
    private readonly List<Renderer> _reverseThrusterRenderers = new List<Renderer>();
    private readonly List<XRInputSubsystem> _xrInputSubsystems = new List<XRInputSubsystem>();
    private readonly Dictionary<Canvas, HudCanvasState> _hudCanvasStates =
        new Dictionary<Canvas, HudCanvasState>();
    private float _nextReverseThrusterRefreshTime;
    private float _nextHudRefreshTime;
    private Coroutine _seatedTrackingCoroutine;

    public bool IsCockpitView => _isCockpitView;

    private void Awake()
    {
        PhotonView photonView = GetComponentInParent<PhotonView>();
        if (photonView != null && !photonView.IsMine)
        {
            DisableRemotePlayerCameras();
            enabled = false;
            return;
        }

        Initialize();
    }

    private void DisableRemotePlayerCameras()
    {
        foreach (Camera cameraComponent in GetComponentsInChildren<Camera>(true))
        {
            cameraComponent.enabled = false;
            AudioListener listener = cameraComponent.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = false;
            }
        }
    }

    private void Update()
    {
        if (_initialized && Input.GetKeyDown(_toggleKey))
        {
            ToggleView();
        }

        // 콕핏 시점일 때만 재정렬 — 자세가 흐트러지면 언제든 좌석 기준으로 되돌린다.
        if (_initialized && _isCockpitView && Input.GetKeyDown(_recenterKey))
        {
            RecenterView();
        }

        Player localPlayer = GameManager.Instance != null ? GameManager.Instance.playerRef : null;
        if (_initialized && (localPlayer != _reverseThrusterOwner ||
            (_isCockpitView && Time.unscaledTime >= _nextReverseThrusterRefreshTime)))
        {
            CacheLocalReverseThrusters(localPlayer);
            SetReverseThrusterRenderersVisible(!_isCockpitView);
        }

        // HUD가 플레이어 생성 이후 늦게 만들어지는 씬도 있으므로 VR 중에는
        // 새 HUD Canvas를 주기적으로 찾아 XR 카메라에 연결한다.
        if (_initialized &&
            _isCockpitView &&
            Time.unscaledTime >= _nextHudRefreshTime)
        {
            _nextHudRefreshTime = Time.unscaledTime + 1f;
            ConfigureHudForView(true);
        }
    }

    private void Initialize()
    {
        if (_vrCamera == null)
        {
            _vrCamera = FindCameraInChildren();
        }

        if (_vrCamera == null)
        {
            Debug.LogError(
                $"[CockpitViewSwitcher] {name}: XR 카메라를 찾지 못했습니다. " +
                "_vrCamera에 XR Rig의 카메라를 연결하세요.",
                this);
            enabled = false;
            return;
        }

        if (_thirdPersonBrain == null)
        {
            _thirdPersonBrain = FindSceneComponent<CinemachineBrain>();
        }

        if (_thirdPersonCamera == null)
        {
            _thirdPersonCamera = _thirdPersonBrain != null
                ? _thirdPersonBrain.GetComponent<Camera>()
                : FindExistingThirdPersonCamera();
        }

        // SeokYong 씬처럼 원본 Virtual Camera는 남아 있지만 출력용 Main Camera가
        // 제거된 경우가 있다. 임의 추적 카메라를 만드는 것이 아니라,
        // 원본 Cinemachine 구도를 출력할 표준 Camera + Brain만 복구한다.
        if (_thirdPersonCamera == null)
        {
            _thirdPersonCamera = CreateCinemachineOutputCamera();
            _thirdPersonBrain = _thirdPersonCamera.GetComponent<CinemachineBrain>();
        }

        if (_thirdPersonBrain == null)
        {
            _thirdPersonBrain = _thirdPersonCamera.GetComponent<CinemachineBrain>();
        }

        if (_thirdPersonVirtualCamera == null)
        {
            _thirdPersonVirtualCamera = FindSceneComponent<CinemachineVirtualCamera>();
        }

        _vrListener = GetOrAddAudioListener(_vrCamera);
        _thirdPersonListener = GetOrAddAudioListener(_thirdPersonCamera);
        _initialized = true;

        SetView(_startInCockpit);

        if (_logSetup)
        {
            Debug.Log(
                $"[CockpitViewSwitcher] 준비 완료: VR={_vrCamera.name}, " +
                $"3인칭={_thirdPersonCamera.name}, " +
                $"Brain={(_thirdPersonBrain != null ? _thirdPersonBrain.name : "없음")}, " +
                $"VirtualCamera={(_thirdPersonVirtualCamera != null ? _thirdPersonVirtualCamera.name : "없음")}",
                this);
        }
    }

    private Camera FindCameraInChildren()
    {
        Camera[] cameras = GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != _thirdPersonCamera)
            {
                return cameras[i];
            }
        }

        return null;
    }

    private Camera FindExistingThirdPersonCamera()
    {
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != _vrCamera &&
                camera.gameObject.scene.IsValid() &&
                camera.GetComponent<CinemachineBrain>() != null)
            {
                return camera;
            }
        }

        return null;
    }

    private Camera CreateCinemachineOutputCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera (Third Person)");
        Camera cameraComponent = cameraObject.AddComponent<Camera>();

        // C:\TeamProjectMYS의 원본 SeokYong Main Camera 기본값.
        // 실제 위치·회전·FOV는 CinemachineVirtualCamera가 매 프레임 적용한다.
        cameraComponent.clearFlags = CameraClearFlags.Skybox;
        cameraComponent.fieldOfView = 95f;
        cameraComponent.nearClipPlane = 0.3f;
        cameraComponent.farClipPlane = 1000f;
        cameraComponent.cullingMask = ~0;
        cameraComponent.stereoTargetEye = StereoTargetEyeMask.Both;

        cameraObject.AddComponent<CinemachineBrain>();
        return cameraComponent;
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].gameObject.scene.IsValid())
            {
                return components[i];
            }
        }

        return null;
    }

    private static AudioListener GetOrAddAudioListener(Camera targetCamera)
    {
        AudioListener listener = targetCamera.GetComponent<AudioListener>();
        return listener != null
            ? listener
            : targetCamera.gameObject.AddComponent<AudioListener>();
    }

    public void ToggleView()
    {
        SetView(!_isCockpitView);
    }

    public void SetView(bool cockpitView)
    {
        if (!_initialized)
        {
            return;
        }

        _isCockpitView = cockpitView;

        SetCameraActive(_vrCamera, _vrListener, cockpitView);
        SetCameraActive(_thirdPersonCamera, _thirdPersonListener, !cockpitView);
        ConfigureHudForView(cockpitView);

        if (cockpitView)
        {
            if (_seatedTrackingCoroutine != null)
            {
                StopCoroutine(_seatedTrackingCoroutine);
            }

            _seatedTrackingCoroutine = StartCoroutine(ConfigureSeatedTracking());
        }

        if (_thirdPersonBrain != null)
        {
            _thirdPersonBrain.enabled = !cockpitView;
        }

        if (_cockpitVisualRoot != null && _hideCockpitInThirdPerson)
        {
            _cockpitVisualRoot.SetActive(cockpitView);
        }

        CacheLocalReverseThrusters(GameManager.Instance != null ? GameManager.Instance.playerRef : null);
        SetReverseThrusterRenderersVisible(!cockpitView);

        OnViewChanged?.Invoke(cockpitView);
    }

    private void ConfigureHudForView(bool cockpitView)
    {
        _nextHudRefreshTime = Time.unscaledTime + 1f;
        Transform vrHudAnchor = cockpitView ? FindVrHudAnchor() : null;
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null ||
                !canvas.gameObject.scene.IsValid() ||
                !canvas.name.Equals("HUD", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!_hudCanvasStates.ContainsKey(canvas))
            {
                _hudCanvasStates.Add(
                    canvas,
                    new HudCanvasState
                    {
                        renderMode = canvas.renderMode,
                        worldCamera = canvas.worldCamera,
                        planeDistance = canvas.planeDistance,
                        overrideSorting = canvas.overrideSorting,
                        sortingOrder = canvas.sortingOrder,
                        parent = canvas.transform.parent,
                        siblingIndex = canvas.transform.GetSiblingIndex(),
                        localPosition = canvas.transform.localPosition,
                        localRotation = canvas.transform.localRotation,
                        localScale = canvas.transform.localScale
                    });
            }

            if (cockpitView)
            {
                // HUD를 HMD 카메라에 붙이면 고개를 돌릴 때 CrossHairHUD까지 따라간다.
                // CameraPoint는 기체에 고정되어 있으므로 World Space HUD의 기준으로 사용한다.
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = _vrCamera;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 100;

                Transform anchor = vrHudAnchor != null ? vrHudAnchor : transform;
                canvas.transform.SetParent(anchor, false);
                canvas.transform.localPosition =
                    Vector3.forward * Mathf.Max(0.5f, _vrHudDistance);
                canvas.transform.localRotation = Quaternion.identity;
                canvas.transform.localScale =
                    Vector3.one * Mathf.Max(0.0001f, _vrHudScale);
            }
            else
            {
                HudCanvasState originalState = _hudCanvasStates[canvas];
                canvas.renderMode = originalState.renderMode;
                canvas.worldCamera = originalState.worldCamera;
                canvas.planeDistance = originalState.planeDistance;
                canvas.overrideSorting = originalState.overrideSorting;
                canvas.sortingOrder = originalState.sortingOrder;
                canvas.transform.SetParent(originalState.parent, false);
                canvas.transform.localPosition = originalState.localPosition;
                canvas.transform.localRotation = originalState.localRotation;
                canvas.transform.localScale = originalState.localScale;
                canvas.transform.SetSiblingIndex(originalState.siblingIndex);
            }
        }
    }

    private Transform FindVrHudAnchor()
    {
        if (_vrCamera == null)
        {
            return null;
        }

        Transform current = _vrCamera.transform;
        while (current != null)
        {
            if (current.name.Equals("XRRig", StringComparison.OrdinalIgnoreCase))
            {
                // XRRig 자체는 HMD 원점 보정으로 움직일 수 있다.
                // 그 부모인 CameraPoint는 cockpit/기체 좌표에 고정되어 있다.
                return current.parent != null ? current.parent : current;
            }

            current = current.parent;
        }

        return _vrCamera.transform.parent;
    }

    private IEnumerator ConfigureSeatedTracking()
    {
        // XRRuntimeManager가 서브시스템을 늦게 시작할 수 있으므로 준비될 때까지
        // 잠시 재시도한 뒤, 룸스케일 좌표 대신 현재 HMD 위치를 좌석 원점으로 잡는다.
        const float timeout = 3f;
        float deadline = Time.realtimeSinceStartup + timeout;

        while (Time.realtimeSinceStartup < deadline)
        {
            _xrInputSubsystems.Clear();
            SubsystemManager.GetInstances(_xrInputSubsystems);

            bool configured = false;
            for (int i = 0; i < _xrInputSubsystems.Count; i++)
            {
                XRInputSubsystem subsystem = _xrInputSubsystems[i];
                if (subsystem == null || !subsystem.running)
                {
                    continue;
                }

                subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);

                // OpenXR 런타임의 TryRecenter는 비동기로 추적 원점을 다시
                // 덮어쓸 수 있다. XRRig 자체를 콕핏에 맞추므로 여기서는
                // 런타임 재중심화를 호출하지 않는다.
                configured = true;
            }

            if (configured)
            {
                // 추적 원점 모드 변경과 첫 HMD Pose가 모두 반영된 뒤 맞춘다.
                // 기체의 월드 스폰 방향과 무관하게 CameraPoint.forward가
                // 플레이 시작 시 사용자의 정면이 된다.
                yield return new WaitForSecondsRealtime(0.5f);
                AlignViewWithCockpitForward();

                // 일부 런타임이 첫 Pose 직후 원점 갱신을 한 번 더 보내므로
                // 짧게 기다렸다가 최종 보정한다.
                yield return new WaitForSecondsRealtime(0.25f);
                AlignViewWithCockpitForward();
                break;
            }

            yield return null;
        }

        _seatedTrackingCoroutine = null;
    }

    /// <summary>
    /// 현재 헤드셋 자세를 기준으로 시점을 좌석(CameraPoint)에 다시 맞춘다.
    /// 재정렬 키로 호출 — 앉은 자세가 흐트러졌을 때 언제든 되돌릴 수 있다.
    /// </summary>
    public void RecenterView()
    {
        AlignViewWithCockpitForward();
    }

    private void AlignViewWithCockpitForward()
    {
        if (_vrCamera == null)
        {
            return;
        }

        Transform xrRig = _vrCamera.transform;
        while (xrRig.parent != null &&
               !xrRig.name.Equals("XRRig", StringComparison.OrdinalIgnoreCase))
        {
            xrRig = xrRig.parent;
        }

        if (!xrRig.name.Equals("XRRig", StringComparison.OrdinalIgnoreCase) ||
            xrRig.parent == null)
        {
            Debug.LogWarning(
                "[CockpitViewSwitcher] XRRig 또는 CameraPoint를 찾지 못해 " +
                "콕핏 정면 보정을 적용하지 못했습니다.",
                this);
            return;
        }

        Transform cameraPoint = xrRig.parent;
        Vector3 up = cameraPoint.up;
        Vector3 currentForward =
            Vector3.ProjectOnPlane(_vrCamera.transform.forward, up).normalized;
        Vector3 cockpitForward =
            Vector3.ProjectOnPlane(cameraPoint.forward, up).normalized;

        if (currentForward.sqrMagnitude > 0.0001f &&
            cockpitForward.sqrMagnitude > 0.0001f)
        {
            float yawCorrection =
                Vector3.SignedAngle(currentForward, cockpitForward, up);

            // 카메라의 현재 위치는 유지하면서 XR 원점만 회전시켜,
            // 이후의 헤드셋 움직임도 콕핏 좌표계를 기준으로 적용되게 한다.
            xrRig.RotateAround(_vrCamera.transform.position, up, yawCorrection);
        }

        // 방향만 맞추면 눈 "위치"는 그대로라, 앉은 키·시작 자세에 따라 좌석보다
        // 높거나 앞으로 나간 시점이 된다. 카메라가 CameraPoint에 오도록 XR 원점을
        // 평행이동해 실제 좌석 위치에서 보이게 맞춘다. (회전 보정 뒤에 해야 함)
        Vector3 positionOffset = cameraPoint.position - _vrCamera.transform.position;
        xrRig.position += positionOffset;
    }

    private void CacheLocalReverseThrusters(Player player)
    {
        _reverseThrusterOwner = player;
        _nextReverseThrusterRefreshTime = Time.unscaledTime + 1f;
        _reverseThrusterRenderers.Clear();
        if (player == null)
        {
            return;
        }

        HashSet<Renderer> uniqueRenderers = new HashSet<Renderer>();
        Transform[] transforms = player.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            string objectName = transforms[i].name;
            bool isReverseRoot =
                objectName.Equals("Thruster_Rev", StringComparison.OrdinalIgnoreCase) ||
                objectName.StartsWith("Rev-Booster", StringComparison.OrdinalIgnoreCase) ||
                objectName.StartsWith("Rev-Sub-Booster", StringComparison.OrdinalIgnoreCase);

            if (!isReverseRoot)
            {
                continue;
            }

            Renderer[] childRenderers = transforms[i].GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < childRenderers.Length; rendererIndex++)
            {
                if (childRenderers[rendererIndex] != null && uniqueRenderers.Add(childRenderers[rendererIndex]))
                {
                    _reverseThrusterRenderers.Add(childRenderers[rendererIndex]);
                }
            }
        }
    }

    private void SetReverseThrusterRenderersVisible(bool visible)
    {
        for (int i = 0; i < _reverseThrusterRenderers.Count; i++)
        {
            Renderer renderer = _reverseThrusterRenderers[i];
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }

    private static void SetCameraActive(
        Camera targetCamera,
        AudioListener listener,
        bool active)
    {
        if (targetCamera == null)
        {
            return;
        }

        targetCamera.enabled = active;
        if (listener != null)
        {
            listener.enabled = active;
        }

        targetCamera.tag = active ? "MainCamera" : "Untagged";
    }

}
