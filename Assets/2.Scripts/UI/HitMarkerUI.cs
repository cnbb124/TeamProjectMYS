using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 피격 방향 인디케이터.
/// Player.onHitDirectionWorld 이벤트를 구독해 피격 시 화면 중앙 주변에 호(arc) 형태로 방향 표시.
/// Shield에 맞으면 연한 파랑, HP가 깎이면 빨간색으로 표시.
/// </summary>
public class HitMarkerUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private Camera mainCam;

    [Header("Prefab / Parent")]
    [SerializeField] private GameObject    markerPrefab;
    [SerializeField] private RectTransform markerParent;

    [Header("설정")]
    [SerializeField] private float radius    = 120f;
    [SerializeField] private float duration  = 1.5f;
    [SerializeField] private int   maxMarkers = 4;

    [Header("색상")]
    [SerializeField] private Color shieldHitColor = new Color(0.4f, 0.8f, 1f, 1f); // 연한 파랑
    [SerializeField] private Color hpHitColor     = new Color(1f,   0.1f, 0.1f, 1f); // 빨간색

    // 풀
    private CanvasGroup[] _pool;
    private RectTransform[] _rects;
    private Image[] _images;
    private int _poolIndex = 0;

    // 직전 프레임 스탯 (Shield/HP 감소 여부 판단용)
    private int _prevShield;
    private int _prevHp;

    private void Awake()
    {
        _pool   = new CanvasGroup[maxMarkers];
        _rects  = new RectTransform[maxMarkers];
        _images = new Image[maxMarkers];

        for (int i = 0; i < maxMarkers; i++)
        {
            GameObject go = Instantiate(markerPrefab, markerParent);
            _pool[i]   = go.GetComponent<CanvasGroup>();
            _rects[i]  = go.GetComponent<RectTransform>();
            _images[i] = go.GetComponentInChildren<Image>();
            go.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (player != null)
            player.onHitDirectionWorld += OnHit;
    }

    private void OnDisable()
    {
        if (player != null)
            player.onHitDirectionWorld -= OnHit;
    }

    private void LateUpdate()
    {
        if (player == null) return;
        _prevShield = player.curShieldRemaining;
        _prevHp     = player.curHpRemaining;
    }

    private void OnHit(Vector3 hitDir)
    {
        // 이벤트 발동 시점엔 이미 데미지 적용 완료 → 직전 값과 비교
        bool shieldDecreased = player.curShieldRemaining < _prevShield;
        bool hpDecreased     = player.curHpRemaining     < _prevHp;

        Color markerColor = (hpDecreased) ? hpHitColor : shieldHitColor;

        // 공격자 방향 (-hitDir = 플레이어→공격자)
        Vector3 toAttacker = -hitDir;
        Vector3 camLocal   = mainCam.transform.InverseTransformDirection(toAttacker);
        float angle        = Mathf.Atan2(camLocal.x, camLocal.z) * Mathf.Rad2Deg;

        int idx = _poolIndex % maxMarkers;
        _poolIndex++;

        StartCoroutine(ShowMarker(idx, angle, markerColor));
    }

    private IEnumerator ShowMarker(int idx, float angle, Color color)
    {
        CanvasGroup   cg = _pool[idx];
        RectTransform rt = _rects[idx];
        Image        img = _images[idx];

        float rad = angle * Mathf.Deg2Rad;
        rt.anchoredPosition  = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * radius;
        rt.localRotation     = Quaternion.Euler(0f, 0f, -angle);

        if (img != null) img.color = color;

        cg.gameObject.SetActive(true);
        cg.alpha = 1f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed  += Time.deltaTime;
            cg.alpha  = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }

        cg.gameObject.SetActive(false);
    }
}
