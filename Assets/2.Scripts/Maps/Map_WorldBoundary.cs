using UnityEngine;
using UnityEngine.UI;

public class WorldBoundary : MonoBehaviour
{
    [Header("Boundary")]
    public float boundaryRadius = 5000f;
    public float warningRadius = 4500f;
    public float returnTime = 10f;

    [Header("Vignette UI")]
    public Image vignetteImage;          // Canvas의 Image 연결
    public float maxAlpha = 0.8f;

    private float outOfBoundsTimer = 0f;
    public Transform player;
    private Vector3 startPosition;

    void Awake()
    {
            if (player == null)
                player = GameObject.FindWithTag("Player").transform;
    }
    void Start()
    {
        startPosition = player.position; // 시작 위치 저장
    }
    void Update()
    {
        float dist = Vector3.Distance(player.position, startPosition);

        if (dist > warningRadius)
        {
            outOfBoundsTimer += Time.deltaTime;

            float ratio = Mathf.Clamp01(
                (dist - warningRadius) / (boundaryRadius - warningRadius)
            );

            SetVignetteAlpha(Mathf.Lerp(0f, maxAlpha, ratio));

            if (outOfBoundsTimer >= returnTime)
                OnBoundaryViolation();
        }
        else
        {
            outOfBoundsTimer = 0f;
            SetVignetteAlpha(Mathf.Lerp(vignetteImage.color.a, 0f, Time.deltaTime * 3f));
        }
    }

    void SetVignetteAlpha(float alpha)
    {
        Color c = vignetteImage.color;
        c.a = alpha;
        vignetteImage.color = c;
    }

    void OnBoundaryViolation()
    {
        Rigidbody rb = player.GetComponent<Rigidbody>();
        Debug.Log("[Boundary] 호출됨, rb=" + rb); // 추가

        if (rb == null)
        {
            Debug.LogError("[Boundary] Rigidbody를 못 찾음! GetComponentInChildren 필요할 수도");
            rb = player.GetComponentInChildren<Rigidbody>(); // 자식에 있을 경우 대비
        }

        if (rb != null)
        {
            rb.position = startPosition;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        outOfBoundsTimer = 0f;
    }
}