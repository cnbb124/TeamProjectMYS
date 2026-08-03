using System;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// 메인 메뉴에서는 전역 UI Ray, 플레이 중에는 콕핏 Direct Interactor,
/// ESC 메뉴에서는 콕핏 UI Ray만 활성화한다.
/// </summary>
[DefaultExecutionOrder(-500)]
public sealed class XRInteractionModeCoordinator : MonoBehaviour
{
    private const string MenuRigResourceName = "XRMenuControllers";
    private const string HangarSceneName = "BASE_HANGAR";
    private const float RigScanInterval = 0.25f;

    private static XRInteractionModeCoordinator _instance;
    private readonly InputAction _menuAction = new InputAction(
        "TogglePauseMenu",
        InputActionType.Button,
        "<XRController>{LeftHand}/menuButton");

    private GameObject _menuRig;
    private GameObject _localCockpitXrOrigin;
    private float _nextRigScanTime;
    private bool _lastPauseMenuOpen;
    private bool _lastXrRunning;
    private bool _lastCockpitScene;
    private bool _wasBattleXrActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (_instance != null) return;
        GameObject coordinator = new GameObject(nameof(XRInteractionModeCoordinator));
        _instance = coordinator.AddComponent<XRInteractionModeCoordinator>();
        DontDestroyOnLoad(coordinator);
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

    private void OnEnable()
    {
        _menuAction.performed += OnMenuPerformed;
        _menuAction.Enable();
    }

    private void OnDisable()
    {
        _menuAction.performed -= OnMenuPerformed;
        _menuAction.Disable();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        XRRuntimeManager.Started -= OnXrStarted;
    }

    private void Update()
    {
        bool xrRunning = XRRuntimeManager.IsRunning;
        bool cockpitScene = UsesCockpitView();
        if (xrRunning != _lastXrRunning ||
            cockpitScene != _lastCockpitScene)
        {
            _lastXrRunning = xrRunning;
            _lastCockpitScene = cockpitScene;
            ApplyInteractionMode();
        }

        if (!xrRunning)
        {
            return;
        }

        if (Time.unscaledTime >= _nextRigScanTime)
        {
            _nextRigScanTime = Time.unscaledTime + RigScanInterval;
            RefreshLocalCockpitRig();
            HideCockpitControllerModels();

            // Multiplayer players and their cockpit camera can be created after
            // the scene-loaded callback. Re-evaluate the output until a usable
            // local cockpit camera is actually ready.
            if (UsesCockpitView())
            {
                ApplyInteractionMode();
            }
        }

        bool pauseMenuOpen = PauseMenuUI.IsOpen;
        if (pauseMenuOpen != _lastPauseMenuOpen)
        {
            _lastPauseMenuOpen = pauseMenuOpen;
            ApplyInteractionMode();
        }

        AlignMenuRigToActiveXrCamera();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // A persisted player can still own the output camera from the
        // previous scene. Disable it before the new scene camera renders.
        SetActiveSafely(_localCockpitXrOrigin, false);
        _localCockpitXrOrigin = null;
        _wasBattleXrActive = false;
        _nextRigScanTime = 0f;
        _lastPauseMenuOpen = false;
        EnsureMenuRig();
        ApplyInteractionMode();
    }

    private void OnXrStarted()
    {
        EnsureMenuRig();
        RefreshLocalCockpitRig();
        ApplyInteractionMode();
    }

    private void OnMenuPerformed(InputAction.CallbackContext context)
    {
        PauseMenuUI.ToggleFromExternalInput();
    }

    private void EnsureMenuRig()
    {
        if (_menuRig != null || !XRRuntimeManager.IsRunning) return;

        GameObject prefab = Resources.Load<GameObject>(MenuRigResourceName);
        if (prefab == null)
        {
            Debug.LogError(
                $"[XRInteractionModeCoordinator] Resources/{MenuRigResourceName} 프리팹이 없습니다.");
            return;
        }

        _menuRig = Instantiate(prefab);
        _menuRig.name = MenuRigResourceName;
        DontDestroyOnLoad(_menuRig);
    }

    private void RefreshLocalCockpitRig()
    {
        GameObject previous = _localCockpitXrOrigin;
        _localCockpitXrOrigin = null;

        LocalPlayerGuard[] guards = FindObjectsByType<LocalPlayerGuard>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < guards.Length; i++)
        {
            LocalPlayerGuard guard = guards[i];
            PhotonView owner = guard.GetComponentInParent<PhotonView>();
            bool isLocal = owner == null || !PhotonNetwork.InRoom || owner.IsMine;
            if (!isLocal) continue;

            Transform xrOrigin = FindDeepChild(guard.transform, "XR Origin");
            if (xrOrigin != null)
            {
                _localCockpitXrOrigin = xrOrigin.gameObject;
                break;
            }
        }

