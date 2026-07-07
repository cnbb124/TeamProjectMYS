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

    [Header("Cardinal Direction")]
    [SerializeField] private RectTransform cardinalRoot; // East/West/South/North 묶음

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
    // 인스펙터 연결 우선, 비어있으면 GameManager.playerRef에서 자동 폴백
    if (GameManager.Instance != null && GameManager.Instance.playerRef != null)
    {
        Player p = GameManager.Instance.playerRef;
        if (lockOnSystem == null) lockOnSystem = p.GetComponent<LockOnSystem>();
        if (player == null)       player       = p.transform;
    }

    if (lockOnSystem == null || player == null) return;

    if (playerArrow != null)
    {
        playerArrow.anchoredPosition = Vector2.zero;
    }

    if (cardinalRoot != null)
        cardinalRoot.localRotation = Quaternion.Euler(0f, 0f, player.eulerAngles.y);

    _activeDotCount = 0;

    // ★ TargetsInRadarRange = 360도 전방향 (각도 필터 없음)
    // NonAlloc 버퍼라 배열 뒤쪽엔 이전 프레임 잔여값이 남음 → 실제 감지 수(RadarHitCount)까지만 순회
    for (int idx = 0; idx < lockOnSystem.RadarHitCount; idx++)
    {
        Collider col = lockOnSystem.TargetsInRadarRange[idx];
        if (col == null) continue;

        Transform target = col.transform;

        // 플레이어 자신 제외 — col이 플레이어의 자식(LockOnBox 등)이어도 걸러지도록
        // GetComponentInParent<Player>()로 소속을 검사 (target == player 직접 비교는 자식이라 실패함)
        if (col.GetComponentInParent<Player>() != null) continue;

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