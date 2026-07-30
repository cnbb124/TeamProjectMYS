using Photon.Pun;
using UnityEngine;

/// <summary>
/// 플레이어 인스턴스의 소유권에 따라 로컬 XR 기능을 켜고 원격 카메라를 차단합니다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class LocalPlayerGuard : MonoBehaviour
{
    [SerializeField] private GameObject _xrOrigin;
    [SerializeField] private CockpitViewSwitcher _viewController;

    public bool IsLocalPlayer { get; private set; }

    private bool _waitingForXr;

    private void Awake()
    {
        PhotonView owner = GetComponentInParent<PhotonView>();
        IsLocalPlayer = owner == null || !PhotonNetwork.InRoom || owner.IsMine;

        if (!IsLocalPlayer)
        {
            DisableRemoteView();
            return;
        }

        if (_viewController != null)
        {
            _viewController.enabled = true;
        }

        if (XRRuntimeManager.IsRunning)
        {
            EnableLocalVr();
            return;
        }

        _waitingForXr = true;
        XRRuntimeManager.Started += OnXrStarted;
    }

    private void OnDestroy()
    {
        if (_waitingForXr)
        {
            XRRuntimeManager.Started -= OnXrStarted;
            _waitingForXr = false;
        }
    }

    private void OnXrStarted()
    {
        XRRuntimeManager.Started -= OnXrStarted;
        _waitingForXr = false;
        EnableLocalVr();
    }

    private void EnableLocalVr()
    {
        if (_xrOrigin != null)
        {
            _xrOrigin.SetActive(true);
        }

        if (_viewController != null)
        {
            _viewController.SetView(true);
        }
    }

    private void DisableRemoteView()
    {
        if (_viewController != null)
        {
            _viewController.enabled = false;
        }

        if (_xrOrigin != null)
        {
            _xrOrigin.SetActive(false);
        }

        Camera[] cameras = GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            cameras[i].enabled = false;
            cameras[i].tag = "Untagged";
        }

        AudioListener[] listeners = GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            listeners[i].enabled = false;
        }
    }
}
