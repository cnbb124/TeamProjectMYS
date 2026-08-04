using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 적 위치 마커. LockOnSystem.TargetsInRadarRange 기준으로 매 프레임 갱신.
/// 화면 안 → 적 위치에 마커 표시.
/// 화면 밖 → 화면 가장자리에 클램프, 방향 화살표로 표시.
///
/// [마커 프리팹 구조]
/// - RectTransform
///   - Image (마커 아이콘)
///   - ArrowImage (화면 밖일 때 방향 화살표, 화면 안이면 비활성)
///   - DistanceText (TMP_Text, 거리 표시)
///
/// [설정]
/// - lockOnSystem : 플레이어의 LockOnSystem 참조
/// - markerPrefab : EnemyMarker 프리팹
/// - markerParent : 마커를 붙일 Canvas 하위 RectTransform
/// - edgePadding  : 화면 가장자리 클램프 여백. 단위는 markerParent의 로컬 단위
///                  (CanvasScaler 레퍼런스가 1920x1080이면 그 기준의 px와 같음)
/// </summary>
public class EnemyMarkerUI : MonoBehaviour
{
    private const float MarkerCanvasSize = 100f;

    [Header("References")]
    [SerializeField] private LockOnSystem lockOnSystem;
    [SerializeField] private Camera       mainCam;
    [SerializeField] private Transform    playerTransform;

    [Header("Prefab / Parent")]
    [SerializeField] private GameObject   markerPrefab;
    [SerializeField] private RectTransform markerParent;

    [Header("설정")]
    [SerializeField] private float edgePadding   = 20f;
    [SerializeField] private float onScreenScale  = 1f;
    [SerializeField] private float offScreenScale = 0.8f;

    [Header("월드 마커")]
    [Tooltip("마커 크기. 키우면 화면에서 크게 보임.")]
    [SerializeField] private float markerSize = 0.05f;

    [Tooltip("마커가 최대로 커지는 거리. 이보다 가까워져도 더 커지지 않음. 0이면 안 씀.")]
    [SerializeField] private float markerMaxSizeDistance = 50f;

    [Tooltip("마커가 최대로 작아지는 거리. 이보다 멀어져도 더 작아지지 않음. 0이면 안 씀.")]
    [SerializeField] private float markerMinSizeDistance = 800f;

    [Header("색상")]
    [SerializeField] private Color markerColor = new Color(1f, 0.1f, 0.1f, 1f); // 빨간색

    // 마커 인스턴스 풀 (적 수만큼 동적 확장)
    private readonly List<MarkerInstance> _edgeMarkers = new List<MarkerInstance>();
    private readonly List<MarkerInstance> _worldMarkers = new List<MarkerInstance>();
    private Transform _worldRoot;

    private class MarkerInstance
    {
        public GameObject  root;
        public RectTransform rect;
        public Image       icon;
        public Image       arrow;    // 화면 밖 방향 화살표
        public TMP_Text    distText;
    }

    private void Update()
    {
        // 인스펙터 연결 우선, 비어있으면 자동 폴백 (playerRef의 LockOnSystem / Camera.main)
        if (GameManager.Instance != null && GameManager.Instance.playerRef != null)
        {
            Player p = GameManager.Instance.playerRef;
            if (lockOnSystem == null)    lockOnSystem    = p.GetComponent<LockOnSystem>();
            if (playerTransform == null) playerTransform = p.transform;
        }
        if (mainCam == null || !mainCam.isActiveAndEnabled) mainCam = Camera.main;

        if (lockOnSystem == null || mainCam == null || markerPrefab == null) return;

        Collider[] targets = lockOnSystem.TargetsInRadarRange;
        // NonAlloc 버퍼라 배열 뒤쪽엔 이전 프레임 잔여값이 남음 → 실제 감지 수(RadarHitCount)까지만 순회
        int count = targets == null ? 0 : lockOnSystem.RadarHitCount;

        int used = 0;
        for (int i = 0; i < count; i++)
        {
            if (targets[i] == null || targets[i].GetComponentInParent<Player>() != null)
            {
                continue;
            }

            UpdateMarker(used, targets[i].transform);
            used++;
        }

        HideFrom(_edgeMarkers, used);
        HideFrom(_worldMarkers, used);
    }

    private void UpdateMarker(int index, Transform target)
    {
        Vector3 viewportPos = mainCam.WorldToViewportPoint(
            target.position,
            Camera.MonoOrStereoscopicEye.Mono);

        bool isOnScreen = viewportPos.z > 0f
            && viewportPos.x > 0f && viewportPos.x < 1f
            && viewportPos.y > 0f && viewportPos.y < 1f;

        float dist = Vector3.Distance(mainCam.transform.position, target.position);

        if (isOnScreen)
        {
            SetActiveAt(_edgeMarkers, index, false);

            MarkerInstance m = GetWorldMarker(index);
            m.root.transform.parent.gameObject.SetActive(true);
            m.root.transform.parent.position = target.position;

            m.arrow?.gameObject.SetActive(false);
            m.rect.localScale = Vector3.one * onScreenScale;
            SetDistanceText(m, dist);
            return;
        }

        SetActiveAt(_worldMarkers, index, false);

        MarkerInstance edge = GetEdgeMarker(index);
        edge.root.SetActive(true);
        PlaceOnEdge(edge, viewportPos);
        SetDistanceText(edge, dist);
    }

