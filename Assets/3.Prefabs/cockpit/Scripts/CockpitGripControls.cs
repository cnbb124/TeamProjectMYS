using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

/// <summary>
/// Lets the pilot operate the cockpit without precisely touching small colliders.
/// Left grip controls the throttle and right grip controls the flight stick.
/// Controller tracking poses are never moved; only their movement delta is used.
/// </summary>
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
public sealed class CockpitGripControls : MonoBehaviour
{
    [SerializeField] private Transform _stickPivot;
    [SerializeField] private Transform _throttlePivot;
    [SerializeField] private CockpitControlsAnimator _controlsAnimator;
    [SerializeField] private float _stickTravelMetres = 0.12f;
    [SerializeField] private float _throttleTravelMetres = 0.12f;
    [SerializeField] private float _stickMaxPitch = 20f;
    [SerializeField] private float _stickMaxRoll = 20f;
    [SerializeField] private float _throttleMaxAngle = 25f;
    [SerializeField] private bool _invertThrottle;

    private readonly InputAction _leftGrip = new InputAction(
        "CockpitThrottleGrip", InputActionType.Value,
        "<XRController>{LeftHand}/grip");
    private readonly InputAction _rightGrip = new InputAction(
        "CockpitStickGrip", InputActionType.Button,
        "<XRController>{RightHand}/gripPressed");

    private Transform _leftController;
    private Transform _rightController;
    private Quaternion _stickRestRotation;
    private Quaternion _throttleRestRotation;
    private Vector3 _stickGrabPosition;
    private Vector3 _throttleGrabPosition;
    private Vector2 _stickValue;
    private float _throttleValue;
    private float _throttleGrabValue;
    private bool _stickHeld;
    private bool _throttleHeld;
    private bool _throttleClaimed;
    private bool _throttleToggleArmed = true;
    private float _throttleReleasedSince = -1f;
    private float _nextThrottleToggleTime;

    private void Awake()
    {
        if (_controlsAnimator == null)
            _controlsAnimator = GetComponent<CockpitControlsAnimator>();

        if (_stickPivot == null)
            _stickPivot = FindDeepChild(transform, "CockpitEquipments_Joystick2-Handle");

        if (_throttlePivot == null)
            _throttlePivot = FindDeepChild(transform, "CockpitEquipments_ThrottleControl1-Handle1");

        Transform xrOrigin = FindDeepChild(transform, "XR Origin");
        if (xrOrigin != null)
        {
            _leftController = FindDeepChild(xrOrigin, "Left Controller");
            _rightController = FindDeepChild(xrOrigin, "Right Controller");
        }

        if (_stickPivot != null) _stickRestRotation = _stickPivot.localRotation;
        if (_throttlePivot != null) _throttleRestRotation = _throttlePivot.localRotation;
        _throttleValue = 0f;
        // 그립으로 연결하기 전에는 키보드 W/S가 전후진 입력을 계속 담당한다.
        _throttleClaimed = false;

        if (_leftController == null || _throttlePivot == null)
            Debug.LogWarning("[CockpitGripControls] Left controller or throttle pivot was not found.", this);
    }

    private void OnEnable()
    {
        _rightGrip.performed += OnStickGripStarted;
        _rightGrip.canceled += OnStickGripEnded;
        _leftGrip.Enable();
        _rightGrip.Enable();
    }

    private void OnDisable()
    {
        _rightGrip.performed -= OnStickGripStarted;
        _rightGrip.canceled -= OnStickGripEnded;
        _leftGrip.Disable();
        _rightGrip.Disable();
        SetThrottleHeld(false);
        SetStickHeld(false);
    }

