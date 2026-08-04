using System.Collections.Generic;
using UnityEngine;

// 락온 마커도 전투씬 전용임 — HUD 프리팹 안에 들어 있어 씬과 함께 생기고 사라진다.
// 싱글톤/DontDestroyOnLoad를 쓰지 않는 이유는 HUDManager와 같음.
//
// 화면 좌표로 찍으면 VR 콕핏 시점에서 어긋나므로, 타겟 위치에 월드 마커를 세움.
public class LockOnUIManager : MonoBehaviour
{
    private const float MarkerCanvasSize = 100f;

    [Header("References")]
    [SerializeField] private LockOnSystem lockOnSystem;
    [SerializeField] private GameObject lockOnUIPrefab;

    [Header("월드 마커")]
    [Tooltip("마커 크기. 키우면 화면에서 크게 보임.")]
    [SerializeField] private float markerSize = 0.05f;

    [Tooltip("마커가 최대로 커지는 거리. 이보다 가까워져도 더 커지지 않음. 0이면 안 씀.")]
    [SerializeField] private float markerMaxSizeDistance = 50f;

    [Tooltip("마커가 최대로 작아지는 거리. 이보다 멀어져도 더 작아지지 않음. 0이면 안 씀.")]
    [SerializeField] private float markerMinSizeDistance = 800f;

    private class Marker
    {
        public GameObject root;
        public Transform tr;
        public LockOnTargetUI ui;
    }

    private readonly List<Marker> _pool = new List<Marker>();
    private Transform _markerRoot;
    private int _activeCount;

    private void Update()
    {
        // 인스펙터 연결 우선, 비어있으면 자동 폴백.
        // 플레이어가 런타임 스폰(네트워크)이라 Start 시점엔 아직 없을 수 있어 매번 확인함.
        if (lockOnSystem == null && GameManager.Instance != null && GameManager.Instance.playerRef != null)
            lockOnSystem = GameManager.Instance.playerRef.GetComponent<LockOnSystem>();

        if (lockOnSystem == null || lockOnUIPrefab == null)
        {
            HideFrom(0);
            return;
        }

        _activeCount = 0;

        if (lockOnSystem.currentLockMode == LOCK_ON_MODE.SINGLE)
            UpdateSingle();
        else
            UpdateMulti();

        HideFrom(_activeCount);
    }

    private void OnDisable()
    {
        HideFrom(0);
    }

    private void OnDestroy()
    {
        // 마커는 HUD 밖 독립 루트에 있어서 HUD가 사라져도 남음.
        if (_markerRoot != null)
        {
            Destroy(_markerRoot.gameObject);
        }
    }

    private void UpdateSingle()
    {
        // 락온 진행 중인 후보
        if (lockOnSystem.LockOnCandidate != null)
        {
            bool isLocked = lockOnSystem.IsLocked;
            float progress = lockOnSystem.GetLockOnProgress(lockOnSystem.LockOnCandidate);
            ShowMarker(lockOnSystem.LockOnCandidate, progress, isLocked);
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
            ShowMarker(target, progress, isLocked);
        }
    }

    private void ShowMarker(Transform target, float progress, bool isLocked)
    {
        Marker marker = GetMarker(_activeCount);
        marker.tr.position = target.position;

        if (!marker.root.activeSelf)
        {
            marker.root.SetActive(true);
        }

        marker.ui.UpdateUI(progress, isLocked);
        _activeCount++;
    }

    private void HideFrom(int startIndex)
    {
        for (int i = startIndex; i < _pool.Count; i++)
        {
            Marker marker = _pool[i];
            if (marker.root != null && marker.root.activeSelf)
            {
                marker.root.SetActive(false);
            }
        }
    }

    // UI 프리팹이 RectTransform 기반이라 캔버스가 있어야 그려짐 — 월드 캔버스를 런타임에 씌움.
    private Marker GetMarker(int index)
    {
        if (index < _pool.Count)
        {
            return _pool[index];
        }

        if (_markerRoot == null)
        {
            _markerRoot = new GameObject("LockOnMarkers").transform;
        }

        GameObject markerObject = new GameObject(
            "LockOnMarker",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(WorldBillboard));
        markerObject.layer = lockOnUIPrefab.layer;
        markerObject.transform.SetParent(_markerRoot, false);

        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.sizeDelta = new Vector2(MarkerCanvasSize, MarkerCanvasSize);
        markerRect.pivot = new Vector2(0.5f, 0.5f);

        Canvas canvas = markerObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        WorldBillboard billboard = markerObject.GetComponent<WorldBillboard>();
        billboard.Configure(markerSize, markerMaxSizeDistance, markerMinSizeDistance);

        GameObject uiObject = Instantiate(lockOnUIPrefab, markerObject.transform);

        RectTransform uiRect = uiObject.GetComponent<RectTransform>();
        if (uiRect != null)
        {
            uiRect.anchorMin = new Vector2(0.5f, 0.5f);
            uiRect.anchorMax = new Vector2(0.5f, 0.5f);
            uiRect.pivot = new Vector2(0.5f, 0.5f);
            uiRect.anchoredPosition = Vector2.zero;
        }

        Marker marker = new Marker
        {
            root = markerObject,
            tr = markerObject.transform,
            ui = uiObject.GetComponent<LockOnTargetUI>()
        };

        _pool.Add(marker);
        return marker;
    }
}
