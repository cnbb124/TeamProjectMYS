using UnityEngine;

/// <summary>
/// Displays decorative pilot hands on the animated cockpit controls.
/// The hands are not tracked and do not drive gameplay input.
/// </summary>
public sealed class CockpitPilotHands : MonoBehaviour
{
    [Header("Hand prefabs")]
    [SerializeField] private GameObject _leftHandPrefab;
    [SerializeField] private GameObject _rightHandPrefab;

    [Header("Control attachment points")]
    [SerializeField] private Transform _leftHandParent;
    [SerializeField] private Transform _rightHandParent;

    [Header("Fine tuning")]
    [SerializeField] private Vector3 _leftPositionOffset;
    [SerializeField] private Vector3 _leftRotationOffset;
    [SerializeField] private Vector3 _leftScale = new Vector3(1f, -1f, 1f);
    [SerializeField] private Vector3 _rightPositionOffset;
    [SerializeField] private Vector3 _rightRotationOffset;
    [SerializeField] private Vector3 _rightScale = Vector3.one;
    [SerializeField] private float _scaleMultiplier = 1f;

    private LocalPlayerGuard _localPlayerGuard;
    private bool _waitingForXr;

    private void Awake()
    {
        _localPlayerGuard = GetComponent<LocalPlayerGuard>();
        CreateHands();

        bool isLocalPlayer =
            _localPlayerGuard == null || _localPlayerGuard.IsLocalPlayer;
        if (!isLocalPlayer)
        {
            SetHandsVisible(false);
            enabled = false;
            return;
        }

        if (XRRuntimeManager.IsRunning)
        {
            SetHandsVisible(true);
            ApplyGripPose();
            return;
        }

        SetHandsVisible(false);
        _waitingForXr = true;
        XRRuntimeManager.Started += OnXrStarted;
    }

    private void OnDestroy()
    {
        if (_waitingForXr)
        {
            XRRuntimeManager.Started -= OnXrStarted;
        }
    }

    private void OnXrStarted()
    {
        XRRuntimeManager.Started -= OnXrStarted;
        _waitingForXr = false;
        SetHandsVisible(true);
        ApplyGripPose();
    }

    private void CreateHands()
    {
        CreateHand(
            _leftHandPrefab,
            _leftHandParent,
            "LeftPilotHand",
            _leftPositionOffset,
            _leftRotationOffset,
            _leftScale);
        CreateHand(
            _rightHandPrefab,
            _rightHandParent,
            "RightPilotHand",
            _rightPositionOffset,
            _rightRotationOffset,
            _rightScale);
    }

    private void CreateHand(
        GameObject prefab,
        Transform parent,
        string instanceName,
        Vector3 positionOffset,
        Vector3 rotationOffset,
        Vector3 handedScale)
    {
        if (prefab == null || parent == null)
        {
            Debug.LogWarning(
                $"[CockpitPilotHands] {instanceName} prefab or parent is missing.",
                this);
            return;
        }

        // Some imported FBX roots can throw InvalidCastException when the
        // GameObject overload is instantiated with a parent. Cloning the
        // root Transform preserves the complete model hierarchy without
        // relying on that problematic native GameObject cast.
        Transform handTransform = Instantiate(prefab.transform);
        handTransform.SetParent(parent, false);
        GameObject instance = handTransform.gameObject;
        instance.name = instanceName;
        handTransform.localPosition += positionOffset;
        handTransform.localRotation *= Quaternion.Euler(rotationOffset);
        handTransform.localScale = Vector3.Scale(
            handTransform.localScale,
            handedScale * Mathf.Max(0.01f, _scaleMultiplier));

        Debug.Log(
            $"[CockpitPilotHands] Created {instanceName} under {parent.name}.",
            instance);
    }

    private void SetHandsVisible(bool visible)
    {
        SetChildHandsVisible(_leftHandParent, visible);
        SetChildHandsVisible(_rightHandParent, visible);
    }

    private static void SetChildHandsVisible(Transform parent, bool visible)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == "LeftPilotHand" || child.name == "RightPilotHand")
            {
                child.gameObject.SetActive(visible);
            }
        }
    }

    private void ApplyGripPose()
    {
        SetGripOnChildren(_leftHandParent);
        SetGripOnChildren(_rightHandParent);
    }

    private static void SetGripOnChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        Animator[] animators = parent.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (HasFloatParameter(animator, "Grip"))
            {
                animator.SetFloat("Grip", 1f);
            }
        }
    }

    private static bool HasFloatParameter(Animator animator, string parameterName)
    {
        if (animator.runtimeAnimatorController == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Float &&
                parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }
}
