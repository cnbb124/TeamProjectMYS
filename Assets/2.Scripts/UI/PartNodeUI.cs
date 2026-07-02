/*
 * [PartNodeUI]
 * 격납고 기체의 PartNodeAnchor(3D 부위 마커)들을 PlaneViewerPanel(RawImage) 위의
 * 노드 버튼으로 변환·배치. 궤도 카메라를 돌리면 노드가 부위를 따라다님.
 *
 * [좌표 변환 원리]
 * 앵커 월드좌표 → 전용카메라.WorldToViewportPoint (0~1)
 *              → RawImage 로컬좌표로 매핑 (viewport * rect 크기)
 * ※ RenderTexture를 찍는 "전용 카메라" 기준이므로 메인 카메라와 무관.
 *
 * [부착] PlaneViewerPanel(또는 노드를 얹을 RawImage) 오브젝트에 부착.
 *
 * [인스펙터 연결]
 * - viewCamera     : 기체를 찍는 전용 카메라 (RenderTexture 지정된 것, HangarOrbitCamera 붙은 것)
 * - viewArea       : 노드를 배치할 RawImage의 RectTransform
 * - shipRoot       : 기체 루트 (하위의 PartNodeAnchor 자동 수집)
 * - nodePrefab     : 노드 버튼 프리팹 (루트에 Button, 자식 "Label"에 TMP_Text, 자식 "Icon"에 Image 권장)
 * - hideWhenBehind : 부위가 기체 뒤편(카메라 반대쪽)일 때 노드 숨김
 *
 * [노드 클릭]
 * onNodeClicked(PartNodeAnchor) 이벤트 발행 — 파츠 선택 패널(PartPicker)이 구독해서
 * 해당 partType 파츠 목록을 띄우면 됨.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PartNodeUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera        viewCamera;   // RenderTexture 전용 카메라
    [SerializeField] private RectTransform viewArea;     // 노드 배치 기준 RawImage
    [SerializeField] private Transform     shipRoot;     // 기체 루트 (앵커 수집용)
    [SerializeField] private GameObject    nodePrefab;   // 노드 버튼 프리팹

    [Header("Settings")]
    [SerializeField] private bool  hideWhenBehind = true;  // 기체 뒤편 부위 노드 숨김
    [SerializeField] private float behindAlpha    = 0f;    // 숨김 대신 반투명하게 하려면 0.25 등

    /// <summary>노드 클릭 시 발행 — 어느 부위(앵커)인지 전달.</summary>
    public event Action<PartNodeAnchor> onNodeClicked;

    private class Node
    {
        public PartNodeAnchor anchor;
        public RectTransform  rect;
        public CanvasGroup    group;
        public Button         button;
    }

    private readonly List<Node> _nodes = new List<Node>();

    private void OnEnable()
    {
        BuildNodes();
    }

    private void OnDisable()
    {
        ClearNodes();
    }

    /// <summary>shipRoot 하위의 앵커를 수집해 노드 생성. 기체 교체 시 다시 호출.</summary>
    public void BuildNodes()
    {
        ClearNodes();
        if (shipRoot == null || nodePrefab == null || viewArea == null) return;

        PartNodeAnchor[] anchors = shipRoot.GetComponentsInChildren<PartNodeAnchor>(true);
        foreach (PartNodeAnchor anchor in anchors)
        {
            GameObject go = Instantiate(nodePrefab, viewArea);
            Node node = new Node
            {
                anchor = anchor,
                rect   = go.GetComponent<RectTransform>(),
                group  = go.GetComponent<CanvasGroup>(),
                button = go.GetComponentInChildren<Button>()
            };
            if (node.group == null) node.group = go.AddComponent<CanvasGroup>();

            // 라벨 세팅 (자식 이름 "Label"의 TMP)
            TMP_Text label = go.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null) label.text = anchor.DisplayName;

            // 클릭 → 이벤트 발행
            if (node.button != null)
            {
                PartNodeAnchor captured = anchor; // 클로저 캡쳐
                node.button.onClick.AddListener(() => onNodeClicked?.Invoke(captured));
            }

            _nodes.Add(node);
        }
    }

    private void ClearNodes()
    {
        foreach (Node n in _nodes)
            if (n.rect != null) Destroy(n.rect.gameObject);
        _nodes.Clear();
    }

    private void LateUpdate()
    {
        if (viewCamera == null || viewArea == null) return;

        Rect area = viewArea.rect;

        foreach (Node node in _nodes)
        {
            if (node.anchor == null || node.rect == null) continue;

            // 앵커 월드좌표 → 뷰포트(0~1)
            Vector3 vp = viewCamera.WorldToViewportPoint(node.anchor.transform.position);

            // 카메라 뒤면 숨김
            bool visible = vp.z > 0f;

            // 기체 뒤편 판정: 카메라→앵커 방향과 카메라→기체중심 방향 비교
            if (visible && hideWhenBehind && shipRoot != null)
            {
                float anchorDist = vp.z;
                float centerDist = viewCamera.WorldToViewportPoint(shipRoot.position).z;
                bool behind = anchorDist > centerDist; // 중심보다 멀면 뒤편
                if (behind)
                {
                    if (behindAlpha <= 0f) visible = false;
                    else node.group.alpha = behindAlpha;
                }
                else node.group.alpha = 1f;
            }

            node.group.alpha = visible ? node.group.alpha : 0f;
            node.group.blocksRaycasts = visible;
            if (!visible) continue;

            // 뷰포트(0~1) → RawImage 로컬 좌표
            // (pivot 고려: rect.min이 좌하단)
            Vector2 localPos = new Vector2(
                area.xMin + vp.x * area.width,
                area.yMin + vp.y * area.height);

            node.rect.anchoredPosition = localPos;
        }
    }
}
