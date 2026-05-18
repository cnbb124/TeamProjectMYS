using System.Collections.Generic;
using UnityEngine;

public class HangarTreeManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject nodeButtonPrefab;
    public GameObject linePrefab;

    [Header("Scene References")]
    public RectTransform contentRoot;  // ScrollView > Viewport > Content
    public RectTransform linesRoot;    // Content > Lines_Layer

    [Header("Tree Root")]
    public PlaneNodeData rootNode;

    [Header("Layout")]
    public Vector2 nodeSize      = new(110f, 110f);
    public Vector2 nodeSpacing   = new(80f, 60f);
    public Vector2 padding       = new(40f, 40f);
    public float   lineThickness = 2f;

    private readonly Dictionary<PlaneNodeData, Vector2>      _positions = new();
    private readonly Dictionary<PlaneNodeData, PlaneNodeButton> _buttons = new();

    void Start() => BuildTree();

    public void BuildTree()
    {
        ClearAll();
        if (!rootNode) return;

        CalculateLayout(rootNode, 0, 0);
        SpawnNodes(rootNode);
        DrawLines(rootNode);
        FitContent();
    }

    // 재귀 레이아웃 — 소비한 행 수를 반환
    int CalculateLayout(PlaneNodeData node, int col, int rowStart)
    {
        float stepX = nodeSize.x + nodeSpacing.x;
        float stepY = nodeSize.y + nodeSpacing.y;

        if (node.children == null || node.children.Count == 0)
        {
            _positions[node] = new Vector2(col * stepX, -rowStart * stepY);
            return 1;
        }

        int cur = rowStart;
        foreach (var child in node.children)
            cur += CalculateLayout(child, col + 1, cur);

        float topY    = _positions[node.children[0]].y;
        float bottomY = _positions[node.children[^1]].y;
        _positions[node] = new Vector2(col * stepX, (topY + bottomY) / 2f);

        return cur - rowStart;
    }

    void SpawnNodes(PlaneNodeData node)
    {
        var go = Instantiate(nodeButtonPrefab, contentRoot);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = _positions[node] + padding;
        rt.sizeDelta = nodeSize;

        var btn = go.GetComponent<PlaneNodeButton>();
        if (btn)
        {
            btn.Initialize(node);
            _buttons[node] = btn;
        }

        if (node.children == null) return;
        foreach (var child in node.children) SpawnNodes(child);
    }

    void DrawLines(PlaneNodeData node)
    {
        if (node.children == null) return;
        foreach (var child in node.children)
        {
            DrawConnection(node, child);
            DrawLines(child);
        }
    }

    // ┤ 형태의 L자 커넥터 (parent 오른쪽 → child 왼쪽)
    void DrawConnection(PlaneNodeData parent, PlaneNodeData child)
    {
        Vector2 pPos = _positions[parent] + padding;
        Vector2 cPos = _positions[child]  + padding;

        Vector2 start = pPos + new Vector2(nodeSize.x, nodeSize.y * 0.5f);
        Vector2 end   = cPos + new Vector2(0f,         nodeSize.y * 0.5f);
        float   midX  = (start.x + end.x) * 0.5f;

        SpawnLine(start,                    new Vector2(midX, start.y));
        SpawnLine(new Vector2(midX, start.y), new Vector2(midX, end.y));
        SpawnLine(new Vector2(midX, end.y), end);
    }

    void SpawnLine(Vector2 from, Vector2 to)
    {
        var go = Instantiate(linePrefab, linesRoot);
        go.GetComponent<UILineDrawer>()?.SetLine(from, to, lineThickness);
    }

    void FitContent()
    {
        float maxX = 0f, minY = 0f;
        foreach (var p in _positions.Values)
        {
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
        }
        contentRoot.sizeDelta = new Vector2(
            maxX + nodeSize.x + padding.x * 2f,
            Mathf.Abs(minY) + nodeSize.y + padding.y * 2f
        );
    }

    void ClearAll()
    {
        _positions.Clear();
        _buttons.Clear();

        var toDestroy = new List<Transform>();
        foreach (Transform t in contentRoot)
            if (t != linesRoot) toDestroy.Add(t);
        foreach (var t in toDestroy) Destroy(t.gameObject);
        foreach (Transform t in linesRoot) Destroy(t.gameObject);
    }

    public PlaneNodeButton GetButton(PlaneNodeData data) =>
        _buttons.TryGetValue(data, out var btn) ? btn : null;
}