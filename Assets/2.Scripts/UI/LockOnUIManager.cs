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
    private List<LockOnTargetUI> _pool = new List<LockOnTargetUI>();
    private int _activeCount;

    private void Start()
    {
        _mainCamera = Camera.main; // 한 번만 캐싱
    }

    private void Update()
    {
        // 인스펙터 연결 우선, 비어있으면 자동 폴백.
        // 플레이어가 런타임 스폰(네트워크)이라 Start 시점엔 아직 없을 수 있어 매번 확인함.
        if (lockOnSystem == null && GameManager.Instance != null && GameManager.Instance.playerRef != null)
            lockOnSystem = GameManager.Instance.playerRef.GetComponent<LockOnSystem>();
        if (_mainCamera == null) _mainCamera = Camera.main;

        if (lockOnSystem == null || _mainCamera == null) return;

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
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);
        if (screenPos.z < 0f) return; // 카메라 뒤면 표시 안 함

        LockOnTargetUI ui = GetUI(_activeCount);
        ui.Show();
        ui.UpdateUI(screenPos, progress, isLocked);
        _activeCount++;
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
