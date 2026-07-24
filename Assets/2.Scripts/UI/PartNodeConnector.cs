/*
 * [PartNodeConnector]
 * 격납고 파츠 노드 버튼(양쪽 컬럼)과 기체 도식 위 앵커를 잇는 라인을 그린다.
 * 각 연결마다 UI Image 한 줄을 만들어 노드와 앵커 사이를 잇도록 위치/길이/각도를 잡는다.
 *
 * [부착 / 연결]
 * 1. HangarPartPanel(또는 노드/앵커를 아우르는 부모)에 이 스크립트 부착
 * 2. Line Container : 라인이 생성될 부모 RectTransform.
 *      노드/앵커보다 형제 순서상 "뒤(위쪽)"에 두면 노드가 라인 위에 그려짐. 비우면 자기 자신.
 * 3. Connections : 각 (노드 버튼, 앵커) 짝을 등록
 *      node   = 컬럼의 PartNode 버튼 RectTransform
 *      anchor = 기체 도식 위 PartNodeAnchor(위치 마커)의 RectTransform
 *
 * [주의]
 * - 노드/앵커는 같은 Canvas 아래에 있어야 좌표 변환이 맞는다.
 * - 라인은 매 프레임 위치를 다시 계산하므로 레이아웃이 나중에 정해져도(첫 프레임 뒤) 정확히 붙는다.
 * - Image라 색/두께/스프라이트(점선 등)를 인스펙터에서 바꿀 수 있다.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PartNodeConnector : MonoBehaviour
{
    [System.Serializable]
    public class Connection
    {
        [Tooltip("컬럼의 노드 버튼")]
        public RectTransform node;
        [Tooltip("기체 도식 위 앵커(위치 마커)")]
        public RectTransform anchor;
    }

    [Header("연결 (노드 ↔ 앵커)")]
    [SerializeField] private List<Connection> connections = new List<Connection>();

    /// <summary>노드↔앵커 짝 목록 (장착 컨트롤러가 재활용).</summary>
    public IReadOnlyList<Connection> Connections => connections;

    [Header("라인 생성 위치")]
    [Tooltip("라인 Image가 생성될 부모. 비우면 이 오브젝트. 노드/앵커보다 뒤 형제에 두면 라인이 아래로 깔림")]
    [SerializeField] private RectTransform lineContainer;

    [Header("라인 모양")]
    [SerializeField] private Color  lineColor     = new Color(0.4f, 0.8f, 1f, 0.6f);
    [SerializeField] private float  lineThickness = 2f;
    [Tooltip("라인에 쓸 스프라이트(점선 등). 비우면 단색")]
    [SerializeField] private Sprite lineSprite;

    private readonly List<RectTransform> _lines = new List<RectTransform>();
    private Canvas _canvas;

    private void OnEnable()
    {
        if (_canvas == null)       _canvas = GetComponentInParent<Canvas>();
        if (lineContainer == null) lineContainer = transform as RectTransform;
        BuildLines();
    }

    private void OnDisable()
    {
        ClearLines();
    }

    // 라인이 노드/앵커의 최신 위치를 계속 따라가도록 매 프레임 갱신.
    // 레이아웃 그룹이 첫 프레임 뒤에 위치를 정하는 경우에도 어긋나지 않게 하기 위함.
    private void LateUpdate()
    {
        Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? _canvas.worldCamera : null;

        for (int i = 0; i < connections.Count && i < _lines.Count; i++)
        {
            Connection c = connections[i];
            RectTransform line = _lines[i];
            if (line == null || c == null || c.node == null || c.anchor == null) continue;

            Vector2 a = ToContainerLocal(c.node.position, cam);
            Vector2 b = ToContainerLocal(c.anchor.position, cam);

            Vector2 diff = b - a;
            float dist   = diff.magnitude;
            float angle  = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            line.localPosition = (a + b) * 0.5f;               // 두 점의 중점
            line.sizeDelta     = new Vector2(dist, lineThickness);
            line.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void BuildLines()
    {
        ClearLines();

        foreach (Connection c in connections)
        {
            if (c == null || c.node == null || c.anchor == null)
            {
                _lines.Add(null);   // 인덱스를 connections와 맞춰둠
                continue;
            }
            _lines.Add(CreateLine());
        }
    }

    private RectTransform CreateLine()
    {
        GameObject go = new GameObject("NodeLine", typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(lineContainer, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.SetAsFirstSibling();   // 노드/앵커보다 뒤로 깔기

        Image img = go.GetComponent<Image>();
        img.color         = lineColor;
        img.raycastTarget = false;      // 라인이 버튼 클릭을 가로채지 않도록
        if (lineSprite != null) img.sprite = lineSprite;

        return rt;
    }

    private void ClearLines()
    {
        foreach (RectTransform line in _lines)
            if (line != null) Destroy(line.gameObject);
        _lines.Clear();
    }

    // 월드좌표 → lineContainer 로컬좌표 (모든 캔버스 렌더모드 대응)
    private Vector2 ToContainerLocal(Vector3 worldPos, Camera cam)
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(lineContainer, screen, cam, out Vector2 local);
        return local;
    }
}