    private void Update()
    {
        if (!XRRuntimeManager.IsRunning || PauseMenuUI.IsOpen) return;

        UpdateThrottleToggle();

        if (_throttleHeld && _leftController != null)
        {
            // 월드 좌표 차이를 사용하면 기체가 이동한 거리까지 손 이동으로 오인한다.
            // 잡은 순간과 현재 손 위치를 모두 콕핏 로컬 좌표로 비교한다.
            Vector3 delta =
                transform.InverseTransformPoint(_leftController.position) -
                _throttleGrabPosition;
            float leverMovement = delta.z;
            if (_invertThrottle) leverMovement = -leverMovement;

            // Keyboard W/S와 동일하게 전진(+1), 중립(0), 후진(-1)을 모두 사용한다.
            _throttleValue = Mathf.Clamp(
                _throttleGrabValue +
                leverMovement / Mathf.Max(0.01f, _throttleTravelMetres),
                -1f, 1f);
        }

        if (_stickHeld && _rightController != null)
        {
            Vector3 delta = transform.InverseTransformPoint(_rightController.position) -
                            _stickGrabPosition;
            float travel = Mathf.Max(0.01f, _stickTravelMetres);
            _stickValue = Vector2.ClampMagnitude(
                new Vector2(delta.x / travel, -delta.z / travel), 1f);
        }

        ApplyControlVisuals();
    }

    private void LateUpdate()
    {
        if (!XRRuntimeManager.IsRunning || InputManager.Instance == null) return;

        InputManager input = InputManager.Instance;
        if (_throttleClaimed)
            input.moveInput.z = _throttleValue;

        if (_stickHeld)
        {
            input.rollInput = _stickValue.x;
            input.lookInput.y = -_stickValue.y;
        }
    }

    private void UpdateThrottleToggle()
    {
        float grip = _leftGrip.ReadValue<float>();

        if (grip <= 0.15f)
        {
            if (_throttleReleasedSince < 0f)
                _throttleReleasedSince = Time.unscaledTime;

            if (Time.unscaledTime - _throttleReleasedSince >= 0.25f)
                _throttleToggleArmed = true;
        }
        else
        {
            _throttleReleasedSince = -1f;
        }

        if (!_throttleToggleArmed ||
            grip < 0.70f ||
            Time.unscaledTime < _nextThrottleToggleTime)
            return;

        _throttleToggleArmed = false;
        _nextThrottleToggleTime = Time.unscaledTime + 0.75f;

        if (_throttleHeld)
        {
            SetThrottleHeld(false);
            _throttleValue = 0f;
            _throttleGrabValue = 0f;
            if (InputManager.Instance != null)
                InputManager.Instance.moveInput.z = 0f;
            _throttleClaimed = false;
            Debug.Log("[CockpitGripControls] Throttle disconnected and returned to neutral.", this);
        }
        else
        {
            BeginThrottleGrip();
        }
    }

    private void BeginThrottleGrip()
    {
        if (_leftController == null) return;

        _throttleClaimed = true;
        _throttleGrabPosition =
            transform.InverseTransformPoint(_leftController.position);
        _throttleGrabValue = _throttleValue;
        SetThrottleHeld(true);
        Debug.Log("[CockpitGripControls] Left grip connected to throttle.", this);
    }

    private void OnStickGripStarted(InputAction.CallbackContext context)
    {
        if (_rightController == null || PauseMenuUI.IsOpen) return;
        _stickGrabPosition = transform.InverseTransformPoint(_rightController.position);
        SetStickHeld(true);
    }

    private void OnStickGripEnded(InputAction.CallbackContext context)
    {
        SetStickHeld(false);
        _stickValue = Vector2.zero;
    }

    private void SetThrottleHeld(bool held)
    {
        _throttleHeld = held;
        _controlsAnimator?.SetThrottleGrabbed(held);
    }

    private void SetStickHeld(bool held)
    {
        _stickHeld = held;
        _controlsAnimator?.SetStickGrabbed(held);
    }

    private void ApplyControlVisuals()
    {
        if (_throttlePivot != null && _throttleClaimed)
        {
            _throttlePivot.localRotation = _throttleRestRotation *
                Quaternion.AngleAxis(_throttleValue * _throttleMaxAngle, Vector3.right);
        }

        if (_stickPivot != null && _stickHeld)
        {
            Quaternion tilt = Quaternion.Euler(
                -_stickValue.y * _stickMaxPitch,
                0f,
                -_stickValue.x * _stickMaxRoll);
            _stickPivot.localRotation = _stickRestRotation * tilt;
        }
    }

    private static Transform FindDeepChild(Transform parent, string targetName)
    {
        if (parent.name == targetName) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindDeepChild(child, targetName);
            if (found != null) return found;
        }
        return null;
    }

    private void Start()
    {
        PhotonView owner = GetComponentInParent<PhotonView>();
        if (owner != null && PhotonNetwork.InRoom && !owner.IsMine)
            enabled = false;
    }
}
