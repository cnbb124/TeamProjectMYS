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

    [Tooltip("조종간을 앞으로 밀 때 기수가 내려가면 기본값. 반대가 편하면 체크.")]
    [SerializeField] private bool _invertStickPitch;

    [Header("조종간 겉모습 방향")]
    [Tooltip("손을 앞으로 미는데 조종간이 뒤로 기울면 체크. 기체 조종에는 영향 없음.")]
    [SerializeField] private bool _invertVisualPitch;

    [Tooltip("손을 오른쪽으로 미는데 조종간이 왼쪽으로 기울면 체크. 기체 조종에는 영향 없음.")]
    [SerializeField] private bool _invertVisualRoll;

    private readonly InputAction _leftGrip = new InputAction(
        "CockpitThrottleGrip", InputActionType.Value,
        "<XRController>{LeftHand}/grip");
    private readonly InputAction _rightGrip = new InputAction(
        "CockpitStickGrip", InputActionType.Button,
        "<XRController>{RightHand}/gripPressed");

    // [2026-08-04 추가] 썸스틱 입력
    // 그립으로 잡는 조종간은 롤·피치만 만든다. 좌우 선회(Yaw)와 평행 이동은
    // 채워지는 곳이 없어서 썸스틱으로 보완한다.
    private readonly InputAction _leftStick = new InputAction(
        "CockpitLeftStick", InputActionType.Value,
        "<XRController>{LeftHand}/thumbstick");
    private readonly InputAction _rightStick = new InputAction(
        "CockpitRightStick", InputActionType.Value,
        "<XRController>{RightHand}/thumbstick");

    // [2026-08-04 추가] 버튼 입력
    // 그립은 조종간·스로틀에 쓰고, 스틱은 선회·이동·락온에 썼다.
    // 남은 트리거와 A/B/X/Y에 전투 조작을 배치한다.
    private readonly InputAction _rightTrigger = new InputAction(
        "CockpitFireBullet", InputActionType.Button,
        "<XRController>{RightHand}/triggerPressed");
    private readonly InputAction _leftTrigger = new InputAction(
        "CockpitFireMissile", InputActionType.Button,
        "<XRController>{LeftHand}/triggerPressed");
    private readonly InputAction _rightPrimary = new InputAction(   // A
        "CockpitDodge", InputActionType.Button,
        "<XRController>{RightHand}/primaryButton");
    private readonly InputAction _rightSecondary = new InputAction( // B
        "CockpitUseSkill", InputActionType.Button,
        "<XRController>{RightHand}/secondaryButton");
    private readonly InputAction _leftPrimary = new InputAction(    // X
        "CockpitMissilePrev", InputActionType.Button,
        "<XRController>{LeftHand}/primaryButton");
    private readonly InputAction _leftSecondary = new InputAction(  // Y
        "CockpitSkillSlot", InputActionType.Button,
        "<XRController>{LeftHand}/secondaryButton");

    [Header("썸스틱")]
    [Tooltip("스틱 중앙 근처의 미세한 흔들림을 무시하는 범위. 0.15 권장.")]
    [SerializeField] private float _stickDeadzone = 0.15f;

    [Tooltip("오른쪽 스틱 상하로 락온 대상을 바꿀 때, 한 번 넘긴 뒤 다시 받기까지의 간격(초).")]
    [SerializeField] private float _lockOnRepeatDelay = 0.35f;

    [Header("부스터 (스로틀 끝까지 밀기)")]
    [Tooltip("스로틀을 이 값 이상 밀면 부스터가 켜진다. 0.9 = 90%")]
    [SerializeField] private float _boostThreshold = 0.9f;

    private float _nextLockOnSwitchTime;

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
        _leftStick.Enable();
        _rightStick.Enable();
        _rightTrigger.Enable();
        _leftTrigger.Enable();
        _rightPrimary.Enable();
        _rightSecondary.Enable();
        _leftPrimary.Enable();
        _leftSecondary.Enable();
    }

    private void OnDisable()
    {
        _rightGrip.performed -= OnStickGripStarted;
        _rightGrip.canceled -= OnStickGripEnded;
        _leftGrip.Disable();
        _rightGrip.Disable();
        _leftStick.Disable();
        _rightStick.Disable();
        _rightTrigger.Disable();
        _leftTrigger.Disable();
        _rightPrimary.Disable();
        _rightSecondary.Disable();
        _leftPrimary.Disable();
        _leftSecondary.Disable();
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
            // 방향 보정은 여기서 하지 않는다. 겉모습은 _invertVisualPitch/Roll,
            // 기체 조종은 _invertStickPitch로 인스펙터에서 뒤집는다.
            // 이 값 자체를 건드리면 두 곳이 같이 바뀌어 원인을 가리기 어렵다.
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
        {
            input.moveInput.z = _throttleValue;

            // [2026-08-04] 부스터는 별도 버튼이 아니라 "스로틀을 끝까지 미는 것"이다.
            // 실제 항공기의 애프터버너와 같은 조작이라 버튼을 하나 덜 쓴다.
            input.isBoosting = _throttleValue >= _boostThreshold;
        }

        if (_stickHeld)
        {
            input.rollInput = _stickValue.x;
            input.lookInput.y = _invertStickPitch ? _stickValue.y : -_stickValue.y;
        }

        ApplyThumbstickInput(input);
        ApplyButtonInput(input);
    }

    /// <summary>
    /// [2026-08-04 추가] 버튼 입력을 InputManager 출력 필드에 채운다.
    ///
    ///   오른쪽 트리거 → 기관포 (누르는 동안)
    ///   왼쪽  트리거 → 미사일 (누른 순간)
    ///   A(오른쪽)    → 회피
    ///   B(오른쪽)    → 스킬 사용
    ///   X(왼쪽)      → 미사일 슬롯 전환
    ///   Y(왼쪽)      → 스킬 슬롯 전환
    ///
    /// 버튼이 모자라서 소모품·인벤토리·지도는 아직 배정하지 못했다.
    /// 나중에 Y를 길게 눌러 여는 퀵 메뉴로 묶는 방향을 검토한다.
    ///
    /// 주의: 기관포만 "누르는 동안"이고 나머지는 "누른 순간"이다.
    /// InputManager의 기존 키보드 처리와 같은 규칙이라 Player 코드는 고칠 필요가 없다.
    /// </summary>
    private void ApplyButtonInput(InputManager input)
    {
        // [2026-08-04 수정] 필드에 직접 쓰면 다음 Update의 ReadKeyboardMouse에 덮여 사라진다.
        // InputManager의 VR 오버레이에 넣어두면 거기서 OR로 합쳐준다(패드와 같은 방식).
        if (_rightTrigger.IsPressed())            InputManager.xrFireBullet = true;
        if (_leftTrigger.WasPressedThisFrame())   InputManager.xrFireMissile = true;
        if (_rightPrimary.WasPressedThisFrame())  InputManager.xrDodge = true;
        if (_rightSecondary.WasPressedThisFrame()) InputManager.xrUseSkill = true;
        if (_leftPrimary.WasPressedThisFrame())   InputManager.xrSwitchMissileNext = true;
        if (_leftSecondary.WasPressedThisFrame()) InputManager.xrSwitchSkillSlot = true;
    }

    /// <summary>
    /// [2026-08-04 추가] 썸스틱 입력을 InputManager 출력 필드에 채운다.
    ///
    /// 그립으로 잡는 조종간은 롤(rollInput)과 피치(lookInput.y)만 만든다.
    /// 좌우 선회(Yaw)와 평행 이동은 채워지는 곳이 없어 썸스틱이 담당한다.
    ///
    ///   왼쪽 스틱  좌우 → 좌우 평행이동 (moveInput.x)
    ///   왼쪽 스틱  상하 → 상하 평행이동 (moveInput.y)
    ///   오른쪽 스틱 좌우 → 좌우 선회 Yaw (lookInput.x)
    ///   오른쪽 스틱 상하 → 락온 대상 전환 (switchLockOnTarget)
    ///
    /// 오른손이 방향, 왼손이 이동을 맡아 그립 조작(오른손=조종간, 왼손=스로틀)과 역할이 같다.
    /// </summary>
    private void ApplyThumbstickInput(InputManager input)
    {
        Vector2 left = ApplyDeadzone(_leftStick.ReadValue<Vector2>());
        Vector2 right = ApplyDeadzone(_rightStick.ReadValue<Vector2>());

        if (left.sqrMagnitude > 0f)
        {
            input.moveInput.x = left.x;
            input.moveInput.y = left.y;
        }

        if (Mathf.Abs(right.x) > 0f)
        {
            input.lookInput.x = right.x;
        }

        // 락온 전환은 "누른 순간"만 필요하므로 스틱을 밀고 있는 동안 반복되지 않게 막는다.
        if (Mathf.Abs(right.y) > 0.5f)
        {
            if (Time.unscaledTime >= _nextLockOnSwitchTime)
            {
                _nextLockOnSwitchTime = Time.unscaledTime + _lockOnRepeatDelay;
                input.switchLockOnTarget = Mathf.Sign(right.y);
            }
        }
        else
        {
            // 중앙으로 돌아오면 다음 입력을 곧바로 받도록 초기화한다.
            _nextLockOnSwitchTime = 0f;
        }
    }

    /// <summary>중앙 근처의 미세한 흔들림을 무시하고, 남은 구간을 0~1로 다시 편다.</summary>
    private Vector2 ApplyDeadzone(Vector2 raw)
    {
        float magnitude = raw.magnitude;
        if (magnitude <= _stickDeadzone)
        {
            return Vector2.zero;
        }

        float scaled = Mathf.InverseLerp(_stickDeadzone, 1f, magnitude);
        return raw / magnitude * Mathf.Clamp01(scaled);
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
            // [2026-08-04] 기울어지는 방향은 조종간 모델의 피벗이 어느 쪽을 보고
            // 서 있느냐에 달려 있어서 코드만으로는 정할 수 없다.
            // 부호를 추측해 고치는 대신 인스펙터에서 뒤집을 수 있게 뺀다.
            float pitch = _invertVisualPitch
                ? _stickValue.y
                : -_stickValue.y;
            float roll = _invertVisualRoll
                ? _stickValue.x
                : -_stickValue.x;

            Quaternion tilt = Quaternion.Euler(
                pitch * _stickMaxPitch,
                0f,
                roll * _stickMaxRoll);
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
