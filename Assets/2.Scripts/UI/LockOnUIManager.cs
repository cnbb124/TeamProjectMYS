using System.Collections.Generic;
using UnityEngine;

// 락온 마커도 전투씬 전용임 — HUD 프리팹 안에 들어 있어 씬과 함께 생기고 사라진다.
// 싱글톤/DontDestroyOnLoad를 쓰지 않는 이유는 HUDManager와 같음.
public class LockOnUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LockOnSystem lockOnSystem;
    [SerializeField] private GameObject lockOnUIPrefab;

    private Camera _mainCamera;
    private Canvas _canvas;
    private RectTransform _indicatorRoot;
    private List<LockOnTargetUI> _pool = new List<LockOnTargetUI>();
    private int _activeCount;

    private void Start()
    {
        _canvas = GetComponentInParent<Canvas>();
        _indicatorRoot = transform as RectTransform;
        RefreshCameraReference();
    }

    private void Update()
    {
        // 인스펙터 연결 우선, 비어있으면 자동 폴백.
        // 플레이어가 런타임 스폰(네트워크)이라 Start 시점엔 아직 없을 수 있어 매번 확인함.
        if (lockOnSystem == null && GameManager.Instance != null && GameManager.Instance.playerRef != null)
            lockOnSystem = GameManager.Instance.playerRef.GetComponent<LockOnSystem>();
        RefreshCameraReference();

        if (lockOnSystem == null || _mainCamera == null || _indicatorRoot == null) return;

        _activeCount = 0;

        if (lockOnSystem.currentLockMode == LOCK_ON_MODE.SINGLE)
            UpdateSingle();
        else
            UpdateMulti();

        for (int i = _activeCount; i < _pool.Count; i++)
            _pool[i].Hide();
    }

    private void UpdateSingle()
    {
        // 락온 진행 중인 후보
        if (lockOnSystem.LockOnCandidate != null)
        {
            bool isLocked = lockOnSystem.IsLocked;
            float progress = lockOnSystem.GetLockOnProgress(lockOnSystem.LockOnCandidate);
            ShowIndicator(lockOnSystem.LockOnCandidate.position, progress, isLocked);
        }
    }

    private void UpdateMulti()
    {
        // 락온 진행 중인 후보들
        foreach (Transform target in lockOnSystem.MultiLockCandidates)
        {
            if (target == null) continue;
            bool isLocked = lockOnSystem.MultiLockedTargets.Contains(target);
            float progress = lockOnSystem.GetLockOnProgress(target);
            ShowIndicator(target.position, progress, isLocked);
        }
    }

    private void ShowIndicator(Vector3 worldPos, float progress, bool isLocked)
    {
        if (_canvas != null && _canvas.renderMode == RenderMode.WorldSpace)
        {
            ShowWorldSpaceIndicator(worldPos, progress, isLocked);
            return;
        }

        Vector3 screenPos = _mainCamera.WorldToScreenPoint(
            worldPos,
            Camera.MonoOrStereoscopicEye.Mono);
        if (screenPos.z < 0f) return; // 카메라 뒤면 표시 안 함

        Camera uiCamera = _canvas != null &&
                          _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? (_canvas.worldCamera != null ? _canvas.worldCamera : _mainCamera)
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _indicatorRoot,
                screenPos,
                uiCamera,
                out Vector2 localPosition))
        {
            return;
        }

        LockOnTargetUI ui = GetUI(_activeCount);
        ui.Show();
        ui.UpdateUI(localPosition, progress, isLocked);
        _activeCount++;
    }

    private void ShowWorldSpaceIndicator(
        Vector3 worldPos,
        float progress,
        bool isLocked)
    {
        Vector3 cameraPosition = _mainCamera.transform.position;
        Vector3 targetDirection = worldPos - cameraPosition;
        if (targetDirection.sqrMagnitude < 0.0001f ||
            Vector3.Dot(_mainCamera.transform.forward, targetDirection) <= 0f)
        {
            return;
        }

        RectTransform canvasRect = _canvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        // Project the actual camera-to-target ray onto the ship-fixed HUD
        // plane. Viewport mapping drifts when the player turns their head.
        Plane hudPlane = new Plane(canvasRect.forward, canvasRect.position);
        Ray targetRay = new Ray(cameraPosition, targetDirection.normalized);
        if (!hudPlane.Raycast(targetRay, out float enter) || enter <= 0f)
        {
            return;
        }

        Vector3 canvasLocalPosition =
            canvasRect.InverseTransformPoint(targetRay.GetPoint(enter));
        Rect rect = canvasRect.rect;
        const float markerPadding = 70f;
        canvasLocalPosition.x = Mathf.Clamp(
            canvasLocalPosition.x,
            rect.xMin + markerPadding,
            rect.xMax - markerPadding);
        canvasLocalPosition.y = Mathf.Clamp(
            canvasLocalPosition.y,
            rect.yMin + markerPadding,
            rect.yMax - markerPadding);
        canvasLocalPosition.z = 0f;

        Vector3 indicatorLocalPosition = _indicatorRoot.InverseTransformPoint(
            canvasRect.TransformPoint(canvasLocalPosition));
        Vector2 localPosition = new Vector2(
            indicatorLocalPosition.x,
            indicatorLocalPosition.y);

        LockOnTargetUI ui = GetUI(_activeCount);
        ui.Show();
        ui.UpdateUI(localPosition, progress, isLocked);
        _activeCount++;
    }

    private void RefreshCameraReference()
    {
        Camera activeMainCamera = Camera.main;
        if (activeMainCamera != null &&
            activeMainCamera.isActiveAndEnabled &&
            activeMainCamera != _mainCamera)
        {
            _mainCamera = activeMainCamera;
            return;
        }

        if (_mainCamera == null || !_mainCamera.isActiveAndEnabled)
        {
            _mainCamera = activeMainCamera;
        }
    }

    private LockOnTargetUI GetUI(int index)
    {
        if (index < _pool.Count) return _pool[index];

        GameObject go = Instantiate(lockOnUIPrefab, transform);
        LockOnTargetUI ui = go.GetComponent<LockOnTargetUI>();
        _pool.Add(ui);
        return ui;
    }
}
