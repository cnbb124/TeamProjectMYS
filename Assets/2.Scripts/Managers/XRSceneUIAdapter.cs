using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Makes scene-owned screen-space UI visible to both eyes while XR is running.
/// The UI itself remains scene-owned and is attached only to its scene camera.
/// </summary>
[DefaultExecutionOrder(-19000)]
public sealed class XRSceneUIAdapter : MonoBehaviour
{
    private const string CombatHudCanvasName = "HUD";
    private const string StationSceneName = "BASE_STATION";
    private const string StationPlayerButtonCanvasName = "PlayerButtonUI";
    private const float UiPlaneDistance = 1.5f;
    private const float ViewMargin = 0.9f;
    private const int RefreshFrameCount = 12;
    private static readonly Vector2 ReferenceResolution =
        new Vector2(1920f, 1080f);

    private static XRSceneUIAdapter _instance;
    private Coroutine _refreshCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject adapterObject = new GameObject(nameof(XRSceneUIAdapter));
        _instance = adapterObject.AddComponent<XRSceneUIAdapter>();
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
        XRRuntimeManager.Started += OnXrStarted;
    }

    private void Start()
    {
        if (XRRuntimeManager.IsRunning)
        {
            QueueRefresh();
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        XRRuntimeManager.Started -= OnXrStarted;

        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (XRRuntimeManager.IsRunning)
        {
            QueueRefresh();
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
        // Repeat because scene managers can enable their camera or UI after
        // SceneManager.sceneLoaded is raised.
        for (int i = 0; i < RefreshFrameCount; i++)
        {
            if (!XRRuntimeManager.IsRunning)
            {
                break;
            }

            AdaptSceneCanvases(SceneManager.GetActiveScene());
            yield return null;
        }

        _refreshCoroutine = null;
    }

    private static void AdaptSceneCanvases(Scene activeScene)
    {
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            return;
        }

        // Menu scenes are rendered through XRMainMenuRenderTextureAdapter.
        // Adapting the same canvases here would make both adapters overwrite
        // their render mode, camera and EventSystem input configuration.
        if (UsesHeadLockedMenuPanel(activeScene))
        {
            return;
        }

        Camera sceneCamera = FindBestSceneCamera(activeScene);
        if (sceneCamera == null)
        {
            return;
        }

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
        {
            sceneCamera.cullingMask |= 1 << uiLayer;
        }

        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null ||
                !canvas.isRootCanvas ||
                canvas.gameObject.scene != activeScene ||
                canvas.renderMode == RenderMode.WorldSpace ||
                canvas.name.Equals(
                    CombatHudCanvasName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FitCanvasToXrView(canvas, sceneCamera);
            Debug.Log(
                $"[XRSceneUIAdapter] {activeScene.name}: " +
                $"{canvas.name} -> {sceneCamera.name}");
        }

        ConfigurePointerInput(sceneCamera);
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

    private static Camera FindBestSceneCamera(Scene activeScene)
    {
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        Camera bestCamera = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null ||
                candidate.gameObject.scene != activeScene ||
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
                bestCamera = candidate;
            }
        }

        return bestCamera;
    }

    private static void FitCanvasToXrView(
        Canvas canvas,
        Camera sceneCamera)
    {
        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            // Transform scale controls world size. Eye texture resolution must
            // not change the authored 1920 x 1080 coordinate system.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;
        }

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = sceneCamera;
        canvas.overrideSorting = true;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 1000;

        canvasRect.SetParent(sceneCamera.transform, false);
        canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
        canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
        canvasRect.pivot = new Vector2(0.5f, 0.5f);
        canvasRect.sizeDelta = ReferenceResolution;
        canvasRect.localRotation = Quaternion.identity;

        float distance = GetSafePlaneDistance(sceneCamera);
        canvasRect.localPosition = new Vector3(0f, 0f, distance);

        float verticalWorldSize = 2f * distance *
            Mathf.Tan(sceneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float horizontalWorldSize = verticalWorldSize *
            Mathf.Max(0.01f, sceneCamera.aspect);
        float scale = Mathf.Min(
            horizontalWorldSize / ReferenceResolution.x,
            verticalWorldSize / ReferenceResolution.y) * ViewMargin;
        canvasRect.localScale = Vector3.one * scale;

        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
        {
            raycaster.enabled = true;
        }

        TrackedDeviceGraphicRaycaster trackedRaycaster =
            canvas.GetComponent<TrackedDeviceGraphicRaycaster>();
        if (trackedRaycaster == null)
        {
            trackedRaycaster = canvas.gameObject.AddComponent<
                TrackedDeviceGraphicRaycaster>();
        }

        trackedRaycaster.enabled = true;
    }

    private static void ConfigurePointerInput(Camera sceneCamera)
    {
        EventSystem[] eventSystems =
            Resources.FindObjectsOfTypeAll<EventSystem>();

        for (int i = 0; i < eventSystems.Length; i++)
        {
            EventSystem eventSystem = eventSystems[i];
            if (eventSystem == null ||
                eventSystem.gameObject.scene != sceneCamera.gameObject.scene)
            {
                continue;
            }

            StandaloneInputModule legacyModule =
                eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                legacyModule.enabled = false;
            }

            XRUIInputModule xrModule =
                eventSystem.GetComponent<XRUIInputModule>();
            if (xrModule == null)
            {
                xrModule = eventSystem.gameObject.AddComponent<XRUIInputModule>();
            }

            xrModule.enableXRInput = true;
            xrModule.enabled = true;

            bool useStationLocalMouse =
                sceneCamera.gameObject.scene.name == StationSceneName;
            XRStationGameViewPointer stationPointer =
                eventSystem.GetComponent<XRStationGameViewPointer>();
            if (useStationLocalMouse)
            {
                if (stationPointer == null)
                {
                    stationPointer = eventSystem.gameObject.AddComponent<
                        XRStationGameViewPointer>();
                }

                stationPointer.Configure(
                    eventSystem,
                    xrModule,
                    sceneCamera,
                    FindSceneCanvas(
                        sceneCamera.gameObject.scene,
                        StationPlayerButtonCanvasName));
                stationPointer.enabled = true;
            }
            else
            {
                if (stationPointer != null)
                {
                    stationPointer.enabled = false;
                }

                xrModule.enableMouseInput = true;
            }
        }
    }

    private static Canvas FindSceneCanvas(Scene scene, string canvasName)
    {
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null &&
                canvas.gameObject.scene == scene &&
                canvas.name.Equals(
                    canvasName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return canvas;
            }
        }

        return null;
    }

    private static float GetSafePlaneDistance(Camera sceneCamera)
    {
        float minimum = sceneCamera.nearClipPlane + 0.05f;
        float maximum = Mathf.Max(
            minimum,
            sceneCamera.farClipPlane - 0.05f);
        return Mathf.Clamp(UiPlaneDistance, minimum, maximum);
    }
}

