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

    [Tooltip("3인칭 카메라를 못 찾으면 새로 만듦. Main Camera가 지워진 작업씬 전용이라 기본은 끔.")]
    [SerializeField] private bool _createThirdPersonCameraIfMissing = false;

    [Header("Optional")]
    [Tooltip("카메라별 근거리 비주얼 표시 정책을 담당합니다.")]
    [SerializeField] private CockpitVisibilityController _visibilityController;
    [SerializeField] private bool _logSetup = true;

    [Header("VR HUD")]
    [SerializeField] private VrHudPresenter _vrHudPresenter;

    [Tooltip("VR HUD를 기체 정면에 고정할 때 CameraPoint로부터 떨어뜨릴 거리입니다.")]
    [SerializeField] private float _vrHudDistance = 1.5f;

    [Tooltip("1920x1080 HUD Canvas를 VR 월드 공간에 표시할 때 사용할 크기입니다.")]
    [SerializeField] private float _vrHudScale = 0.001f;

    [Tooltip("CameraPoint 기준 VR HUD의 좌우(X), 위아래(Y), 앞뒤(Z) 추가 오프셋입니다.")]
    [SerializeField] private Vector3 _vrHudLocalOffset = Vector3.zero;

    [Tooltip("CameraPoint 기준 VR HUD의 추가 회전 각도입니다.")]
    [SerializeField] private Vector3 _vrHudLocalEulerAngles = Vector3.zero;

    [Header("VR HUD Element Offsets (Canvas units)")]
    [SerializeField] private Vector2 _vrRadarOffset = Vector2.zero;
    [SerializeField] private Vector2 _vrHpOffset = Vector2.zero;
    [SerializeField] private Vector2 _vrFuelOffset = Vector2.zero;
    [SerializeField] private Vector2 _vrWeaponOffset = Vector2.zero;
    [SerializeField] private Vector2 _vrBoosterOffset = Vector2.zero;
    [SerializeField] private Vector3 _vrRadarRotation = Vector3.zero;
    [SerializeField] private Vector3 _vrHpRotation = Vector3.zero;
    [SerializeField] private Vector3 _vrFuelRotation = Vector3.zero;
    [SerializeField] private Vector3 _vrWeaponRotation = Vector3.zero;
    [SerializeField] private Vector3 _vrBoosterRotation = Vector3.zero;

    private AudioListener _vrListener;
    private AudioListener _thirdPersonListener;
    private readonly List<AudioListener> _suppressedListeners = new List<AudioListener>();
    private bool _isCockpitView;
    private bool _initialized;
    private readonly List<XRInputSubsystem> _xrInputSubsystems = new List<XRInputSubsystem>();
    private readonly Dictionary<Canvas, HudCanvasState> _hudCanvasStates =
        new Dictionary<Canvas, HudCanvasState>();
    private readonly Dictionary<RectTransform, Vector2> _hudElementOriginalPositions =
        new Dictionary<RectTransform, Vector2>();
    private readonly Dictionary<RectTransform, Quaternion> _hudElementOriginalRotations =
        new Dictionary<RectTransform, Quaternion>();
    private readonly List<Canvas> _hudCanvases = new List<Canvas>();
    private Transform _vrHudAnchor;
    private float _nextHudRefreshTime;
    private Coroutine _seatedTrackingCoroutine;
    private bool _hudPreviewRequested;

    public bool IsCockpitView => _isCockpitView;
    public float VrHudDistance => _vrHudDistance;
    public float VrHudScale => _vrHudScale;
    public Vector3 VrHudLocalOffset => _vrHudLocalOffset;
    public Vector3 VrHudLocalEulerAngles => _vrHudLocalEulerAngles;
    public Vector2 VrRadarOffset => _vrRadarOffset;
    public Vector2 VrHpOffset => _vrHpOffset;
    public Vector2 VrFuelOffset => _vrFuelOffset;
    public Vector2 VrWeaponOffset => _vrWeaponOffset;
    public Vector2 VrBoosterOffset => _vrBoosterOffset;

    public void PreviewVrHudPlacement(
        float distance,
        float scale,
        Vector3 localOffset,
        Vector3 localEulerAngles)
    {
        _vrHudDistance = Mathf.Max(0.05f, distance);
        _vrHudScale = Mathf.Max(0.00001f, scale);
        _vrHudLocalOffset = localOffset;
        _vrHudLocalEulerAngles = localEulerAngles;

        if (_initialized && _isCockpitView)
        {
            ConfigureHudForView(true);
            UpdateHudPose();
        }
    }

    public void PreviewVrHudElementOffsets(
        Vector2 radar,
        Vector2 hp,
        Vector2 fuel,
        Vector2 weapon,
        Vector2 booster)
    {
        _vrRadarOffset = radar;
        _vrHpOffset = hp;
        _vrFuelOffset = fuel;
        _vrWeaponOffset = weapon;
        _vrBoosterOffset = booster;
        _hudPreviewRequested = true;

        // The placement window is itself the explicit preview request. Do not
        // silently discard it while view initialization is between refreshes.
        if (!HasValidHudCanvas())
        {
            CacheHudCanvases();
        }

        ApplyHudElementOffsetsToCachedCanvases(true);
    }

    public void PreviewVrHudElementRotations(
        Vector3 radar,
        Vector3 hp,
        Vector3 fuel,
        Vector3 weapon,
        Vector3 booster)
    {
        _vrRadarRotation = radar;
        _vrHpRotation = hp;
        _vrFuelRotation = fuel;
        _vrWeaponRotation = weapon;
        _vrBoosterRotation = booster;
        _hudPreviewRequested = true;

        if (!HasValidHudCanvas())
        {
            CacheHudCanvases();
        }

        ApplyHudElementOffsetsToCachedCanvases(true);
    }

    // 콕핏 카메라는 프리팹에서 켜진 채 MainCamera 태그를 달고 있음 — 남의 함선까지 켜지지 않게 일단 꺼둠.
    private void Awake()
    {
        // LocalPlayerGuard(-1000)가 먼저 VR 진입을 끝냈으면 도로 끄면 안 됨.
        if (_initialized)
        {
            return;
        }
        ShutDownCockpitCamera();
    }

    private void OnEnable()
    {
        Canvas.willRenderCanvases -= ApplyHudElementsBeforeRender;
        Canvas.willRenderCanvases += ApplyHudElementsBeforeRender;
    }

    // 내 함선만 초기화. Awake는 IsMine 확정 전이라 Start에서 함.
    private void Start()
    {
        if (_initialized || !IsLocalShip())
        {
            return;
        }
        Initialize();
    }

    // 싱글(방 밖)이면 무조건 내 것.
    private bool IsLocalShip()
    {
        PhotonView owner = GetComponentInParent<PhotonView>();
        return owner == null || !PhotonNetwork.InRoom || owner.IsMine;
    }

    /// <summary>VR 시점 진입. 'VR 실행 중 + 내 함선'을 확인한 쪽만 호출할 것.</summary>
    public void ActivateCockpitView()
    {
        if (!_initialized)
        {
            Initialize();
        }
        SetView(true);
    }

    private void ShutDownCockpitCamera()
    {
        if (_vrCamera == null)
        {
            _vrCamera = FindCameraInChildren();
        }
        if (_vrCamera == null)
        {
            return;
        }

        SetCameraActive(_vrCamera, _vrCamera.GetComponent<AudioListener>(), false);
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

    private void LateUpdate()
    {
        if (_initialized && _isCockpitView)
        {
            UpdateHudPose();
            // Some HUD layout components rebuild RectTransforms after Update.
            // Reapply the requested VR-only offsets at the end of the frame.
            ApplyHudElementOffsetsToCachedCanvases(true);
        }
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= ApplyHudElementsBeforeRender;
        RestoreHudState();
    }

    private void OnDestroy()
    {
        Canvas.willRenderCanvases -= ApplyHudElementsBeforeRender;
        RestoreHudState();
    }

    private void ApplyHudElementsBeforeRender()
    {
        if (_isCockpitView || _hudPreviewRequested)
        {
            ApplyHudElementOffsetsToCachedCanvases(true);
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

        // Main Camera가 지워진 작업씬 복구용. 켜두면 전투 카메라가 없는 씬에서도 만들어져 MainCamera가 늘어남.
        if (_thirdPersonCamera == null && _createThirdPersonCameraIfMissing)
        {
            _thirdPersonCamera = CreateCinemachineOutputCamera();
            _thirdPersonBrain = _thirdPersonCamera.GetComponent<CinemachineBrain>();
        }

        if (_thirdPersonBrain == null && _thirdPersonCamera != null)
        {
            _thirdPersonBrain = _thirdPersonCamera.GetComponent<CinemachineBrain>();
        }

        if (_thirdPersonVirtualCamera == null)
        {
            _thirdPersonVirtualCamera = FindSceneComponent<CinemachineVirtualCamera>();
        }

        if (_thirdPersonVirtualCamera == null && _createThirdPersonCameraIfMissing)
        {
            _thirdPersonVirtualCamera = CreateThirdPersonVirtualCamera();
        }

        // 3인칭 카메라가 없어도 콕핏 시점은 되므로 초기화는 계속함. 전환만 막힘(ToggleView).
        _vrListener = GetOrAddAudioListener(_vrCamera);
        _thirdPersonListener = _thirdPersonCamera != null
            ? ResolveThirdPersonListener(_thirdPersonCamera)
            : null;
        _initialized = true;

        SetView(_startInCockpit);

        if (_logSetup)
        {
            Debug.Log(
                $"[CockpitViewSwitcher] 준비 완료: VR={_vrCamera.name}, " +
                $"3인칭={(_thirdPersonCamera != null ? _thirdPersonCamera.name : "없음(전환 불가)")}, " +
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

        // 실제로 화면을 출력 중인 MainCamera를 가장 먼저 사용한다.
        // 최신 씬처럼 CinemachineBrain이 없는 일반 카메라도 3인칭 출력일 수 있다.
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != _vrCamera &&
                camera.gameObject.scene.IsValid() &&
                camera.enabled &&
                camera.targetTexture == null &&
                camera.CompareTag("MainCamera"))
            {
                return camera;
            }
        }

        // 활성 MainCamera가 없으면 팀의 Cinemachine 출력 카메라를 찾는다.
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
        cameraObject.transform.SetParent(transform.root, true);
        return cameraComponent;
    }

    private CinemachineVirtualCamera CreateThirdPersonVirtualCamera()
    {
        GameObject virtualCameraObject = new GameObject("Virtual Camera (Third Person)");
        CinemachineVirtualCamera virtualCamera =
            virtualCameraObject.AddComponent<CinemachineVirtualCamera>();
        virtualCameraObject.transform.SetParent(transform.root, true);

        PhotonView owner = GetComponentInParent<PhotonView>();
        Transform followTarget = owner != null ? owner.transform : transform.root;
        virtualCamera.Follow = followTarget;
        virtualCamera.LookAt = followTarget;
        virtualCamera.Priority = 20;

        CinemachineTransposer transposer =
            virtualCamera.AddCinemachineComponent<CinemachineTransposer>();
        transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
        transposer.m_FollowOffset = new Vector3(0f, 3f, -8f);

        CinemachineComposer composer =
            virtualCamera.AddCinemachineComponent<CinemachineComposer>();
        composer.m_TrackedObjectOffset = new Vector3(0f, 1f, 0f);

        return virtualCamera;
    }

    private bool EnsureThirdPersonCamera()
    {
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

        if (_thirdPersonCamera == null && _createThirdPersonCameraIfMissing)
        {
            _thirdPersonCamera = CreateCinemachineOutputCamera();
            _thirdPersonBrain = _thirdPersonCamera.GetComponent<CinemachineBrain>();
        }

        if (_thirdPersonBrain == null && _thirdPersonCamera != null)
        {
            _thirdPersonBrain = _thirdPersonCamera.GetComponent<CinemachineBrain>();
        }

        if (_thirdPersonVirtualCamera == null)
        {
            _thirdPersonVirtualCamera = FindSceneComponent<CinemachineVirtualCamera>();
        }

        if (_thirdPersonVirtualCamera == null && _createThirdPersonCameraIfMissing)
        {
            _thirdPersonVirtualCamera = CreateThirdPersonVirtualCamera();
        }

        _thirdPersonListener = _thirdPersonCamera != null
            ? ResolveThirdPersonListener(_thirdPersonCamera)
            : null;

        // 일반 Camera도 유효한 3인칭 출력이다. Brain은 Cinemachine 씬에서만 필수다.
        return _thirdPersonCamera != null;
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

    /// <summary>
    /// [2026-08-04 수정] 3인칭 카메라의 오디오 리스너를 찾는다.
    ///
    /// 예전에는 GetOrAddAudioListener를 그대로 썼는데, 고른 카메라에 리스너가 없으면
    /// 새로 붙여 버렸다. 씬의 Main Camera에는 이미 리스너가 있으므로 결과적으로
    /// 리스너가 2개가 되고, 3인칭에서 둘 다 켜져
    /// "There are 2 audio listeners in the scene" 경고가 계속 떴다.
    /// 시점을 바꿔도 사라지지 않았던 이유가 이것이다.
    ///
    /// 그래서 새로 만들지 않는다. 카메라에 없으면 씬에 이미 있는 리스너를 찾아
    /// 그것을 관리 대상으로 삼는다. 없으면 null을 반환하고, 그때는 아무것도 건드리지 않는다.
    /// </summary>
    private AudioListener ResolveThirdPersonListener(Camera targetCamera)
    {
        if (targetCamera == null)
        {
            return null;
        }

        AudioListener listener = targetCamera.GetComponent<AudioListener>();
        if (listener != null)
        {
            return listener;
        }

        AudioListener[] all = Resources.FindObjectsOfTypeAll<AudioListener>();
        for (int i = 0; i < all.Length; i++)
        {
            AudioListener candidate = all[i];
            if (candidate == null ||
                candidate == _vrListener ||
                !candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    public void ToggleView()
    {
        bool thirdPersonCameraReady = EnsureThirdPersonCamera();
        // 3인칭 카메라가 없으면 콕핏만 꺼지고 켜질 게 없어 화면이 까매짐.
        if (_isCockpitView && !thirdPersonCameraReady)
        {
            Debug.LogWarning(
                "[CockpitViewSwitcher] 이 씬엔 3인칭 출력 카메라가 없어 전환하지 않음 " +
                "(CinemachineBrain이 붙은 카메라가 있어야 함).",
                this);
            return;
        }

        SetView(!_isCockpitView);
    }

    public void SetView(bool cockpitView)
    {
        if (!_initialized)
        {
            return;
        }

        _isCockpitView = cockpitView;
        if (!cockpitView)
        {
            _hudPreviewRequested = false;
        }

        SetCameraActive(_vrCamera, _vrListener, cockpitView);
        SetCameraActive(_thirdPersonCamera, _thirdPersonListener, !cockpitView);
        EnforceSingleAudioListener(cockpitView);
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

        if (_visibilityController != null)
        {
            _visibilityController.ApplyView(cockpitView);
        }

        OnViewChanged?.Invoke(cockpitView);
    }

    private void ConfigureHudForView(bool cockpitView)
    {
        _nextHudRefreshTime = Time.unscaledTime + 1f;
        if (cockpitView && !HasValidHudCanvas())
        {
            CacheHudCanvases();
        }

        if (cockpitView && _vrHudAnchor == null)
        {
            _vrHudAnchor = FindVrHudAnchor();
        }

        for (int i = _hudCanvases.Count - 1; i >= 0; i--)
        {
            Canvas canvas = _hudCanvases[i];
            if (canvas == null)
            {
                _hudCanvasStates.Remove(canvas);
                _hudCanvases.RemoveAt(i);
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

                canvas.transform.localScale =
                    Vector3.one * Mathf.Max(0.0001f, _vrHudScale);

                if (_vrHudPresenter != null)
                {
                    _vrHudPresenter.Show(_vrCamera, canvas);
                }
            }
            else
            {
                if (_vrHudPresenter != null)
                {
                    _vrHudPresenter.Restore(canvas);
                }

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

            ConfigureHudElements(canvas, cockpitView);
        }

        if (cockpitView)
        {
            UpdateHudPose();
        }
        else
        {
            _vrHudAnchor = null;
        }
    }

    private void ConfigureHudElements(Canvas canvas, bool cockpitView)
    {
        ApplyHudElementTransform(canvas, "PanelRadar", _vrRadarOffset, _vrRadarRotation, cockpitView);
        ApplyHudElementTransform(canvas, "PanelHUD", _vrHpOffset, _vrHpRotation, cockpitView);
        ApplyHudElementTransform(canvas, "FuelGageIndicator", _vrFuelOffset, _vrFuelRotation, cockpitView);
        ApplyHudElementTransform(canvas, "AmmoUI", _vrWeaponOffset, _vrWeaponRotation, cockpitView);
        ApplyHudElementTransform(canvas, "QuickSlotUI", _vrWeaponOffset, _vrWeaponRotation, cockpitView);
        ApplyHudElementTransform(canvas, "PanelNitro", _vrBoosterOffset, _vrBoosterRotation, cockpitView);
    }

    private void ApplyHudElementOffsetsToCachedCanvases(bool cockpitView)
    {
        for (int i = 0; i < _hudCanvases.Count; i++)
        {
            if (_hudCanvases[i] != null)
            {
                ConfigureHudElements(_hudCanvases[i], cockpitView);
            }
        }
    }

    public string GetVrHudElementStatus()
    {
        Canvas canvas = null;
        for (int i = 0; i < _hudCanvases.Count; i++)
        {
            if (_hudCanvases[i] != null)
            {
                canvas = _hudCanvases[i];
                break;
            }
        }

        if (canvas == null)
        {
            return "HUD Canvas: not found";
        }

        return
            HudElementStatus(canvas, "Radar", "PanelRadar") + "\n" +
            HudElementStatus(canvas, "HP", "PanelHUD") + "\n" +
            HudElementStatus(canvas, "Fuel", "FuelGageIndicator") + "\n" +
            HudElementStatus(canvas, "Weapon Ammo", "AmmoUI") + "\n" +
            HudElementStatus(canvas, "Weapon/Skill", "QuickSlotUI") + "\n" +
            HudElementStatus(canvas, "Booster", "PanelNitro");
    }

    private static string HudElementStatus(
        Canvas canvas,
        string label,
        string targetName)
    {
        RectTransform target = FindHudElement(canvas.transform, targetName);
        if (target == null)
        {
            return label + ": missing";
        }

        Vector2 position = target.anchoredPosition;
        Vector3 rotation = target.localEulerAngles;
        return label + ": connected  actual X=" + position.x.ToString("0.##") +
            " Y=" + position.y.ToString("0.##") +
            " Rot=" + rotation.x.ToString("0.#") + "," +
            rotation.y.ToString("0.#") + "," + rotation.z.ToString("0.#");
    }

    private void ApplyHudElementTransform(
        Canvas canvas,
        string targetName,
        Vector2 offset,
        Vector3 rotation,
        bool cockpitView)
    {
        RectTransform target = FindHudElement(canvas.transform, targetName);
        if (target == null)
        {
            return;
        }

        if (!_hudElementOriginalPositions.TryGetValue(target, out Vector2 original))
        {
            original = target.anchoredPosition;
            _hudElementOriginalPositions.Add(target, original);
        }

        if (!_hudElementOriginalRotations.TryGetValue(target, out Quaternion originalRotation))
        {
            originalRotation = target.localRotation;
            _hudElementOriginalRotations.Add(target, originalRotation);
        }

        target.anchoredPosition = cockpitView ? original + offset : original;
        target.localRotation = cockpitView
            ? originalRotation * Quaternion.Euler(rotation)
            : originalRotation;
    }

    private static RectTransform FindHudElement(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            {
                return child as RectTransform;
            }

            RectTransform result = FindHudElement(child, targetName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private bool HasValidHudCanvas()
    {
        bool found = false;
        for (int i = _hudCanvases.Count - 1; i >= 0; i--)
        {
            Canvas canvas = _hudCanvases[i];
            if (canvas == null)
            {
                _hudCanvasStates.Remove(canvas);
                _hudCanvases.RemoveAt(i);
                continue;
            }

            found = true;
        }

        return found;
    }

    private void CacheHudCanvases()
    {
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null ||
                !canvas.gameObject.scene.IsValid() ||
                !canvas.name.Equals("HUD", StringComparison.OrdinalIgnoreCase) ||
                _hudCanvases.Contains(canvas))
            {
                continue;
            }

            _hudCanvases.Add(canvas);
        }
    }

    private void RestoreHudState()
    {
        if (_hudCanvases.Count > 0)
        {
            ConfigureHudForView(false);
        }

        if (_vrHudPresenter != null)
        {
            _vrHudPresenter.RestoreAll();
        }
    }

    private void UpdateHudPose()
    {
        if (_vrHudAnchor == null)
        {
            _vrHudAnchor = FindVrHudAnchor();
            if (_vrHudAnchor == null)
            {
                return;
            }
        }

        Vector3 localPosition =
            _vrHudLocalOffset +
            Vector3.forward * Mathf.Max(0.05f, _vrHudDistance);
        Vector3 hudPosition = _vrHudAnchor.TransformPoint(localPosition);
        Quaternion hudRotation =
            _vrHudAnchor.rotation *
            Quaternion.Euler(_vrHudLocalEulerAngles);
        Vector3 hudScale =
            Vector3.one * Mathf.Max(0.0001f, _vrHudScale);

        for (int i = 0; i < _hudCanvases.Count; i++)
        {
            Canvas canvas = _hudCanvases[i];
            if (canvas == null)
            {
                continue;
            }

            canvas.transform.SetPositionAndRotation(
                hudPosition,
                hudRotation);
            canvas.transform.localScale = hudScale;
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
            if (current.name.Equals("XR Origin", StringComparison.OrdinalIgnoreCase) ||
                current.name.Equals("XRRig", StringComparison.OrdinalIgnoreCase))
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
               !xrRig.name.Equals("XR Origin", StringComparison.OrdinalIgnoreCase) &&
               !xrRig.name.Equals("XRRig", StringComparison.OrdinalIgnoreCase))
        {
            xrRig = xrRig.parent;
        }

        if ((!xrRig.name.Equals("XR Origin", StringComparison.OrdinalIgnoreCase) &&
             !xrRig.name.Equals("XRRig", StringComparison.OrdinalIgnoreCase)) ||
            xrRig.parent == null)
        {
            Debug.LogWarning(
                "[CockpitViewSwitcher] XR Origin 또는 CameraPoint를 찾지 못해 " +
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

    /// <summary>
    /// [2026-08-04 추가] 콕핏 시점에서 오디오 리스너를 하나만 남긴다.
    ///
    /// 이 스크립트는 콕핏 카메라와 3인칭 카메라의 리스너만 관리하는데,
    /// 씬에 따라 그 둘이 아닌 리스너(추적 카메라, 씬 기본 카메라 등)가 더 있어
    /// "There are 2 audio listeners in the scene" 경고가 매 프레임 뜬다.
    ///
    /// 콕핏에 들어갈 때 남의 리스너를 꺼두고, 나올 때 원래대로 되돌린다.
    /// 껐던 것만 기억했다가 되살리므로 원래 꺼져 있던 것은 건드리지 않는다.
    /// </summary>
    private void EnforceSingleAudioListener(bool cockpitView)
    {
        if (!cockpitView)
        {
            for (int i = 0; i < _suppressedListeners.Count; i++)
            {
                if (_suppressedListeners[i] != null)
                {
                    _suppressedListeners[i].enabled = true;
                }
            }

            _suppressedListeners.Clear();
            return;
        }

        _suppressedListeners.Clear();

        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener == null || listener == _vrListener || !listener.enabled)
            {
                continue;
            }

            listener.enabled = false;
            _suppressedListeners.Add(listener);
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
