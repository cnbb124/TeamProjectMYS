using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// VR이 안 켜져 있으면 컨트롤러 인터랙터(레이저 선 포함)를 꺼둔다.
// 비VR 플레이에서 함선에 빨간 선이 뻗어 보이는 걸 막기 위함. cockpit 프리팹 루트에 부착.
public class XRInteractorVisibility : MonoBehaviour
{
    private XRBaseInteractor[] _interactors;

    private void Awake()
    {
        _interactors = GetComponentsInChildren<XRBaseInteractor>(true);
    }

    private void OnEnable()
    {
        XRRuntimeManager.Started -= OnXRStarted;
        XRRuntimeManager.Started += OnXRStarted;
        Apply(XRRuntimeManager.IsRunning);
    }

    private void OnDisable()
    {
        XRRuntimeManager.Started -= OnXRStarted;
    }

    private void OnXRStarted()
    {
        Apply(true);
    }

    private void Apply(bool visible)
    {
        if (_interactors == null)
        {
            return;
        }
        for (int i = 0; i < _interactors.Length; i++)
        {
            if (_interactors[i] != null)
            {
                _interactors[i].gameObject.SetActive(visible);
            }
        }
    }
}
