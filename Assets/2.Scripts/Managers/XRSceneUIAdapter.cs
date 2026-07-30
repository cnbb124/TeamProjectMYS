using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Makes scene-owned screen-space UI visible to both eyes while XR is running.
/// The UI itself remains scene-owned and is attached only to its scene camera.
/// </summary>
[DefaultExecutionOrder(-19000)]
public sealed class XRSceneUIAdapter : MonoBehaviour
{
    private const string CombatHudCanvasName = "HUD";
    private const float UiPlaneDistance = 1.5f;
    private const float ViewMargin = 0.9f;
    private const int RefreshFrameCount = 12;
    private static readonly Vector2 ReferenceResolution =
        new Vector2(1920f, 1080f);

    private static XRSceneUIAdapter _instance;
    private Coroutine _refreshCoroutine;

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

        ConfigurePointerInput(sceneCamera);

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

            StandaloneInputModule module =
                eventSystem.GetComponent<StandaloneInputModule>();
            if (module == null)
            {
                continue;
            }

            // GraphicRaycaster and the Game View mirror both use normal screen
            // coordinates in ScreenSpaceCamera mode. Remapping through the XR
            // camera pixelRect offsets hit positions in single-eye views.
            module.inputOverride = null;
        }
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
