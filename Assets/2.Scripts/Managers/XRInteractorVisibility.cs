using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// VR이 아닐 때만 컨트롤러 인터랙터(레이저 선 포함)를 꺼둔다.
// VR일 때는 아무것도 건드리지 않음 — 그쪽은 XRInteractionModeCoordinator가 담당함.
// cockpit 프리팹 루트에 부착.
public class XRInteractorVisibility : MonoBehaviour
{
    private XRBaseInteractor[] _interactors;
    // 내가 끈 경우에만 true. VR이 뒤늦게 켜졌을 때 내가 끈 것만 되돌리기 위함.
    private bool _disabledByThis;

    private void Awake()
    {
        _interactors = GetComponentsInChildren<XRBaseInteractor>(true);
    }

    private void OnEnable()
    {
        XRRuntimeManager.Started -= OnXRStarted;
        XRRuntimeManager.Started += OnXRStarted;

        if (!XRRuntimeManager.IsRunning)
        {
            SetInteractorsActive(false);
            _disabledByThis = true;
        }
    }

    private void OnDisable()
    {
        XRRuntimeManager.Started -= OnXRStarted;
    }

    // VR이 뒤늦게 켜지면 내가 꺼둔 걸 원래대로 돌려놓고 손을 뗀다.
    private void OnXRStarted()
    {
        if (!_disabledByThis)
        {
            return;
        }
        SetInteractorsActive(true);
        _disabledByThis = false;
    }

    private void SetInteractorsActive(bool active)
    {
        if (_interactors == null)
        {
            return;
        }
        for (int i = 0; i < _interactors.Length; i++)
        {
            if (_interactors[i] != null)
            {
                _interactors[i].gameObject.SetActive(active);
            }
        }
    }
}
