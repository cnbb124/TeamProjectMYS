using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// XR 컨트롤러로 스로틀을 잡았을 때 손의 이동을 한 축 회전으로 변환한다.
/// XRGrabInteractable처럼 오브젝트를 손에 붙이지 않으므로 콕핏에서 분리되지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CockpitThrottleInteractable : XRBaseInteractable
{
    [SerializeField] private Transform _throttlePivot;
    [SerializeField] private CockpitControlsAnimator _controlsAnimator;
    [SerializeField] private Vector3 _localRotationAxis = Vector3.right;
    [SerializeField] private float _minimumAngle = -25f;
    [SerializeField] private float _maximumAngle = 30f;

    private Quaternion _restRotation;
    private Vector3 _grabDirection;
    private float _grabAngle;
    private IXRSelectInteractor _selectingInteractor;

    protected override void Awake()
    {
        base.Awake();

        if (_throttlePivot == null)
        {
            _throttlePivot = transform.parent;
        }

        if (_controlsAnimator == null)
        {
            _controlsAnimator = GetComponentInParent<CockpitControlsAnimator>();
        }

        if (_throttlePivot != null)
        {
            _restRotation = _throttlePivot.localRotation;
        }
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);

        _selectingInteractor = args.interactorObject;
        _controlsAnimator?.SetThrottleGrabbed(true);

        if (_throttlePivot == null)
        {
            return;
        }

        _grabDirection = GetProjectedLocalDirection(_selectingInteractor);
        _grabAngle = GetCurrentSignedAngle();
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        _controlsAnimator?.SetThrottleGrabbed(false);
        _selectingInteractor = null;
        base.OnSelectExited(args);
    }

    private void Update()
    {
        if (_selectingInteractor == null || _throttlePivot == null)
        {
            return;
        }

        Vector3 currentDirection = GetProjectedLocalDirection(_selectingInteractor);
        if (_grabDirection.sqrMagnitude < 0.0001f || currentDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 axis = GetAxis();
        float delta = Vector3.SignedAngle(_grabDirection, currentDirection, axis);
        float angle = Mathf.Clamp(_grabAngle + delta, _minimumAngle, _maximumAngle);
        _throttlePivot.localRotation = _restRotation * Quaternion.AngleAxis(angle, axis);
    }

    private Vector3 GetProjectedLocalDirection(IXRSelectInteractor interactor)
    {
        Transform attach = interactor.GetAttachTransform(this);
        Vector3 localPosition = _throttlePivot.InverseTransformPoint(attach.position);
        return Vector3.ProjectOnPlane(localPosition, GetAxis()).normalized;
    }

    private float GetCurrentSignedAngle()
    {
        Quaternion relative = Quaternion.Inverse(_restRotation) * _throttlePivot.localRotation;
        relative.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f)
        {
            angle -= 360f;
        }

        return Vector3.Dot(axis, GetAxis()) < 0f ? -angle : angle;
    }

    private Vector3 GetAxis()
    {
        return _localRotationAxis.sqrMagnitude > 0.0001f
            ? _localRotationAxis.normalized
            : Vector3.right;
    }
}
