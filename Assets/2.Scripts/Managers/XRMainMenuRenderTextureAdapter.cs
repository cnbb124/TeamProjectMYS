using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    private const string PanelShaderName = "MYS/XR Always On Top UI";
    private const string FixedPixelCanvasSceneName = "BASE_HANGAR";
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
    private EventSystem _mouseOnlyEventSystem;
    private bool _originalSendNavigationEvents;
    private readonly List<SelectableNavigationState> _navigationStates =
        new List<SelectableNavigationState>();
    private PointerEventData _pointerDiagnosticData;
    private EventSystem _pointerDiagnosticEventSystem;
    private readonly List<RaycastResult> _pointerDiagnosticResults =
        new List<RaycastResult>();
    private GameObject _lastPointerDiagnosticTarget;
    private bool _hasPointerDiagnosticTarget;
    private StandaloneInputModule _legacyPointerModule;
    private bool _legacyPointerModuleWasEnabled;
    private GameObject _manualHoverTarget;
    private GameObject _manualPressTarget;
    private GameObject _manualDragTarget;
    private Vector2 _previousManualPointerPosition;
    private bool _manualDragging;
    private int _sessionSceneHandle = -1;

    private struct SelectableNavigationState
    {
        public Selectable Selectable;
        public Navigation Navigation;
    }

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
            Vector2 pointerPosition = _pointerInput.mousePosition;
            bool isVisible = pointerPosition.x >= 0f &&
                             pointerPosition.y >= 0f;
            if (_cursor.gameObject.activeSelf != isVisible)
            {
                _cursor.gameObject.SetActive(isVisible);
            }

            if (isVisible)
            {
                _cursor.anchoredPosition = pointerPosition;
            }

            ProcessManualPointer(pointerPosition);
        }
    }

    private void ProcessManualPointer(Vector2 pointerPosition)
    {
        if (_mouseOnlyEventSystem == null || _pointerInput == null)
        {
            return;
        }

        if (_pointerDiagnosticData == null ||
            _pointerDiagnosticEventSystem != _mouseOnlyEventSystem)
        {
            _pointerDiagnosticEventSystem = _mouseOnlyEventSystem;
            _pointerDiagnosticData =
                new PointerEventData(_mouseOnlyEventSystem);
            _hasPointerDiagnosticTarget = false;
            _previousManualPointerPosition = pointerPosition;
        }

        bool isValid = pointerPosition.x >= 0f && pointerPosition.y >= 0f;
        _pointerDiagnosticData.position = pointerPosition;
        _pointerDiagnosticData.delta =
            pointerPosition - _previousManualPointerPosition;
        _pointerDiagnosticData.button = PointerEventData.InputButton.Left;
        _previousManualPointerPosition = pointerPosition;
        _pointerDiagnosticResults.Clear();
        if (isValid)
        {
            _mouseOnlyEventSystem.RaycastAll(
                _pointerDiagnosticData,
                _pointerDiagnosticResults);
        }

        GameObject target = _pointerDiagnosticResults.Count > 0
            ? _pointerDiagnosticResults[0].gameObject
            : null;
        _pointerDiagnosticData.pointerCurrentRaycast =
            _pointerDiagnosticResults.Count > 0
                ? _pointerDiagnosticResults[0]
                : new RaycastResult();

        GameObject hoverTarget = target != null
            ? ExecuteEvents.GetEventHandler<IPointerEnterHandler>(target)
            : null;
        SetManualHoverTarget(hoverTarget);
        ApplyExclusiveHoverVisual(hoverTarget);

        if (_pointerInput.GetMouseButtonDown(0) && target != null)
        {
            BeginManualPress(target);
        }

        if (_pointerInput.GetMouseButton(0) &&
            _manualDragTarget != null &&
            _pointerDiagnosticData.delta.sqrMagnitude > 0f)
        {
            if (!_manualDragging)
            {
                ExecuteEvents.Execute(
                    _manualDragTarget,
                    _pointerDiagnosticData,
                    ExecuteEvents.beginDragHandler);
                _manualDragging = true;
                _pointerDiagnosticData.dragging = true;
            }

            ExecuteEvents.Execute(
                _manualDragTarget,
                _pointerDiagnosticData,
                ExecuteEvents.dragHandler);
        }

        if (_pointerInput.GetMouseButtonUp(0))
        {
            EndManualPress(target);
        }

        LogPointerTarget(pointerPosition, target);
    }

    private void SetManualHoverTarget(GameObject target)
    {
        if (_manualHoverTarget == target || _pointerDiagnosticData == null)
        {
            return;
        }

        if (_manualHoverTarget != null)
        {
            ExecuteEvents.Execute(
                _manualHoverTarget,
                _pointerDiagnosticData,
                ExecuteEvents.pointerExitHandler);
        }

        _manualHoverTarget = target;
        _pointerDiagnosticData.pointerEnter = target;
        if (_manualHoverTarget != null)
        {
            ExecuteEvents.Execute(
                _manualHoverTarget,
                _pointerDiagnosticData,
                ExecuteEvents.pointerEnterHandler);
        }
    }

    private void ApplyExclusiveHoverVisual(GameObject hoverTarget)
    {
        for (int i = 0; i < _navigationStates.Count; i++)
        {
            Selectable selectable = _navigationStates[i].Selectable;
            if (selectable == null)
            {
                continue;
            }

            LobbyButtonHover hoverEffect =
                selectable.GetComponent<LobbyButtonHover>();
            if (hoverEffect != null)
            {
                hoverEffect.SetImmediateState(
                    selectable.gameObject == hoverTarget);
            }
        }
    }

    private void BeginManualPress(GameObject target)
    {
        _pointerDiagnosticData.eligibleForClick = true;
        _pointerDiagnosticData.dragging = false;
        _pointerDiagnosticData.useDragThreshold = true;
        _pointerDiagnosticData.pressPosition =
            _pointerDiagnosticData.position;
        _pointerDiagnosticData.pointerPressRaycast =
            _pointerDiagnosticData.pointerCurrentRaycast;

        _manualPressTarget = ExecuteEvents.ExecuteHierarchy(
            target,
            _pointerDiagnosticData,
            ExecuteEvents.pointerDownHandler);
        if (_manualPressTarget == null)
        {
            _manualPressTarget =
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
        }

        _pointerDiagnosticData.pointerPress = _manualPressTarget;
        _pointerDiagnosticData.rawPointerPress = target;
        _manualDragTarget =
            ExecuteEvents.GetEventHandler<IDragHandler>(target);
        _pointerDiagnosticData.pointerDrag = _manualDragTarget;
        _manualDragging = false;
        if (_manualDragTarget != null)
        {
            ExecuteEvents.Execute(
                _manualDragTarget,
                _pointerDiagnosticData,
                ExecuteEvents.initializePotentialDrag);
        }
    }

    private void EndManualPress(GameObject target)
    {
        if (_manualPressTarget != null)
        {
            ExecuteEvents.Execute(
                _manualPressTarget,
                _pointerDiagnosticData,
                ExecuteEvents.pointerUpHandler);

            GameObject clickTarget = target != null
                ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(target)
                : null;
            if (_pointerDiagnosticData.eligibleForClick &&
                clickTarget == _manualPressTarget)
            {
                ExecuteEvents.Execute(
                    _manualPressTarget,
                    _pointerDiagnosticData,
                    ExecuteEvents.pointerClickHandler);
            }
        }

        if (_manualDragging && _manualDragTarget != null)
        {
            ExecuteEvents.Execute(
                _manualDragTarget,
                _pointerDiagnosticData,
                ExecuteEvents.endDragHandler);
        }

        _manualPressTarget = null;
        _manualDragTarget = null;
        _manualDragging = false;
        _pointerDiagnosticData.eligibleForClick = false;
        _pointerDiagnosticData.dragging = false;
        _pointerDiagnosticData.pointerPress = null;
        _pointerDiagnosticData.rawPointerPress = null;
        _pointerDiagnosticData.pointerDrag = null;
    }

    private void LogPointerTarget(
        Vector2 pointerPosition,
        GameObject target)
    {
        Selectable selectable = target != null
            ? target.GetComponentInParent<Selectable>()
            : null;
        GameObject diagnosticTarget = selectable != null
            ? selectable.gameObject
            : null;
        if (_hasPointerDiagnosticTarget &&
            diagnosticTarget == _lastPointerDiagnosticTarget)
        {
            return;
        }

        _hasPointerDiagnosticTarget = true;
        _lastPointerDiagnosticTarget = diagnosticTarget;
        GameObject selected =
            _mouseOnlyEventSystem.currentSelectedGameObject;
        Debug.Log(
            $"[XR Mouse Target Diagnostic] mapped={pointerPosition:F1}, " +
            $"raycast={(target != null ? target.name : "<none>")}, " +
            $"selectable={(selectable != null ? selectable.name : "<none>")}, " +
            $"selected={(selected != null ? selected.name : "<none>")}");
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

        ConfigurePointerInput(scene, canvases[0], xrCamera);
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

        Shader panelShader = Resources.Load<Shader>("XRAlwaysOnTopUI");
        if (panelShader == null)
        {
            panelShader = Shader.Find(PanelShaderName);
        }

        if (panelShader == null)
        {
            panelShader = Shader.Find("Unlit/Transparent");
            Debug.LogWarning(
                "[XRMainMenuRenderTextureAdapter] " +
                $"{PanelShaderName} shader is missing. " +
                "Falling back to Unlit/Transparent.");
        }

        if (panelShader == null)
        {
            Debug.LogError(
                "[XRMainMenuRenderTextureAdapter] " +
                "No compatible panel shader was found.");
            CleanupSession();
            return;
        }

        _panelMaterial = new Material(panelShader)
        {
            name = "MAIN VR UI Material",
            mainTexture = _uiTexture
        };
        _panelMaterial.renderQueue = 5000;
        _panel.GetComponent<MeshRenderer>().sharedMaterial = _panelMaterial;

        Debug.Log(
            $"[XRMainMenuRenderTextureAdapter] Created " +
            $"{TextureWidth}x{TextureHeight} panel for {scene.name}.");
    }

    private void ConfigureCanvas(Canvas canvas)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            if (canvas.gameObject.scene.name == FixedPixelCanvasSceneName)
            {
                // HangarUI is authored with fixed 1920 x 1080 positions.
                // Scaling it against the Editor Game View resolution makes
                // the complete UI gather in a small area at the upper-left of
                // the 1920 x 1080 render texture.
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
                scaler.referencePixelsPerUnit = 100f;
            }
            else
            {
                scaler.uiScaleMode =
                    CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution =
                    new Vector2(TextureWidth, TextureHeight);
                scaler.screenMatchMode =
                    CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = _uiCamera;
        canvas.planeDistance = 1f;
        canvas.overrideSorting = true;
        canvas.pixelPerfect = false;
    }

    private void ConfigurePointerInput(
        Scene scene,
        Canvas canvas,
        Camera xrCamera)
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

            ConfigureMouseOnlyNavigation(scene, eventSystem);

            _pointerInput =
                eventSystem.GetComponent<RenderTexturePointerInput>();
            if (_pointerInput == null)
            {
                _pointerInput =
                    eventSystem.gameObject.AddComponent<
                        RenderTexturePointerInput>();
            }

            _pointerInput.SetTargetPanel(
                TextureWidth,
                TextureHeight,
                xrCamera,
                _panel.transform,
                ViewMargin);
            if (_legacyPointerModule != module)
            {
                RestoreLegacyPointerModule();
                _legacyPointerModule = module;
                _legacyPointerModuleWasEnabled = module.enabled;
            }

            module.inputOverride = _pointerInput;
            module.enabled = false;
        }

        if (_cursor == null)
        {
            CreateCursor(canvas);
        }
    }

    private void ConfigureMouseOnlyNavigation(
        Scene scene,
        EventSystem eventSystem)
    {
        if (_mouseOnlyEventSystem != eventSystem)
        {
            RestoreNavigationSettings();
            _mouseOnlyEventSystem = eventSystem;
            _originalSendNavigationEvents =
                eventSystem.sendNavigationEvents;

            Selectable[] selectables =
                Resources.FindObjectsOfTypeAll<Selectable>();
            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable selectable = selectables[i];
                if (selectable == null ||
                    selectable.gameObject.scene != scene)
                {
                    continue;
                }

                _navigationStates.Add(
                    new SelectableNavigationState
                    {
                        Selectable = selectable,
                        Navigation = selectable.navigation
                    });

                Navigation navigation = selectable.navigation;
                navigation.mode = Navigation.Mode.None;
                selectable.navigation = navigation;
            }

            ResetSelectablePointerStates(eventSystem);
        }

        eventSystem.sendNavigationEvents = false;
        eventSystem.SetSelectedGameObject(null);
    }

    private void ResetSelectablePointerStates(EventSystem eventSystem)
    {
        PointerEventData resetData = new PointerEventData(eventSystem);
        for (int i = 0; i < _navigationStates.Count; i++)
        {
            Selectable selectable = _navigationStates[i].Selectable;
            if (selectable == null)
            {
                continue;
            }

            ExecuteEvents.Execute(
                selectable.gameObject,
                resetData,
                ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(
                selectable.gameObject,
                resetData,
                ExecuteEvents.pointerExitHandler);
            ExecuteEvents.Execute(
                selectable.gameObject,
                resetData,
                ExecuteEvents.deselectHandler);

            LobbyButtonHover hoverEffect =
                selectable.GetComponent<LobbyButtonHover>();
            if (hoverEffect != null)
            {
                hoverEffect.ResetVisualState();
            }
        }
    }

    private void RestoreNavigationSettings()
    {
        if (_mouseOnlyEventSystem != null)
        {
            _mouseOnlyEventSystem.sendNavigationEvents =
                _originalSendNavigationEvents;
        }

        for (int i = 0; i < _navigationStates.Count; i++)
        {
            SelectableNavigationState state = _navigationStates[i];
            if (state.Selectable != null)
            {
                state.Selectable.navigation = state.Navigation;
            }
        }

        _navigationStates.Clear();
        _mouseOnlyEventSystem = null;
        _pointerDiagnosticData = null;
        _pointerDiagnosticEventSystem = null;
        _pointerDiagnosticResults.Clear();
        _lastPointerDiagnosticTarget = null;
        _hasPointerDiagnosticTarget = false;
        _manualHoverTarget = null;
        _manualPressTarget = null;
        _manualDragTarget = null;
        _manualDragging = false;
    }

    private void RestoreLegacyPointerModule()
    {
        if (_legacyPointerModule != null)
        {
            if (_legacyPointerModule.inputOverride == _pointerInput)
            {
                _legacyPointerModule.inputOverride = null;
            }

            _legacyPointerModule.enabled =
                _legacyPointerModuleWasEnabled;
        }

        _legacyPointerModule = null;
        _legacyPointerModuleWasEnabled = false;
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

        RestoreLegacyPointerModule();
        RestoreNavigationSettings();

        _pointerInput = null;
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
/// Maps desktop mouse coordinates into the fixed render-texture coordinate
/// system consumed by StandaloneInputModule and GraphicRaycaster.
/// </summary>
public sealed class RenderTexturePointerInput : BaseInput
{
    private Vector2 _targetSize = new Vector2(1920f, 1080f);
    private Camera _rayCamera;
    private Transform _panel;
    private bool _loggedPanelDiagnostic;
    private Vector2 _gameViewMousePosition;
    private bool _hasGameViewCoordinate;
    private bool _isPointerInsideGameView;
    private bool _loggedGameViewCoordinate;
    private float _viewportMargin = 0.82f;
    private bool _usedFallbackViewport;
    private const Camera.MonoOrStereoscopicEye MouseProjectionEye =
        Camera.MonoOrStereoscopicEye.Mono;

    public void SetTargetPanel(
        int width,
        int height,
        Camera rayCamera,
        Transform panel,
        float viewportMargin)
    {
        _targetSize = new Vector2(
            Mathf.Max(1, width),
            Mathf.Max(1, height));
        if (_rayCamera != rayCamera || _panel != panel)
        {
            _loggedPanelDiagnostic = false;
        }

        _rayCamera = rayCamera;
        _panel = panel;
        _viewportMargin = Mathf.Clamp(viewportMargin, 0.01f, 1f);

        if (!_loggedPanelDiagnostic)
        {
            _loggedPanelDiagnostic = true;
            bool centerHit = TryMapViewportPosition(
                new Vector2(0.5f, 0.5f),
                out Vector2 centerPosition);
            bool hasPanelRect = TryGetPanelViewportRect(
                out Rect panelViewportRect);
            Debug.Log(
                $"[XR Mouse Panel Diagnostic] eye={MouseProjectionEye}, " +
                $"fallback={_usedFallbackViewport}, " +
                $"yFlipped=False, " +
                $"rect={(hasPanelRect ? panelViewportRect.ToString() : "<invalid>")}, " +
                $"centerHit={centerHit}, mapped={centerPosition:F1}");
        }
    }

    private void OnGUI()
    {
        Event guiEvent = Event.current;
        if (guiEvent == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        Vector2 guiPosition = guiEvent.mousePosition;
        _gameViewMousePosition = new Vector2(
            guiPosition.x,
            Screen.height - guiPosition.y);
        _hasGameViewCoordinate = true;
        _isPointerInsideGameView =
            _gameViewMousePosition.x >= 0f &&
            _gameViewMousePosition.x <= Screen.width &&
            _gameViewMousePosition.y >= 0f &&
            _gameViewMousePosition.y <= Screen.height;

        if (_isPointerInsideGameView && !_loggedGameViewCoordinate)
        {
            _loggedGameViewCoordinate = true;
            Debug.Log(
                $"[XR Mouse GameView Diagnostic] " +
                $"local={_gameViewMousePosition:F1}, " +
                $"Screen={Screen.width}x{Screen.height}");
        }
    }

    public override Vector2 mousePosition
    {
        get
        {
            if (_hasGameViewCoordinate && !_isPointerInsideGameView)
            {
                return new Vector2(-1f, -1f);
            }

            Vector2 position = _hasGameViewCoordinate
                ? _gameViewMousePosition
                : base.mousePosition;
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return position;
            }

            Vector2 viewportPosition = new Vector2(
                position.x / Screen.width,
                position.y / Screen.height);
            if (viewportPosition.x < 0f || viewportPosition.x > 1f ||
                viewportPosition.y < 0f || viewportPosition.y > 1f ||
                !TryMapViewportPosition(
                    viewportPosition,
                    out Vector2 mappedPosition))
            {
                return new Vector2(-1f, -1f);
            }

            return mappedPosition;
        }
    }

    private bool TryMapViewportPosition(
        Vector2 viewportPosition,
        out Vector2 mappedPosition)
    {
        mappedPosition = new Vector2(-1f, -1f);
        if (!TryGetPanelViewportRect(out Rect panelViewportRect) ||
            viewportPosition.x < panelViewportRect.xMin ||
            viewportPosition.x > panelViewportRect.xMax ||
            viewportPosition.y < panelViewportRect.yMin ||
            viewportPosition.y > panelViewportRect.yMax)
        {
            return false;
        }

        float normalizedX = Mathf.InverseLerp(
            panelViewportRect.xMin,
            panelViewportRect.xMax,
            viewportPosition.x);
        float normalizedY = Mathf.InverseLerp(
            panelViewportRect.yMin,
            panelViewportRect.yMax,
            viewportPosition.y);
        mappedPosition = new Vector2(
            normalizedX * _targetSize.x,
            normalizedY * _targetSize.y);
        return true;
    }

    private bool TryGetPanelViewportRect(out Rect panelViewportRect)
    {
        panelViewportRect = default;
        if (_rayCamera == null || _panel == null)
        {
            return false;
        }

        Vector3 bottomLeft = _rayCamera.WorldToViewportPoint(
            _panel.TransformPoint(new Vector3(-0.5f, -0.5f, 0f)),
            MouseProjectionEye);
        Vector3 bottomRight = _rayCamera.WorldToViewportPoint(
            _panel.TransformPoint(new Vector3(0.5f, -0.5f, 0f)),
            MouseProjectionEye);
        Vector3 topLeft = _rayCamera.WorldToViewportPoint(
            _panel.TransformPoint(new Vector3(-0.5f, 0.5f, 0f)),
            MouseProjectionEye);
        Vector3 topRight = _rayCamera.WorldToViewportPoint(
            _panel.TransformPoint(new Vector3(0.5f, 0.5f, 0f)),
            MouseProjectionEye);

        if (bottomLeft.z <= 0f || bottomRight.z <= 0f ||
            topLeft.z <= 0f || topRight.z <= 0f)
        {
            return TryGetFallbackViewportRect(out panelViewportRect);
        }

        float minX = Mathf.Min(
            bottomLeft.x,
            bottomRight.x,
            topLeft.x,
            topRight.x);
        float maxX = Mathf.Max(
            bottomLeft.x,
            bottomRight.x,
            topLeft.x,
            topRight.x);
        float minY = Mathf.Min(
            bottomLeft.y,
            bottomRight.y,
            topLeft.y,
            topRight.y);
        float maxY = Mathf.Max(
            bottomLeft.y,
            bottomRight.y,
            topLeft.y,
            topRight.y);

        if (maxX - minX <= Mathf.Epsilon ||
            maxY - minY <= Mathf.Epsilon ||
            minX > 0.5f || maxX < 0.5f ||
            minY > 0.5f || maxY < 0.5f ||
            minX < -0.25f || maxX > 1.25f ||
            minY < -0.25f || maxY > 1.25f)
        {
            return TryGetFallbackViewportRect(out panelViewportRect);
        }

        _usedFallbackViewport = false;
        panelViewportRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    private bool TryGetFallbackViewportRect(out Rect viewportRect)
    {
        viewportRect = default;
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            return false;
        }

        float screenAspect = (float)Screen.width / Screen.height;
        float targetAspect = _targetSize.x / _targetSize.y;
        float width;
        float height;
        if (screenAspect >= targetAspect)
        {
            height = _viewportMargin;
            width = height * targetAspect / screenAspect;
        }
        else
        {
            width = _viewportMargin;
            height = width * screenAspect / targetAspect;
        }

        _usedFallbackViewport = true;
        viewportRect = new Rect(
            (1f - width) * 0.5f,
            (1f - height) * 0.5f,
            width,
            height);
        return true;
    }
}
