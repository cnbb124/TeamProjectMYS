using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TargettingRadarSystem : MonoBehaviour
{
    public static TargettingRadarSystem Instance { get; private set; }

    [Header("References")]
    [SerializeField] private LockOnSystem lockOnSystem;
    [SerializeField] private Transform player;
    [SerializeField] private RectTransform radarRect;
    [SerializeField] private GameObject dotPrefab;

    [Header("Player Arrow")]
    [SerializeField] private RectTransform playerArrow;

    [Header("Settings")]
    [SerializeField] private float radarDisplayRadius = 75f;

    [Header("Dot Colors")]
    [SerializeField] private Color colorNormal    = Color.white;
    [SerializeField] private Color colorCandidate = Color.cyan;
    [SerializeField] private Color colorLocked    = Color.red;

    private List<Image> _dotPool = new List<Image>();
    private int _activeDotCount = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
{
    if (lockOnSystem == null || player == null) return;

    if (playerArrow != null)
    {
        playerArrow.anchoredPosition = Vector2.zero;
        playerArrow.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
    }

    _activeDotCount = 0;

    // ★ TargetsInRadarRange = 360도 전방향 (각도 필터 없음)
    foreach (Collider col in lockOnSystem.TargetsInRadarRange)
    {
        if (col == null) return;

        Transform target = col.transform;

        // 플레이어 자신 제외
        if (target == player) continue;

        Vector3 localPos = player.InverseTransformPoint(target.position);
        Vector2 radarPos = new Vector2(localPos.x, localPos.z);
        radarPos = Vector2.ClampMagnitude(radarPos, lockOnSystem.lockOnRange);
        radarPos *= radarDisplayRadius / lockOnSystem.lockOnRange;

        Image dot = GetDot(_activeDotCount);
        dot.rectTransform.anchoredPosition = radarPos;
        dot.color = GetDotColor(target);

        _activeDotCount++;
    }

    for (int i = _activeDotCount; i < _dotPool.Count; i++)
        _dotPool[i].gameObject.SetActive(false);
}

    private Color GetDotColor(Transform target)
    {
        if (IsLocked(target))         return colorLocked;
        if (target == lockOnSystem.LockOnCandidate) return colorCandidate;
        return colorNormal;
    }

    private bool IsLocked(Transform target)
    {
        if (lockOnSystem.LockedTarget == target) return true;
        if (lockOnSystem.MultiLockedTargets.Contains(target)) return true;
        return false;
    }

    private Image GetDot(int index)
    {
        if (index < _dotPool.Count)
        {
            _dotPool[index].gameObject.SetActive(true);
            return _dotPool[index];
        }

        GameObject go = Instantiate(dotPrefab, radarRect);
        Image img = go.GetComponent<Image>();
        _dotPool.Add(img);
        return img;
    }
}