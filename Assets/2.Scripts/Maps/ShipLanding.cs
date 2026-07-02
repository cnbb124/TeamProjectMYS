using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipLanding : MonoBehaviour
{
    [Header("플레이어")]
    [SerializeField] private Transform target;

    [Header("착지")]
    [SerializeField] private Transform landedPose;

    [Header("착륙 진입 / 이륙 종착 겸용")]
    [SerializeField] private Transform offscreenPoint;

    [Header("이륙 시 날아갈 거리 (착지 자세의 정면 방향 기준)")]
    [SerializeField] private float takeoffDistance = 60f;

    [Header("타이밍")]
    [SerializeField] private float landingDuration = 1.5f;
    [SerializeField] private float takeoffDuration = 1.0f;

    [Header("이륙 (임시)")]
    [SerializeField] private KeyCode takeoffKey = KeyCode.F12;

    [Header("착륙 - 위치 커브")]
    [SerializeField]
    private AnimationCurve landingPositionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("착륙 - 회전 커브 (도착 근처에서만 회전)")]
    [SerializeField]
    private AnimationCurve landingRotationCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.6f, 0f),
        new Keyframe(1f, 1f)
    );

    [Header("이륙 - 위치 커브 (회전은 고정)")]
    [SerializeField]
    private AnimationCurve takeoffCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.2f),
        new Keyframe(0.3f, 0.1f),
        new Keyframe(1f, 1f, 3f, 0f)
    );

    [Header("착지 - 도착 후 살짝 가라앉는 정도 (음수면 더 내려감)")]
    [SerializeField] private float settleOffsetY = -0.3f;
    [SerializeField] private float descendDuration = 0.6f;
    [SerializeField]
    private AnimationCurve descendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Coroutine currentRoutine;
    private enum State { Idle, Landing, Landed, TakingOff, Gone }
    private State currentState = State.Idle;

    private void Start()
    {
        if (target == null) target = transform;
        PlayLanding();
    }

    private void Update()
    {
        if (currentState == State.Landed && Input.GetKeyDown(takeoffKey))
        {
            PlayTakeoff();
        }
    }

    public void PlayLanding()
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentState = State.Landing;
        currentRoutine = StartCoroutine(LandingSequence());
    }

    // Phase 1: offscreenPoint → landedPose (위치 X,Y,Z + 회전 전부 도착)
    // Phase 2: 도착 후 Y만 살짝 추가로 가라앉음 (settleOffsetY만큼, 살짝 안착하는 느낌)
    private IEnumerator LandingSequence()
    {
        yield return MoveAlong(
            offscreenPoint.position, landedPose.position,
            offscreenPoint.rotation, landedPose.rotation,
            landingDuration, landingPositionCurve, landingRotationCurve);

        // Phase 2: 착지 Y에서 settleOffsetY만큼 살짝 더 내려앉기
        yield return MoveYOnly(
            landedPose.position.y, landedPose.position.y + settleOffsetY,
            descendDuration, descendCurve);

        currentState = State.Landed;
    }

    public void PlayTakeoff()
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentState = State.TakingOff;
        currentRoutine = StartCoroutine(TakeoffSequence());
    }

    // Phase 1: 가라앉았던 Y를 landedPose 높이로 되돌림 (착륙 settle의 역재생)
    // Phase 2: 그 자리에서 정면 방향으로 이륙 (회전 고정)
    private IEnumerator TakeoffSequence()
    {
        yield return MoveYOnly(
            landedPose.position.y + settleOffsetY, landedPose.position.y,
            descendDuration, descendCurve);

        Vector3 exitPoint = landedPose.position + landedPose.forward * takeoffDistance;

        yield return MoveForwardOnly(
            landedPose.position, exitPoint,
            landedPose.rotation,
            takeoffDuration, takeoffCurve);

        currentState = State.Gone;
    }

    // 착륙용: 위치/회전을 각각 다른 커브로 보간
    private IEnumerator MoveAlong(Vector3 fromPos, Vector3 toPos,
        Quaternion fromRot, Quaternion toRot,
        float duration, AnimationCurve posCurve, AnimationCurve rotCurve,
        System.Action onComplete = null)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / duration);

            float posEased = posCurve.Evaluate(normalized);
            float rotEased = rotCurve.Evaluate(normalized);

            target.position = Vector3.LerpUnclamped(fromPos, toPos, posEased);
            target.rotation = Quaternion.Slerp(fromRot, toRot, rotEased);

            yield return null;
        }

        target.position = toPos;
        target.rotation = toRot;
        onComplete?.Invoke();
    }

    // 착지 하강용: Y값만 보간 (X,Z, 회전은 그대로 유지)
    private IEnumerator MoveYOnly(float fromY, float toY,
        float duration, AnimationCurve curve,
        System.Action onComplete = null)
    {
        float t = 0f;
        Vector3 pos = target.position;
        while (t < duration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / duration);
            float eased = curve.Evaluate(normalized);

            pos.y = Mathf.LerpUnclamped(fromY, toY, eased);
            target.position = pos;

            yield return null;
        }

        pos.y = toY;
        target.position = pos;
        onComplete?.Invoke();
    }

    // 이륙용: 회전은 고정, 위치만 이동
    private IEnumerator MoveForwardOnly(Vector3 fromPos, Vector3 toPos,
        Quaternion fixedRot,
        float duration, AnimationCurve curve,
        System.Action onComplete = null)
    {
        target.rotation = fixedRot;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / duration);
            float eased = curve.Evaluate(normalized);

            target.position = Vector3.LerpUnclamped(fromPos, toPos, eased);

            yield return null;
        }

        target.position = toPos;
        onComplete?.Invoke();
    }
}