        if (previous != _localCockpitXrOrigin)
        {
            _wasBattleXrActive = false;
            ApplyInteractionMode();
        }
    }

    private void ApplyInteractionMode()
    {
        bool xrRunning = XRRuntimeManager.IsRunning;
        bool battleScene = IsBattleScene();
        bool wantsCockpit = xrRunning &&
                            UsesCockpitView() &&
                            _localCockpitXrOrigin != null;

        SetActiveSafely(_menuRig, xrRunning);
        SetActiveSafely(_localCockpitXrOrigin, wantsCockpit);

        if (!wantsCockpit)
        {
            SetCameraOutputActive(_menuRig, xrRunning);
            if (_menuRig != null)
            {
                SetControllerRootsActive(_menuRig, xrRunning);
                SetInteractorMode(
                    _menuRig,
                    useDirect: false,
                    useRay: xrRunning);
            }

            _wasBattleXrActive = false;
            return;
        }

        CockpitViewSwitcher switcher =
            _localCockpitXrOrigin.GetComponentInParent<
                CockpitViewSwitcher>(true);
        if (switcher != null &&
            (!_wasBattleXrActive || !switcher.IsCockpitView))
        {
            switcher.enabled = true;
            switcher.ActivateCockpitView();
        }

        // Do not turn off the persistent menu camera until the asynchronously
        // spawned local cockpit has produced a live camera. This avoids the
        // multiplayer "No Display" frame/state.
        bool cockpitOutputReady = HasActiveOutputCamera(_localCockpitXrOrigin);
        SetCameraOutputActive(_menuRig, xrRunning && !cockpitOutputReady);

        if (_menuRig != null)
        {
            SetInteractorMode(_menuRig, useDirect: false, useRay: false);
            SetControllerRootsActive(_menuRig, false);
        }

        bool menuOpen = PauseMenuUI.IsOpen;
        SetInteractorMode(
            _localCockpitXrOrigin,
            useDirect: battleScene && !menuOpen,
            useRay: battleScene && menuOpen);
        HideCockpitControllerModels();

        _wasBattleXrActive = cockpitOutputReady;
    }

    private static bool HasActiveOutputCamera(GameObject rig)
    {
        if (rig == null || !rig.activeInHierarchy)
        {
            return false;
        }

        Camera[] cameras = rig.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera.isActiveAndEnabled && camera.targetTexture == null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBattleScene()
    {
        return GameManager.Instance != null &&
               GameManager.Instance.IsBattleScene;
    }

    private static bool UsesCockpitView()
    {
        return IsBattleScene() ||
               SceneManager.GetActiveScene().name.Equals(
                   HangarSceneName,
                   StringComparison.OrdinalIgnoreCase);
    }

    private void AlignMenuRigToActiveXrCamera()
    {
        if (_menuRig == null || !_menuRig.activeSelf || Camera.main == null) return;

        Transform cameraParent = Camera.main.transform.parent;
        if (cameraParent != null)
        {
            _menuRig.transform.SetPositionAndRotation(
                cameraParent.position,
                cameraParent.rotation);
        }
    }

    private static void SetInteractorMode(
        GameObject rig,
        bool useDirect,
        bool useRay)
    {
        XRDirectInteractor[] directInteractors =
            rig.GetComponentsInChildren<XRDirectInteractor>(true);
        for (int i = 0; i < directInteractors.Length; i++)
            directInteractors[i].gameObject.SetActive(useDirect);

        XRPokeInteractor[] pokeInteractors =
            rig.GetComponentsInChildren<XRPokeInteractor>(true);
        for (int i = 0; i < pokeInteractors.Length; i++)
            pokeInteractors[i].gameObject.SetActive(useDirect);

        XRRayInteractor[] rayInteractors =
            rig.GetComponentsInChildren<XRRayInteractor>(true);
        for (int i = 0; i < rayInteractors.Length; i++)
        {
            bool isUiRay = rayInteractors[i].name == "Ray Interactor";
            rayInteractors[i].gameObject.SetActive(useRay && isUiRay);
        }
    }

    private static void SetActiveSafely(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }

    private static void SetCameraOutputActive(GameObject rig, bool active)
    {
        if (rig == null)
        {
            return;
        }

        Camera[] cameras = rig.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            cameras[i].enabled = active;
        }

        AudioListener[] listeners =
            rig.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            listeners[i].enabled = active;
        }
    }

    private static void SetControllerRootsActive(GameObject rig, bool active)
    {
        Transform cameraOffset = FindDeepChild(rig.transform, "Camera Offset");
        if (cameraOffset == null) return;

        foreach (Transform child in cameraOffset)
        {
            if (child.name == "Left Controller" || child.name == "Right Controller")
            {
                child.gameObject.SetActive(active);
            }
        }
    }

    private void HideCockpitControllerModels()
    {
        if (_localCockpitXrOrigin == null)
        {
            return;
        }

        XRBaseController[] controllers =
            _localCockpitXrOrigin.GetComponentsInChildren<XRBaseController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            XRBaseController controller = controllers[i];
            controller.modelPrefab = null;
            if (controller.model != null)
            {
                controller.model.gameObject.SetActive(false);
            }

            Renderer[] renderers =
                controller.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Renderer controllerRenderer = renderers[rendererIndex];
                XRBaseInteractor owningInteractor =
                    controllerRenderer.GetComponentInParent<XRBaseInteractor>(true);
                if (owningInteractor == null)
                {
                    controllerRenderer.enabled = false;
                }
            }
        }

        Transform[] descendants =
            _localCockpitXrOrigin.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform descendant = descendants[i];
            if (descendant.name.StartsWith(
                    "XR Controller Left",
                    StringComparison.OrdinalIgnoreCase) ||
                descendant.name.StartsWith(
                    "XR Controller Right",
                    StringComparison.OrdinalIgnoreCase))
            {
                descendant.gameObject.SetActive(false);
            }
        }
    }

    private static Transform FindDeepChild(Transform parent, string targetName)
    {
        if (parent.name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindDeepChild(child, targetName);
            if (result != null) return result;
        }

        return null;
    }
}
