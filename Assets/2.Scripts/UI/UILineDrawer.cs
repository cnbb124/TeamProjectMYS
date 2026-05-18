using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UILineDrawer : MonoBehaviour
{
    private RectTransform _rt;

    void Awake() => _rt = GetComponent<RectTransform>();

    // start/end: anchoredPosition 기준 좌표 (contentRoot 로컬 스페이스)
    public void SetLine(Vector2 start, Vector2 end, float thickness = 2f)
    {
        if (!_rt) _rt = GetComponent<RectTransform>();
        Vector2 dir = end - start;
        _rt.anchoredPosition = start;
        _rt.sizeDelta = new Vector2(dir.magnitude, thickness);
        _rt.pivot = new Vector2(0f, 0.5f);
        _rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }
}