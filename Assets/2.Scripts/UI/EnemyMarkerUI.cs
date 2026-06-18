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
/// - edgePadding  : 화면 가장자리 클램프 여백(px)
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
        if (lockOnSystem == null || mainCam == null) return;

        Collider[] targets = lockOnSystem.TargetsInRadarRange;
        int count = targets == null ? 0 : targets.Length;

        // 마커 풀 크기 조정
        while (_markers.Count < count) AddMarker();
        for (int i = count; i < _markers.Count; i++)
            _markers[i].root.SetActive(false);

        // 각 적마다 마커 갱신
        for (int i = 0; i < count; i++)
        {
            if (targets[i] == null || targets[i].transform.root.CompareTag("Player"))
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

        float halfW = Screen.width  * 0.5f;
        float halfH = Screen.height * 0.5f;

        bool isOnScreen = screenPos.z > 0f
            && screenPos.x > 0f && screenPos.x < Screen.width
            && screenPos.y > 0f && screenPos.y < Screen.height;

        if (isOnScreen)
        {
            // 화면 내 → 실제 위치에 마커
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                markerParent, screenPos, null, out Vector2 localPos);
            m.rect.localPosition = localPos;

            m.arrow?.gameObject.SetActive(false);
            m.rect.localScale = Vector3.one * onScreenScale;
        }
        else
        {
            // 화면 밖 → 가장자리 클램프
            Vector2 dir;
            if (screenPos.z < 0f)
            {
                // 카메라 뒤 → 방향 반전
                screenPos.x = Screen.width  - screenPos.x;
                screenPos.y = Screen.height - screenPos.y;
            }

            dir = new Vector2(screenPos.x - halfW, screenPos.y - halfH).normalized;

            // 화면 가장자리까지의 비율 계산 (직사각형 클램프)
            float maxX = halfW - edgePadding;
            float maxY = halfH - edgePadding;
            float scaleX = dir.x != 0f ? maxX / Mathf.Abs(dir.x) : float.MaxValue;
            float scaleY = dir.y != 0f ? maxY / Mathf.Abs(dir.y) : float.MaxValue;
            float scale  = Mathf.Min(scaleX, scaleY);

            Vector2 clampedPos = dir * scale;
            m.rect.localPosition = clampedPos;

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