/// <summary>
/// Uses the Unity Editor Game View-local mouse coordinate for the station's
/// single/multiplayer panel. The normal XR mouse path can report desktop/editor
/// coordinates, so this pointer owns UI input while the panel is visible.
/// This prevents a stationary XR ray from keeping the other button highlighted
/// or overwriting the mouse hover/click target.
/// </summary>
[DisallowMultipleComponent]
public sealed class XRStationGameViewPointer : MonoBehaviour
{
    private EventSystem _eventSystem;
    private XRUIInputModule _xrInputModule;
    private Camera _eventCamera;
    private Canvas _targetCanvas;
    private Selectable[] _selectables = Array.Empty<Selectable>();
    private RectTransform _cursor;
    private PointerEventData _pointerData;
    private GameObject _hoverTarget;
    private GameObject _pressTarget;
    private GameObject _dragTarget;
    private GameObject _lastDiagnosticTarget;
    private Vector2 _gameViewMousePosition;
    private Vector2 _previousPointerPosition;
    private bool _hasGameViewCoordinate;
    private bool _pointerInsideGameView;
    private bool _dragging;
    private bool _manualInputWasActive;
    private bool _hasDiagnosticTarget;
    private bool _loggedGameViewCoordinate;

    public void Configure(
        EventSystem eventSystem,
        XRUIInputModule xrInputModule,
        Camera eventCamera,
        Canvas targetCanvas)
    {
        bool canvasChanged = _targetCanvas != targetCanvas;
        _eventSystem = eventSystem;
        _xrInputModule = xrInputModule;
        _eventCamera = eventCamera;
        _targetCanvas = targetCanvas;

        if (canvasChanged)
        {
            ClearPointerState();
            _selectables = _targetCanvas != null
                ? _targetCanvas.GetComponentsInChildren<Selectable>(true)
                : Array.Empty<Selectable>();
            CreateCursor();
            _hasDiagnosticTarget = false;
        }

        UpdateMouseInputOwnership();
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
        _pointerInsideGameView =
            _gameViewMousePosition.x >= 0f &&
            _gameViewMousePosition.x <= Screen.width &&
            _gameViewMousePosition.y >= 0f &&
            _gameViewMousePosition.y <= Screen.height;

        if (_pointerInsideGameView && !_loggedGameViewCoordinate)
        {
            _loggedGameViewCoordinate = true;
            Debug.Log(
                $"[XR Station Mouse Diagnostic] " +
                $"local={_gameViewMousePosition:F1}, " +
                $"Screen={Screen.width}x{Screen.height}");
        }
    }

