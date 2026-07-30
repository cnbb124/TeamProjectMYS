using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Management;

public sealed class XRRuntimeManager : MonoBehaviour
{
    private static XRRuntimeManager _instance;
    private bool _initializationStarted;

    public static event Action Started;

    public static bool IsRunning
    {
        get
        {
            XRManagerSettings manager = XRGeneralSettings.Instance != null
                ? XRGeneralSettings.Instance.Manager
                : null;
            return manager != null && manager.activeLoader != null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject managerObject = new GameObject(nameof(XRRuntimeManager));
        _instance = managerObject.AddComponent<XRRuntimeManager>();
        DontDestroyOnLoad(managerObject);
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
    }

    private void Start()
    {
        StartCoroutine(InitializeXR());
    }

    private IEnumerator InitializeXR()
    {
        if (_initializationStarted)
        {
            yield break;
        }

        if (IsRunning)
        {
            NotifyStarted();
            yield break;
        }

        _initializationStarted = true;

        XRGeneralSettings settings = XRGeneralSettings.Instance;
        XRManagerSettings manager = settings != null ? settings.Manager : null;
        if (manager == null)
        {
            Debug.LogWarning("[XRRuntimeManager] XR Manager 설정을 찾지 못했습니다.");
            yield break;
        }

        yield return manager.InitializeLoader();

        if (manager.activeLoader == null)
        {
            Debug.Log("[XRRuntimeManager] 연결된 XR 장치가 없어 일반 화면 모드로 실행합니다.");
            yield break;
        }

        manager.StartSubsystems();
        Debug.Log($"[XRRuntimeManager] XR 실행 완료: {manager.activeLoader.name}");
        NotifyStarted();
    }

    private static void NotifyStarted()
    {
        Started?.Invoke();
    }

    private void OnApplicationQuit()
    {
        XRGeneralSettings settings = XRGeneralSettings.Instance;
        XRManagerSettings manager = settings != null ? settings.Manager : null;
        if (manager == null || manager.activeLoader == null)
        {
            return;
        }

        manager.StopSubsystems();
        manager.DeinitializeLoader();
    }
}
