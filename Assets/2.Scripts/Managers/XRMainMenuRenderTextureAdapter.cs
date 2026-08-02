using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Renders scene-owned screen-space UI into a fixed 16:9 texture and presents
/// it on a head-locked panel in XR. Each scene owns and releases its session.
/// </summary>
[DefaultExecutionOrder(-19000)]
public sealed class XRMainMenuRenderTextureAdapter : MonoBehaviour
{
    private const string CombatHudCanvasName = "HUD";
    private const int TextureWidth = 1920;
    private const int TextureHeight = 1080;
    private const float PanelDistance = 1.25f;
    private const float ViewMargin = 0.82f;
    private const int RefreshFrameCount = 12;

    private static XRMainMenuRenderTextureAdapter _instance;

    private Coroutine _refreshCoroutine;
    private Camera _uiCamera;
    private RenderTexture _uiTexture;
    private GameObject _panel;
    private Material _panelMaterial;
    private Texture2D _cursorTexture;
    private RectTransform _cursor;
    private RenderTexturePointerInput _pointerInput;
    private XRRenderTexturePanelInput _xrPointerInput;
    private int _sessionSceneHandle = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject adapterObject =
            new GameObject(nameof(XRMainMenuRenderTextureAdapter));
        _instance =
            adapterObject.AddComponent<XRMainMenuRenderTextureAdapter>();
        DontDestroyOnLoad(adapterObject);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        XRRuntimeManager.Started += OnXrStarted;
    }

    private void Start()
    {
        if (XRRuntimeManager.IsRunning)
        {
            QueueRefresh();
        }
    }

    private void Update()
    {
        if (_cursor != null && _pointerInput != null)
        {
            _cursor.anchoredPosition = _pointerInput.mousePosition;
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        XRRuntimeManager.Started -= OnXrStarted;
        CleanupSession();

        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CleanupSession();
        if (XRRuntimeManager.IsRunning)
        {
            QueueRefresh();
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (scene.handle == _sessionSceneHandle)
        {
            CleanupSession();
        }
    }

    private void OnXrStarted()
    {
        QueueRefresh();
    }

    private void QueueRefresh()
    {
        if (_refreshCoroutine != null)
        {
            StopCoroutine(_refreshCoroutine);
        }

        _refreshCoroutine = StartCoroutine(RefreshActiveSceneUi());
    }

    private IEnumerator RefreshActiveSceneUi()
    {
        for (int i = 0; i < RefreshFrameCount; i++)
        {
            if (!XRRuntimeManager.IsRunning)
            {
                break;
            }

            AdaptScene(SceneManager.GetActiveScene());

            yield return null;
        }

        _refreshCoroutine = null;
    }

    private void AdaptScene(Scene scene)
    {
        if (!UsesHeadLockedMenuPanel(scene))
        {
            CleanupSession();
            return;
        }

        Camera xrCamera = FindBestSceneCamera(scene);
        Canvas[] canvases = FindSceneCanvases(scene);
        if (xrCamera == null || canvases.Length == 0)
        {
            return;
        }

        EnsureSession(scene, xrCamera);
        if (_uiCamera == null || _uiTexture == null || _panel == null)
        {
            return;
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            ConfigureCanvas(canvases[i]);
        }

        ConfigurePointerInput(scene, canvases[0]);
    }

    private static bool UsesHeadLockedMenuPanel(Scene scene)
    {
        switch (scene.name)
        {
            case "MAIN":
            case "LOGIN":
            case "MAP_SELECT":
            case "MULTIPLAYER":
            case "BASE_HANGAR":
            case "RESULT":
            case "LOADING_SEQUENCE":
                return true;
            default:
                return false;
        }
    }

    private void EnsureSession(Scene scene, Camera xrCamera)
    {
        if (_sessionSceneHandle == scene.handle &&
            _uiCamera != null &&
            _uiTexture != null &&
            _panel != null)
        {
            return;
        }

        CleanupSession();
        _sessionSceneHandle = scene.handle;

        _uiTexture = new RenderTexture(
            TextureWidth,
            TextureHeight,
            16,
            RenderTextureFormat.ARGB32)
        {
            name = "MAIN_VR_UI",
            antiAliasing = 1,
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false
        };
        _uiTexture.Create();

        GameObject cameraObject = new GameObject("MAIN VR UI Camera");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        _uiCamera = cameraObject.AddComponent<Camera>();
        _uiCamera.clearFlags = CameraClearFlags.SolidColor;
        _uiCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        int uiLayer = LayerMask.NameToLayer("UI");
        _uiCamera.cullingMask = uiLayer >= 0 ? 1 << uiLayer : 0;
        _uiCamera.nearClipPlane = 0.1f;
        _uiCamera.farClipPlane = 10f;
        _uiCamera.depth = -100f;
        _uiCamera.stereoTargetEye = StereoTargetEyeMask.None;
        _uiCamera.targetTexture = _uiTexture;

        _panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _panel.name = "MAIN VR UI Panel";
        SceneManager.MoveGameObjectToScene(_panel, scene);
        _panel.transform.SetParent(xrCamera.transform, false);
        _panel.transform.localPosition =
            new Vector3(0f, 0f, GetSafePanelDistance(xrCamera));
        _panel.transform.localRotation = Quaternion.identity;
        FitPanelToView(_panel.transform, xrCamera);

        Shader panelShader = Shader.Find("Unlit/Transparent");
        if (panelShader == null)
        {
            Debug.LogError(
                "[XRMainMenuRenderTextureAdapter] " +
                "Unlit/Transparent shader is missing.");
            CleanupSession();
            return;
        }

        _panelMaterial = new Material(panelShader)
        {
            name = "MAIN VR UI Material",
            mainTexture = _uiTexture
        };
        _panel.GetComponent<MeshRenderer>().sharedMaterial = _panelMaterial;
        _xrPointerInput = _panel.AddComponent<XRRenderTexturePanelInput>();

        Debug.Log(
            $"[XRMainMenuRenderTextureAdapter] Created " +
            $"{TextureWidth}x{TextureHeight} panel for {scene.name}.");
    }

    private void ConfigureCanvas(Canvas canvas)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution =
                new Vector2(TextureWidth, TextureHeight);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = _uiCamera;
        canvas.planeDistance = 1f;
        canvas.overrideSorting = true;
        canvas.pixelPerfect = false;
    }

    private void ConfigurePointerInput(Scene scene, Canvas canvas)
    {
        EventSystem[] systems =
            Resources.FindObjectsOfTypeAll<EventSystem>();
        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem eventSystem = systems[i];
            if (eventSystem == null ||
                eventSystem.gameObject.scene != scene)
            {
                continue;
            }

            StandaloneInputModule module =
                eventSystem.GetComponent<StandaloneInputModule>();
            if (module == null)
            {
                continue;
            }

            _pointerInput =
                eventSystem.GetComponent<RenderTexturePointerInput>();
            if (_pointerInput == null)
            {
                _pointerInput =
                    eventSystem.gameObject.AddComponent<
                        RenderTexturePointerInput>();
            }

            _pointerInput.SetTargetSize(TextureWidth, TextureHeight);
            module.inputOverride = _pointerInput;
            _xrPointerInput?.Configure(
                eventSystem,
                canvas.GetComponent<GraphicRaycaster>(),
                TextureWidth,
                TextureHeight);
        }

        if (_cursor == null)
        {
            CreateCursor(canvas);
        }
    }

    private void CreateCursor(Canvas canvas)
    {
        _cursorTexture = new Texture2D(
            1,
            1,
            TextureFormat.RGBA32,
            false)
        {
            name = "MAIN VR Cursor Texture"
        };
        _cursorTexture.SetPixel(0, 0, Color.white);
        _cursorTexture.Apply(false, true);

        GameObject cursorObject = new GameObject(
            "VR Mouse Cursor",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage));
        cursorObject.transform.SetParent(canvas.transform, false);
        _cursor = cursorObject.GetComponent<RectTransform>();
        _cursor.anchorMin = Vector2.zero;
        _cursor.anchorMax = Vector2.zero;
        _cursor.pivot = new Vector2(0.5f, 0.5f);
        _cursor.sizeDelta = new Vector2(18f, 18f);
        _cursor.SetAsLastSibling();

        RawImage image = cursorObject.GetComponent<RawImage>();
        image.texture = _cursorTexture;
        image.color = new Color(0.25f, 0.9f, 1f, 1f);
        image.raycastTarget = false;
    }

    private void CleanupSession()
    {
        if (_refreshCoroutine != null)
        {
            StopCoroutine(_refreshCoroutine);
            _refreshCoroutine = null;
        }

        if (_pointerInput != null)
        {
            StandaloneInputModule module =
                _pointerInput.GetComponent<StandaloneInputModule>();
            if (module != null && module.inputOverride == _pointerInput)
            {
                module.inputOverride = null;
            }
        }

        _pointerInput = null;
        _xrPointerInput = null;
        _cursor = null;

        DestroyRuntimeObject(_panel);
        DestroyRuntimeObject(
            _uiCamera != null ? _uiCamera.gameObject : null);
        DestroyRuntimeObject(_panelMaterial);
        DestroyRuntimeObject(_cursorTexture);
        _panel = null;
        _uiCamera = null;
        _panelMaterial = null;
        _cursorTexture = null;

        if (_uiTexture != null)
        {
            _uiTexture.Release();
            DestroyRuntimeObject(_uiTexture);
            _uiTexture = null;
        }

        _sessionSceneHandle = -1;
    }

    private static Canvas[] FindSceneCanvases(Scene scene)
    {
        Canvas[] allCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
        int count = 0;
        for (int i = 0; i < allCanvases.Length; i++)
        {
            Canvas canvas = allCanvases[i];
            if (IsPresentationCanvas(canvas, scene))
            {
                count++;
            }
        }

        Canvas[] result = new Canvas[count];
        int index = 0;
        for (int i = 0; i < allCanvases.Length; i++)
        {
            Canvas canvas = allCanvases[i];
            if (IsPresentationCanvas(canvas, scene))
            {
                result[index++] = canvas;
            }
        }

        return result;
    }

    private static bool IsPresentationCanvas(Canvas canvas, Scene scene)
    {
        return canvas != null &&
               canvas.isRootCanvas &&
               canvas.gameObject.scene == scene &&
               canvas.renderMode != RenderMode.WorldSpace &&
               !canvas.name.Equals(
                   CombatHudCanvasName,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static Camera FindBestSceneCamera(Scene scene)
    {
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        Camera best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null ||
                candidate.gameObject.scene != scene ||
                !candidate.isActiveAndEnabled ||
                candidate.targetTexture != null)
            {
                continue;
            }

            int score = Mathf.RoundToInt(candidate.depth * 10f);
            if (candidate.CompareTag("MainCamera"))
            {
                score += 1000;
            }

            if (candidate.stereoTargetEye == StereoTargetEyeMask.Both)
            {
                score += 100;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static void FitPanelToView(Transform panel, Camera xrCamera)
    {
        float distance = GetSafePanelDistance(xrCamera);
        float verticalSize = 2f * distance *
            Mathf.Tan(xrCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float horizontalSize = verticalSize *
            Mathf.Max(0.01f, xrCamera.aspect);
        float targetAspect = (float)TextureWidth / TextureHeight;

        float width = horizontalSize * ViewMargin;
        float height = width / targetAspect;
        float maximumHeight = verticalSize * ViewMargin;
        if (height > maximumHeight)
        {
            height = maximumHeight;
            width = height * targetAspect;
        }

        panel.localScale = new Vector3(width, height, 1f);
    }

    private static float GetSafePanelDistance(Camera xrCamera)
    {
        return Mathf.Clamp(
            PanelDistance,
            xrCamera.nearClipPlane + 0.05f,
            Mathf.Max(
                xrCamera.nearClipPlane + 0.05f,
                xrCamera.farClipPlane - 0.05f));
    }

    private static void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (target != null)
        {
            Destroy(target);
        }
    }
}

/// <summary>
/// XR Ray Interactor가 Render Texture 패널에 맞힌 지점을 UI 픽셀 좌표로
/// 변환하고 기존 GraphicRaycaster에 전달한다.
/// </summary>
public sealed class XRRenderTexturePanelInput : XRBaseInteractable
{
    private EventSystem _eventSystem;
    private GraphicRaycaster _graphicRaycaster;
    private Vector2 _targetSize = new Vector2(1920f, 1080f);
    private readonly System.Collections.Generic.List<RaycastResult> _results =
        new System.Collections.Generic.List<RaycastResult>();

    private PointerEventData _pointer;
    private GameObject _hoverTarget;
    private GameObject _pressedTarget;
    private GameObject _dragTarget;
    private Vector2 _previousPosition;

    public void Configure(
        EventSystem eventSystem,
        GraphicRaycaster graphicRaycaster,
        int width,
        int height)
    {
        _eventSystem = eventSystem;
        _graphicRaycaster = graphicRaycaster;
        _targetSize = new Vector2(
            Mathf.Max(1, width),
            Mathf.Max(1, height));
        _pointer = new PointerEventData(eventSystem)
        {
            pointerId = -100
        };
    }

    private static bool UsesHeadLockedMenuPanel(Scene scene)
    {
        // 3D 월드 장면(STATION, STAGE1 등)에 전체 화면 메뉴 Quad를 만들면
        // 불투명 UI 배경이 카메라 앞을 덮어 단색 화면처럼 보일 수 있다.
        switch (scene.name)
        {
            case "MAIN":
            case "LOGIN":
            case "MAP_SELECT":
            case "MULTIPLAYER":
            case "BASE_HANGAR":
            case "RESULT":
            case "LOADING_SEQUENCE":
                return true;
            default:
                return false;
        }
    }

    private void Update()
    {
        XRRayInteractor ray = FindHoveringRay();
        if (ray == null || !TryUpdatePointer(ray))
        {
            SetHoverTarget(null);
            return;
        }

        if (_pressedTarget != null && _dragTarget != null)
        {
            ExecuteEvents.Execute(
                _dragTarget,
                _pointer,
                ExecuteEvents.dragHandler);
        }
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);

        XRRayInteractor ray = args.interactorObject as XRRayInteractor;
        if (ray == null || !TryUpdatePointer(ray) || _hoverTarget == null)
        {
            return;
        }

        _pressedTarget = _hoverTarget;
        _pointer.pointerPress = _pressedTarget;
        _pointer.pressPosition = _pointer.position;
        ExecuteEvents.Execute(
            _pressedTarget,
            _pointer,
            ExecuteEvents.pointerDownHandler);

        _dragTarget = ExecuteEvents.GetEventHandler<IDragHandler>(_pressedTarget);
        _pointer.pointerDrag = _dragTarget;
        if (_dragTarget != null)
        {
            ExecuteEvents.Execute(
                _dragTarget,
                _pointer,
                ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(
                _dragTarget,
                _pointer,
                ExecuteEvents.beginDragHandler);
            _pointer.dragging = true;
        }
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        if (_pressedTarget != null && _pointer != null)
        {
            if (_dragTarget != null)
            {
                ExecuteEvents.Execute(
                    _dragTarget,
                    _pointer,
                    ExecuteEvents.endDragHandler);
            }

            ExecuteEvents.Execute(
                _pressedTarget,
                _pointer,
                ExecuteEvents.pointerUpHandler);

            if (_pressedTarget == _hoverTarget)
            {
                ExecuteEvents.Execute(
                    _pressedTarget,
                    _pointer,
                    ExecuteEvents.pointerClickHandler);
            }
        }

        _pressedTarget = null;
        _dragTarget = null;
        if (_pointer != null)
        {
            _pointer.pointerPress = null;
            _pointer.pointerDrag = null;
            _pointer.dragging = false;
        }

        base.OnSelectExited(args);
    }

    protected override void OnDisable()
    {
        SetHoverTarget(null);
        _pressedTarget = null;
        _dragTarget = null;
        base.OnDisable();
    }

    private XRRayInteractor FindHoveringRay()
    {
        for (int i = 0; i < interactorsHovering.Count; i++)
        {
            if (interactorsHovering[i] is XRRayInteractor ray)
            {
                return ray;
            }
        }

        return null;
    }

    private bool TryUpdatePointer(XRRayInteractor ray)
    {
        if (_eventSystem == null ||
            _graphicRaycaster == null ||
            _pointer == null ||
            !ray.TryGetCurrent3DRaycastHit(out RaycastHit hit) ||
            hit.collider == null ||
            hit.collider.gameObject != gameObject)
        {
            return false;
        }

        Vector3 localHit = transform.InverseTransformPoint(hit.point);
        float normalizedX = Mathf.Clamp01(localHit.x + 0.5f);
        float normalizedY = Mathf.Clamp01(localHit.y + 0.5f);
        Vector2 nextPosition = new Vector2(
            normalizedX * _targetSize.x,
            normalizedY * _targetSize.y);
        _pointer.delta = nextPosition - _previousPosition;
        _pointer.position = nextPosition;
        _previousPosition = nextPosition;

        _results.Clear();
        _graphicRaycaster.Raycast(_pointer, _results);
        SetHoverTarget(_results.Count > 0 ? _results[0].gameObject : null);
        return true;
    }

    private void SetHoverTarget(GameObject target)
    {
        if (_hoverTarget == target || _pointer == null)
        {
            return;
        }

        if (_hoverTarget != null)
        {
            ExecuteEvents.Execute(
                _hoverTarget,
                _pointer,
                ExecuteEvents.pointerExitHandler);
        }

        _hoverTarget = target;
        _pointer.pointerEnter = target;

        if (_hoverTarget != null)
        {
            ExecuteEvents.Execute(
                _hoverTarget,
                _pointer,
                ExecuteEvents.pointerEnterHandler);
        }
    }
}

/// <summary>
/// Maps desktop mouse coordinates into the fixed render-texture coordinate
/// system consumed by StandaloneInputModule and GraphicRaycaster.
/// </summary>
public sealed class RenderTexturePointerInput : BaseInput
{
    private Vector2 _targetSize = new Vector2(1920f, 1080f);

    public void SetTargetSize(int width, int height)
    {
        _targetSize = new Vector2(
            Mathf.Max(1, width),
            Mathf.Max(1, height));
    }

    public override Vector2 mousePosition
    {
        get
        {
            Vector2 position = base.mousePosition;
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return position;
            }

            return new Vector2(
                Mathf.Clamp01(position.x / Screen.width) * _targetSize.x,
                Mathf.Clamp01(position.y / Screen.height) * _targetSize.y);
        }
    }
}
