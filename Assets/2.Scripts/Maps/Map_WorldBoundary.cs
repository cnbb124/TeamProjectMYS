using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class WorldBoundary : MonoBehaviour
{
    [Header("Boundary")]
    public float boundaryRadius = 5000f;
    public float warningRadius = 4500f;
    public float returnTime = 10f;

    [Header("Vignette UI")]
    public Image vignetteImage;
    public float maxAlpha = 0.8f;

    private float outOfBoundsTimer = 0f;
    private Transform player;
    private Vector3 startPosition;
    private bool initialized = false;

    void Update()
    {
        if (!initialized)
        {
            TryFindLocalPlayer();
            return;
        }

        float dist = Vector3.Distance(player.position, startPosition);
        if (dist > warningRadius)
        {
            outOfBoundsTimer += Time.deltaTime;
            float ratio = Mathf.Clamp01((dist - warningRadius) / (boundaryRadius - warningRadius));
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

    void TryFindLocalPlayer()
    {
        // 씬에 있는 PhotonView 중 로컬 소유(IsMine)인 것을 찾음
        PhotonView[] views = FindObjectsOfType<PhotonView>();
        foreach (var view in views)
        {
            if (view.IsMine && view.CompareTag("Player"))
            {
                player = view.transform;
                startPosition = player.position;
                initialized = true;
                Debug.Log("[Boundary] 로컬 플레이어 참조 확보: " + player.name);
                break;
            }
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
        if (rb == null)
            rb = player.GetComponentInChildren<Rigidbody>();

        if (rb != null)
        {
            rb.position = startPosition;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        outOfBoundsTimer = 0f;
    }
}