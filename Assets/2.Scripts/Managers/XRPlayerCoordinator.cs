using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[DefaultExecutionOrder(-20000)]
public sealed class XRPlayerCoordinator : MonoBehaviour
{
    private const float ScanInterval = 0.5f;

    private static XRPlayerCoordinator _instance;
    private readonly HashSet<int> _processedSwitchers = new HashSet<int>();
    private readonly HashSet<int> _liveSwitcherIds = new HashSet<int>();
    private readonly List<int> _staleSwitcherIds = new List<int>();
    private float _nextScanTime;
    private bool _legacyScannersDisabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject coordinatorObject = new GameObject(nameof(XRPlayerCoordinator));
        _instance = coordinatorObject.AddComponent<XRPlayerCoordinator>();
        DontDestroyOnLoad(coordinatorObject);
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

    private void Update()
    {
        if (!_legacyScannersDisabled)
        {
            DisableLegacyScanners();
            _legacyScannersDisabled = true;
        }

        if (Time.unscaledTime < _nextScanTime)
        {
            return;
        }

        _nextScanTime = Time.unscaledTime + ScanInterval;
        ProcessNewPlayerRigs();
    }

    private static void DisableLegacyScanners()
    {
        RemoteXRGuard remoteGuard = FindObjectOfType<RemoteXRGuard>();
        if (remoteGuard != null)
        {
            remoteGuard.enabled = false;
        }

        LocalXRViewActivator viewActivator =
            FindObjectOfType<LocalXRViewActivator>();
        if (viewActivator != null)
        {
            viewActivator.enabled = false;
        }
    }

    private void ProcessNewPlayerRigs()
    {
        CockpitViewSwitcher[] switchers =
            FindObjectsByType<CockpitViewSwitcher>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        _liveSwitcherIds.Clear();
        for (int i = 0; i < switchers.Length; i++)
        {
            CockpitViewSwitcher switcher = switchers[i];
            int instanceId = switcher.GetInstanceID();
            _liveSwitcherIds.Add(instanceId);
            if (_processedSwitchers.Contains(instanceId))
            {
                continue;
            }

            // 권장 구조에서는 프리팹에 붙은 LocalPlayerGuard가 소유권을 직접
            // 처리한다. 이 전역 스캐너는 아직 마이그레이션되지 않은 프리팹만
            // 위한 호환 경로로 남긴다.
            if (switcher.GetComponent<LocalPlayerGuard>() != null)
            {
                _processedSwitchers.Add(instanceId);
                continue;
            }

            PhotonView owner = switcher.GetComponentInParent<PhotonView>();
            if (owner == null || !PhotonNetwork.InRoom)
            {
                continue;
            }

            if (owner.IsMine)
            {
                if (!XRRuntimeManager.IsRunning)
                {
                    continue;
                }

                switcher.enabled = true;
                switcher.SetView(true);
            }
            else
            {
                DisableRemoteRig(switcher);
            }

            _processedSwitchers.Add(instanceId);
        }

        _staleSwitcherIds.Clear();
        foreach (int processedId in _processedSwitchers)
        {
            if (!_liveSwitcherIds.Contains(processedId))
            {
                _staleSwitcherIds.Add(processedId);
            }
        }

        for (int i = 0; i < _staleSwitcherIds.Count; i++)
        {
            _processedSwitchers.Remove(_staleSwitcherIds[i]);
        }
    }

    private static void DisableRemoteRig(CockpitViewSwitcher switcher)
    {
        switcher.enabled = false;

        Camera[] cameras = switcher.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            cameras[i].enabled = false;
            cameras[i].tag = "Untagged";
        }

        AudioListener[] listeners =
            switcher.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            listeners[i].enabled = false;
        }
    }
}
