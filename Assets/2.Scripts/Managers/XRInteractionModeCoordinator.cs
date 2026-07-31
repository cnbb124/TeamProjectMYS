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
        if (!XRRuntimeManager.IsRunning)
        {
            SetActiveSafely(_menuRig, false);
            return;
        }

        if (Time.unscaledTime >= _nextRigScanTime)
        {
            _nextRigScanTime = Time.unscaledTime + RigScanInterval;
            RefreshLocalCockpitRig();
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
        _localCockpitXrOrigin = null;
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

        if (previous != _localCockpitXrOrigin) ApplyInteractionMode();
    }

    private void ApplyInteractionMode()
    {
        bool hasCockpit = _localCockpitXrOrigin != null;
        SetActiveSafely(_menuRig, XRRuntimeManager.IsRunning);

        if (!hasCockpit)
        {
            if (_menuRig != null)
            {
                SetControllerRootsActive(_menuRig, true);
                SetInteractorMode(_menuRig, useDirect: false, useRay: true);
            }
            return;
        }

        if (_menuRig != null)
        {
            SetInteractorMode(_menuRig, useDirect: false, useRay: false);
            SetControllerRootsActive(_menuRig, false);
        }

        bool menuOpen = PauseMenuUI.IsOpen;
        SetInteractorMode(
            _localCockpitXrOrigin,
            useDirect: !menuOpen,
            useRay: menuOpen);
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