    // 화면 밖 마커를 부모 사각형 가장자리에 붙임.
    // 픽셀(Screen.width) 대신 뷰포트(0~1)를 쓰는 이유 — VR은 눈 텍스처 해상도와 창 해상도가 달라
    // 픽셀 기준으로 계산하면 마커가 통째로 어긋남.
    private void PlaceOnEdge(MarkerInstance m, Vector3 viewportPos)
    {
        Rect parentRect = markerParent.rect;
        Vector2 center = parentRect.center;

        Vector2 localPos = new Vector2(
            parentRect.xMin + viewportPos.x * parentRect.width,
            parentRect.yMin + viewportPos.y * parentRect.height);

        // 카메라 뒤면 투영이 반전돼 나오므로 사각형 중심 기준으로 되뒤집음
        if (viewportPos.z < 0f)
        {
            localPos = center - (localPos - center);
        }

        Vector2 dir = localPos - center;
        if (dir.sqrMagnitude < 0.0001f)
        {
            // 정확히 중심이면 방향이 정해지지 않음 — 위쪽으로 몰아둠
            dir = Vector2.up;
        }
        dir.Normalize();

        // 부모 사각형 가장자리까지의 비율 중 작은 쪽이 실제 접점
        float maxX = parentRect.width  * 0.5f - edgePadding;
        float maxY = parentRect.height * 0.5f - edgePadding;
        float scaleX = dir.x != 0f ? maxX / Mathf.Abs(dir.x) : float.MaxValue;
        float scaleY = dir.y != 0f ? maxY / Mathf.Abs(dir.y) : float.MaxValue;

        m.rect.localPosition = center + dir * Mathf.Min(scaleX, scaleY);
        m.rect.localScale = Vector3.one * offScreenScale;

        // 화살표 회전 (적 방향을 가리킴)
        if (m.arrow != null)
        {
            m.arrow.gameObject.SetActive(true);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            m.arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void SetDistanceText(MarkerInstance m, float dist)
    {
        if (m.distText != null)
        {
            m.distText.text = $"{Mathf.RoundToInt(dist)}m";
        }
    }

    private MarkerInstance GetEdgeMarker(int index)
    {
        while (_edgeMarkers.Count <= index)
        {
            GameObject go = Instantiate(markerPrefab, markerParent);
            _edgeMarkers.Add(BuildInstance(go));
        }

        return _edgeMarkers[index];
    }

    private MarkerInstance GetWorldMarker(int index)
    {
        while (_worldMarkers.Count <= index)
        {
            if (_worldRoot == null)
            {
                _worldRoot = new GameObject("EnemyWorldMarkers").transform;
            }

            GameObject holder = new GameObject(
                "EnemyWorldMarker",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(WorldBillboard));
            holder.layer = markerPrefab.layer;
            holder.transform.SetParent(_worldRoot, false);

            RectTransform holderRect = holder.GetComponent<RectTransform>();
            holderRect.sizeDelta = new Vector2(MarkerCanvasSize, MarkerCanvasSize);
            holderRect.pivot = new Vector2(0.5f, 0.5f);

            holder.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            holder.GetComponent<WorldBillboard>().Configure(
                markerSize,
                markerMaxSizeDistance,
                markerMinSizeDistance);

            GameObject go = Instantiate(markerPrefab, holder.transform);
            RectTransform goRect = go.GetComponent<RectTransform>();
            if (goRect != null)
            {
                goRect.anchorMin = new Vector2(0.5f, 0.5f);
                goRect.anchorMax = new Vector2(0.5f, 0.5f);
                goRect.pivot = new Vector2(0.5f, 0.5f);
                goRect.anchoredPosition = Vector2.zero;
            }

            _worldMarkers.Add(BuildInstance(go));
        }

        return _worldMarkers[index];
    }

    private MarkerInstance BuildInstance(GameObject go)
    {
        MarkerInstance m = new MarkerInstance
        {
            root     = go,
            rect     = go.GetComponent<RectTransform>(),
            icon     = go.transform.Find("Icon")?.GetComponent<Image>(),
            arrow    = go.transform.Find("Arrow")?.GetComponent<Image>(),
            distText = go.transform.Find("DistanceText")?.GetComponent<TMP_Text>()
        };

        if (m.icon  != null) m.icon.color  = markerColor;
        if (m.arrow != null) m.arrow.color = markerColor;

        return m;
    }

    private void HideFrom(List<MarkerInstance> list, int startIndex)
    {
        for (int i = startIndex; i < list.Count; i++)
        {
            SetActiveAt(list, i, false);
        }
    }

    private void SetActiveAt(List<MarkerInstance> list, int index, bool active)
    {
        if (index >= list.Count)
        {
            return;
        }

        GameObject target = list == _worldMarkers
            ? list[index].root.transform.parent.gameObject
            : list[index].root;

        if (target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private void OnDestroy()
    {
        if (_worldRoot != null)
        {
            Destroy(_worldRoot.gameObject);
        }
    }
}
