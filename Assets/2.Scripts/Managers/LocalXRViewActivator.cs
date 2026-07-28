using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[DefaultExecutionOrder(-9000)]
public sealed class LocalXRViewActivator : MonoBehaviour
{
    private static LocalXRViewActivator _instance;
    private readonly HashSet<int> _activatedSwitchers = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject activatorObject = new GameObject(nameof(LocalXRViewActivator));
        _instance = activatorObject.AddComponent<LocalXRViewActivator>();
        DontDestroyOnLoad(activatorObject);
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
        if (!XRRuntimeManager.IsRunning)
        {
            return;
        }

        CockpitViewSwitcher[] switchers =
            FindObjectsOfType<CockpitViewSwitcher>(true);

        for (int i = 0; i < switchers.Length; i++)
        {
            ActivateLocalView(switchers[i]);
        }
    }

    private void ActivateLocalView(CockpitViewSwitcher switcher)
    {
        int instanceId = switcher.GetInstanceID();
        if (_activatedSwitchers.Contains(instanceId))
        {
            return;
        }

        PhotonView owner = switcher.GetComponentInParent<PhotonView>();
        if (owner != null && PhotonNetwork.InRoom && !owner.IsMine)
        {
            return;
        }

        switcher.enabled = true;
        switcher.SetView(true);
        _activatedSwitchers.Add(instanceId);
    }
}
