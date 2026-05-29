using UnityEngine;
using UnityEngine.UI;

public class LockOnTargetUI : MonoBehaviour
{
    [SerializeField] private Image frameImage;    // 브라켓 테두리
    [SerializeField] private Image progressFill;  // 락온 진행률 (원형 Fill)

    private static readonly Color ColorCandidate = Color.green; // 락온 중
    private static readonly Color ColorLocked    = Color.red;   // 락온 완료

    private RectTransform _rect;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    public void UpdateUI(Vector3 screenPos, float progress, bool isLocked)
    {
        // 카메라 뒤에 있으면 숨기기
        if (screenPos.z < 0f) { Hide(); return; }

        _rect.position = new Vector2(screenPos.x, screenPos.y);

        Color color = isLocked ? ColorLocked : ColorCandidate;

        if (frameImage != null)    frameImage.color = color;
        if (progressFill != null)
        {
            progressFill.fillAmount = isLocked ? 1f : progress;
            progressFill.color = color;
        }
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
}