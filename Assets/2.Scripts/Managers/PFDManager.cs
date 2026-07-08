using UnityEngine;
using TMPro;

public class PFDManager : MonoBehaviour
{
    public static PFDManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Player    player;
    [SerializeField] private Rigidbody playerRb;

    [Header("Speedometer")]
    [SerializeField] private TMP_Text speedText;

    [Header("Throttle")]
    [SerializeField] private TMP_Text throttleText;

    [Header("Altitude")]
    [SerializeField] private TMP_Text altitudeText;

    private int   _cachedSpeed    = -1;
    private int   _cachedAltitude = -1;
    private float _cachedThrottle = -1f;
    private float _updateTimer    = 0f;
    private const float INTERVAL  = 0.05f; // 20fps 갱신

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        // 인스펙터 연결 우선, 비어있으면 GameManager.playerRef에서 자동 폴백
        if (player == null || playerRb == null)
        {
            Player p = GameManager.Instance != null ? GameManager.Instance.playerRef : null;
            if (p != null)
            {
                if (player == null)   player   = p;
                if (playerRb == null) playerRb = p.GetComponent<Rigidbody>();
            }
        }

        if (player == null || playerRb == null) return;

        _updateTimer += Time.deltaTime;
        if (_updateTimer < INTERVAL) return;
        _updateTimer = 0f;

        UpdateSpeed();
        UpdateThrottle();
        UpdateAltitude();
    }

    private void UpdateSpeed()
    {
        int speed = Mathf.RoundToInt(playerRb.velocity.magnitude);
        if (speed == _cachedSpeed) return;
        _cachedSpeed = speed;

        if (speedText != null) speedText.text = $"{speed}";
    }

    private void UpdateThrottle()
    {
        float ratio = player.maxSpeed > 0
            ? playerRb.velocity.magnitude / player.maxSpeed : 0f;
        ratio = Mathf.Clamp01(ratio);

        if (Mathf.Approximately(ratio, _cachedThrottle)) return;
        _cachedThrottle = ratio;

        if (throttleText != null) throttleText.text = $"{Mathf.RoundToInt(ratio * 100)}%";
    }

    private void UpdateAltitude()
    {
        int altitude = Mathf.RoundToInt(player.transform.position.y);
        if (altitude == _cachedAltitude) return;
        _cachedAltitude = altitude;

        if (altitudeText != null) altitudeText.text = $"{altitude} m";
    }
}