using Photon.Pun;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class RemoteXRGuard : MonoBehaviour
{
    private static RemoteXRGuard _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject guardObject = new GameObject(nameof(RemoteXRGuard));
        _instance = guardObject.AddComponent<RemoteXRGuard>();
        DontDestroyOnLoad(guardObject);
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
        CockpitViewSwitcher[] switchers =
            FindObjectsOfType<CockpitViewSwitcher>(true);

        for (int i = 0; i < switchers.Length; i++)
        {
            DisableIfRemote(switchers[i]);
        }
    }

    private static void DisableIfRemote(CockpitViewSwitcher switcher)
    {
        PhotonView owner = switcher.GetComponentInParent<PhotonView>();
        if (owner == null || !PhotonNetwork.InRoom || owner.IsMine)
        {
            return;
        }

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