    private void Update()
    {
        bool panelVisible = IsTargetPanelVisible();
        UpdateMouseInputOwnership(panelVisible);
        if (!panelVisible || _eventSystem == null || _eventCamera == null)
        {
            SetCursorVisible(false);
            ClearPointerState();
            return;
        }

        if (!_hasGameViewCoordinate || !_pointerInsideGameView ||
            Screen.width <= 0 || Screen.height <= 0)
        {
            SetCursorVisible(false);
            ProcessPointer(null, new Vector2(-1f, -1f));
            return;
        }

        Vector2 viewportPosition = new Vector2(
            _gameViewMousePosition.x / Screen.width,
            _gameViewMousePosition.y / Screen.height);
        if (!TryFindSelectable(
                viewportPosition,
                out GameObject target,
                out Vector2 canvasPosition))
        {
            target = null;
            canvasPosition = new Vector2(-1f, -1f);
        }

        UpdateCursor(canvasPosition);
        ProcessPointer(target, canvasPosition);
    }

    private bool TryFindSelectable(
        Vector2 viewportPosition,
        out GameObject target,
        out Vector2 canvasPosition)
    {
        target = null;
        canvasPosition = new Vector2(-1f, -1f);
        if (_targetCanvas == null)
        {
            return false;
        }

        RectTransform canvasRect =
            _targetCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return false;
        }

        // Recalculate the point from a mono viewport ray. This avoids the
        // left-eye projection offset used by some XR camera helpers.
        Ray pointerRay = _eventCamera.ViewportPointToRay(
            new Vector3(viewportPosition.x, viewportPosition.y, 0f),
            Camera.MonoOrStereoscopicEye.Mono);
        Plane canvasPlane = new Plane(
            canvasRect.forward,
            canvasRect.position);
        if (!canvasPlane.Raycast(pointerRay, out float distance))
        {
            return false;
        }

        Vector3 worldPoint = pointerRay.GetPoint(distance);
        Vector3 localPoint3 = canvasRect.InverseTransformPoint(worldPoint);
        Vector2 canvasLocalPosition =
            new Vector2(localPoint3.x, localPoint3.y);
        if (!canvasRect.rect.Contains(canvasLocalPosition))
        {
            return false;
        }

        canvasPosition = new Vector2(
            canvasLocalPosition.x - canvasRect.rect.xMin,
            canvasLocalPosition.y - canvasRect.rect.yMin);

        Selectable bestSelectable = null;
        int bestDepth = int.MinValue;
        for (int i = 0; i < _selectables.Length; i++)
        {
            Selectable selectable = _selectables[i];
            if (selectable == null ||
                !selectable.isActiveAndEnabled ||
                !selectable.IsInteractable())
            {
                continue;
            }

            RectTransform selectableRect =
                selectable.transform as RectTransform;
            if (selectableRect == null)
            {
                continue;
            }

            Vector3 selectableLocal =
                selectableRect.InverseTransformPoint(worldPoint);
            if (!selectableRect.rect.Contains(
                    new Vector2(selectableLocal.x, selectableLocal.y)))
            {
                continue;
            }

            Graphic targetGraphic = selectable.targetGraphic;
            int depth = targetGraphic != null
                ? targetGraphic.depth
                : selectable.transform.GetSiblingIndex();
            if (bestSelectable == null || depth >= bestDepth)
            {
                bestSelectable = selectable;
                bestDepth = depth;
            }
        }

