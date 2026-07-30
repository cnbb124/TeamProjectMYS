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

    [Header("색상")]
    [SerializeField] private Color markerColor = new Color(1f, 0.1f, 0.1f, 1f); // 빨간색

    // 마커 인스턴스 풀 (적 수만큼 동적 확장)
    private readonly List<MarkerInstance> _markers = new List<MarkerInstance>();

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
        if (mainCam == null) mainCam = Camera.main;

        if (lockOnSystem == null || mainCam == null) return;

        Collider[] targets = lockOnSystem.TargetsInRadarRange;
        // NonAlloc 버퍼라 배열 뒤쪽엔 이전 프레임 잔여값이 남음 → 실제 감지 수(RadarHitCount)까지만 순회
        int count = targets == null ? 0 : lockOnSystem.RadarHitCount;

        // 마커 풀 크기 조정
        while (_markers.Count < count) AddMarker();
        for (int i = count; i < _markers.Count; i++)
            _markers[i].root.SetActive(false);

        // 각 적마다 마커 갱신
        for (int i = 0; i < count; i++)
        {
            if (targets[i] == null || targets[i].GetComponentInParent<Player>() != null)
            {
                _markers[i].root.SetActive(false);
                continue;
            }

            _markers[i].root.SetActive(true);
            UpdateMarker(_markers[i], targets[i].transform);
        }
    }

    private void UpdateMarker(MarkerInstance m, Transform target)
    {
        Vector3 screenPos = mainCam.WorldToScreenPoint(target.position);

        // 화면 안/밖 판정은 픽셀 기준(screenPos가 픽셀이므로).
        bool isOnScreen = screenPos.z > 0f
            && screenPos.x > 0f && screenPos.x < Screen.width
            && screenPos.y > 0f && screenPos.y < Screen.height;

        // 마커 좌표는 부모 사각형의 로컬 단위로 다룸 — CanvasScaler가 걸려 있으면 로컬 단위가
        // 픽셀과 다르므로(레퍼런스 해상도 기준), 픽셀로 계산하면 다른 해상도에서 위치가 어긋남.
        Rect parentRect = markerParent.rect;
        Vector2 center = parentRect.center;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            markerParent, screenPos, null, out Vector2 localPos);

        if (isOnScreen)
        {
            // 화면 내 → 실제 위치에 마커
            m.rect.localPosition = localPos;

            m.arrow?.gameObject.SetActive(false);
            m.rect.localScale = Vector3.one * onScreenScale;
        }
        else
        {
            // 화면 밖 → 가장자리 클램프
            // 카메라 뒤면 투영이 반전돼 나오므로 사각형 중심 기준으로 되뒤집음
            if (screenPos.z < 0f)
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
            float scale  = Mathf.Min(scaleX, scaleY);

            m.rect.localPosition = center + dir * scale;

            // 화살표 회전 (적 방향을 가리킴)
            if (m.arrow != null)
            {
                m.arrow.gameObject.SetActive(true);
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                m.arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            m.rect.localScale = Vector3.one * offScreenScale;
        }

        // 거리 표시
        if (m.distText != null)
        {
            float dist = Vector3.Distance(mainCam.transform.position, target.position);
            m.distText.text = $"{Mathf.RoundToInt(dist)}m";
        }
    }

    private void AddMarker()
    {
        GameObject go = Instantiate(markerPrefab, markerParent);
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

        _markers.Add(m);
    }
}
