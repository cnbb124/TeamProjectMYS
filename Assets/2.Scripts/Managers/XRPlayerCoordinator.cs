using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[DefaultExecutionOrder(-20000)]
public sealed class XRPlayerCoordinator : MonoBehaviour
{
    private const float ScanInterval = 0.5f;

    private static XRPlayerCoordinator _instance;
    private readonly HashSet<int> _processedSwitchers = new HashSet<int>();
    private float _nextScanTime;

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
        DisableLegacyScanners();

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
            FindObjectsOfType<CockpitViewSwitcher>(true);

        for (int i = 0; i < switchers.Length; i++)
        {
            CockpitViewSwitcher switcher = switchers[i];
            int instanceId = switcher.GetInstanceID();
            if (_processedSwitchers.Contains(instanceId))
            {
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
