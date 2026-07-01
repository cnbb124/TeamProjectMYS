using System.Collections.Generic;
using UnityEngine;

public class LockOnUIManager : MonoBehaviour
{
    public static LockOnUIManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private LockOnSystem lockOnSystem;
    [SerializeField] private GameObject lockOnUIPrefab;

    private Camera _mainCamera;
    private List<LockOnTargetUI> _pool = new List<LockOnTargetUI>();
    private int _activeCount;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        _mainCamera = Camera.main; // 한 번만 캐싱
    }

    private void Update()
    {
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