        target = bestSelectable != null
            ? bestSelectable.gameObject
            : null;
        return true;
    }

    private void CreateCursor()
    {
        if (_cursor != null)
        {
            Destroy(_cursor.gameObject);
            _cursor = null;
        }

        if (_targetCanvas == null)
        {
            return;
        }

        GameObject cursorObject = new GameObject(
            "VR Mouse Cursor",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage));
        cursorObject.transform.SetParent(_targetCanvas.transform, false);
        _cursor = cursorObject.GetComponent<RectTransform>();
        _cursor.anchorMin = Vector2.zero;
        _cursor.anchorMax = Vector2.zero;
        _cursor.pivot = new Vector2(0.5f, 0.5f);
        _cursor.sizeDelta = new Vector2(18f, 18f);
        _cursor.SetAsLastSibling();

        RawImage image = cursorObject.GetComponent<RawImage>();
        image.texture = Texture2D.whiteTexture;
        image.color = new Color(0.25f, 0.9f, 1f, 1f);
        image.raycastTarget = false;
        cursorObject.SetActive(false);
    }

    private void UpdateCursor(Vector2 canvasPosition)
    {
        bool visible = _cursor != null &&
                       canvasPosition.x >= 0f &&
                       canvasPosition.y >= 0f;
        SetCursorVisible(visible);
        if (visible)
        {
            _cursor.anchoredPosition = canvasPosition;
            _cursor.SetAsLastSibling();
        }
    }

    private void SetCursorVisible(bool visible)
    {
        if (_cursor != null && _cursor.gameObject.activeSelf != visible)
        {
            _cursor.gameObject.SetActive(visible);
        }
    }

    private void ProcessPointer(GameObject target, Vector2 pointerPosition)
    {
        EnsurePointerData(pointerPosition);
        SetHoverTarget(target);

        if (Input.GetMouseButtonDown(0) && target != null)
        {
            BeginPress(target);
        }

        if (Input.GetMouseButton(0) &&
            _dragTarget != null &&
            _pointerData.delta.sqrMagnitude > 0f)
        {
            if (!_dragging)
            {
                ExecuteEvents.Execute(
                    _dragTarget,
                    _pointerData,
                    ExecuteEvents.beginDragHandler);
                _dragging = true;
                _pointerData.dragging = true;
            }

            ExecuteEvents.Execute(
                _dragTarget,
                _pointerData,
                ExecuteEvents.dragHandler);
        }

        if (Input.GetMouseButtonUp(0))
        {
            EndPress(target);
        }

        LogTarget(pointerPosition, target);
    }

    private void EnsurePointerData(Vector2 pointerPosition)
    {
        if (_pointerData == null)
        {
            _pointerData = new PointerEventData(_eventSystem);
            _previousPointerPosition = pointerPosition;
        }

        _pointerData.position = pointerPosition;
        _pointerData.delta = pointerPosition - _previousPointerPosition;
        _pointerData.button = PointerEventData.InputButton.Left;
        _previousPointerPosition = pointerPosition;
    }

    private void SetHoverTarget(GameObject target)
    {
        if (_hoverTarget == target || _pointerData == null)
        {
            return;
        }

        if (_hoverTarget != null)
        {
            ExecuteEvents.Execute(
                _hoverTarget,
                _pointerData,
                ExecuteEvents.pointerExitHandler);
        }

        _hoverTarget = target;
        _pointerData.pointerEnter = target;
        if (_hoverTarget != null)
        {
            ExecuteEvents.Execute(
                _hoverTarget,
                _pointerData,
                ExecuteEvents.pointerEnterHandler);
        }
    }

    private void BeginPress(GameObject target)
    {
        _pointerData.eligibleForClick = true;
        _pointerData.dragging = false;
        _pointerData.useDragThreshold = true;
        _pointerData.pressPosition = _pointerData.position;

        _pressTarget = ExecuteEvents.ExecuteHierarchy(
            target,
            _pointerData,
            ExecuteEvents.pointerDownHandler);
        if (_pressTarget == null)
        {
            _pressTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                target);
        }

        _pointerData.pointerPress = _pressTarget;
        _pointerData.rawPointerPress = target;
        _dragTarget = ExecuteEvents.GetEventHandler<IDragHandler>(target);
        _pointerData.pointerDrag = _dragTarget;
        _dragging = false;
        if (_dragTarget != null)
        {
            ExecuteEvents.Execute(
                _dragTarget,
                _pointerData,
                ExecuteEvents.initializePotentialDrag);
        }
    }

    private void EndPress(GameObject target)
    {
        if (_pressTarget != null)
        {
            ExecuteEvents.Execute(
                _pressTarget,
                _pointerData,
                ExecuteEvents.pointerUpHandler);

            GameObject clickTarget = target != null
                ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(target)
                : null;
            if (_pointerData.eligibleForClick &&
                clickTarget == _pressTarget)
            {
                ExecuteEvents.Execute(
                    _pressTarget,
                    _pointerData,
                    ExecuteEvents.pointerClickHandler);
            }
        }

        if (_dragging && _dragTarget != null)
        {
            ExecuteEvents.Execute(
                _dragTarget,
                _pointerData,
                ExecuteEvents.endDragHandler);
        }

        _pressTarget = null;
        _dragTarget = null;
        _dragging = false;
        if (_pointerData != null)
        {
            _pointerData.eligibleForClick = false;
            _pointerData.dragging = false;
            _pointerData.pointerPress = null;
            _pointerData.rawPointerPress = null;
            _pointerData.pointerDrag = null;
        }
    }

    private bool IsTargetPanelVisible()
    {
        return _targetCanvas != null &&
               _targetCanvas.gameObject.activeInHierarchy;
    }

    private void UpdateMouseInputOwnership()
    {
        UpdateMouseInputOwnership(IsTargetPanelVisible());
    }

    private void UpdateMouseInputOwnership(bool manualMouseActive)
    {
        if (manualMouseActive && !_manualInputWasActive)
        {
            ResetSelectablePointerStates();
        }

        _manualInputWasActive = manualMouseActive;
        if (_xrInputModule != null)
        {
            _xrInputModule.enableMouseInput = !manualMouseActive;
            _xrInputModule.enableXRInput = !manualMouseActive;
        }
    }

    private void ResetSelectablePointerStates()
    {
        if (_eventSystem == null)
        {
            return;
        }

        PointerEventData resetData = new PointerEventData(_eventSystem);
        for (int i = 0; i < _selectables.Length; i++)
        {
            Selectable selectable = _selectables[i];
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
        }

        _eventSystem.SetSelectedGameObject(null);
    }

    private void ClearPointerState()
    {
        if (_pointerData != null && _hoverTarget != null)
        {
            ExecuteEvents.Execute(
                _hoverTarget,
                _pointerData,
                ExecuteEvents.pointerExitHandler);
        }

        if (_pointerData != null && _pressTarget != null)
        {
            ExecuteEvents.Execute(
                _pressTarget,
                _pointerData,
                ExecuteEvents.pointerUpHandler);
        }

        _hoverTarget = null;
        _pressTarget = null;
        _dragTarget = null;
        _dragging = false;
        _pointerData = null;
    }

    private void LogTarget(Vector2 pointerPosition, GameObject target)
    {
        if (_hasDiagnosticTarget && _lastDiagnosticTarget == target)
        {
            return;
        }

        _hasDiagnosticTarget = true;
        _lastDiagnosticTarget = target;
        Debug.Log(
            $"[XR Station Mouse Target] mapped={pointerPosition:F1}, " +
            $"selectable={(target != null ? target.name : "<none>")}");
    }

    private void OnDisable()
    {
        SetCursorVisible(false);
        ClearPointerState();
        _manualInputWasActive = false;
        if (_xrInputModule != null)
        {
            _xrInputModule.enableMouseInput = true;
            _xrInputModule.enableXRInput = true;
        }
    }
}
