using UnityEngine;
using UnityEngine.UI;

public class LockOnTargetUI : MonoBehaviour
{
    [SerializeField] private Image frameImage;
    [SerializeField] private Image progressFill;

    private static readonly Color ColorCandidate = Color.green;
    private static readonly Color ColorLocked = Color.red;
    private static readonly Color VrColorCandidate =
        new Color(1f, 0.82f, 0.05f, 1f);
    private static readonly Color VrColorLocked = Color.white;

    private RectTransform _rect;
    private RectTransform _frameRect;
    private Text _vrStatusText;
    private Outline _vrFrameOutline;
    private bool _vrPresentationReady;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        if (frameImage != null)
        {
            _frameRect = frameImage.rectTransform;
        }
    }

    public void UpdateUI(Vector2 localPosition, float progress, bool isLocked)
    {
        _rect.anchoredPosition = localPosition;

        bool useVrPresentation = XRRuntimeManager.IsRunning;
        Color color = useVrPresentation
            ? (isLocked ? VrColorLocked : VrColorCandidate)
            : (isLocked ? ColorLocked : ColorCandidate);

        if (frameImage != null)
        {
            frameImage.color = color;
        }

        if (progressFill != null)
        {
            progressFill.fillAmount = isLocked ? 1f : progress;
            progressFill.color = color;
        }

        if (useVrPresentation)
        {
            UpdateVrPresentation(progress, isLocked);
        }
        else
        {
            RestoreDesktopPresentation();
        }
    }

    private void UpdateVrPresentation(float progress, bool isLocked)
    {
        EnsureVrPresentation();

        float pulse = 0.5f + 0.5f *
            Mathf.Sin(Time.unscaledTime * Mathf.PI * 3f);
        float scale = isLocked
            ? 1.35f
            : Mathf.Lerp(1.08f, 1.2f, pulse);
        _rect.localScale = Vector3.one * scale;

        if (_frameRect != null)
        {
            _frameRect.localRotation = isLocked
                ? Quaternion.identity
                : Quaternion.Euler(0f, 0f, -Time.unscaledTime * 45f);
        }

        if (_vrFrameOutline != null)
        {
            _vrFrameOutline.enabled = true;
            _vrFrameOutline.effectDistance = isLocked
                ? new Vector2(4f, -4f)
                : new Vector2(2f, -2f);
        }

        if (_vrStatusText != null)
        {
            _vrStatusText.gameObject.SetActive(true);
            _vrStatusText.text = isLocked
                ? "LOCKED"
                : $"LOCKING {Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%";
            _vrStatusText.fontSize = isLocked ? 30 : 24;
        }
    }

    private void EnsureVrPresentation()
    {
        if (_vrPresentationReady)
        {
            return;
        }

        _vrPresentationReady = true;

        if (frameImage != null)
        {
            _vrFrameOutline = frameImage.GetComponent<Outline>();
            if (_vrFrameOutline == null)
            {
                _vrFrameOutline = frameImage.gameObject.AddComponent<Outline>();
            }

            _vrFrameOutline.effectColor = Color.black;
            _vrFrameOutline.useGraphicAlpha = false;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            return;
        }

        GameObject labelObject = new GameObject(
            "VR Lock Status",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text),
            typeof(Outline));
        labelObject.layer = gameObject.layer;
        labelObject.transform.SetParent(transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, -74f);
        labelRect.sizeDelta = new Vector2(240f, 42f);

        _vrStatusText = labelObject.GetComponent<Text>();
        _vrStatusText.font = font;
        _vrStatusText.fontStyle = FontStyle.Bold;
        _vrStatusText.alignment = TextAnchor.MiddleCenter;
        _vrStatusText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _vrStatusText.verticalOverflow = VerticalWrapMode.Overflow;
        _vrStatusText.color = Color.white;
        _vrStatusText.raycastTarget = false;

        Outline textOutline = labelObject.GetComponent<Outline>();
        textOutline.effectColor = Color.black;
        textOutline.effectDistance = new Vector2(2f, -2f);
        textOutline.useGraphicAlpha = false;
    }

    private void RestoreDesktopPresentation()
    {
        _rect.localScale = Vector3.one;
        if (_frameRect != null)
        {
            _frameRect.localRotation = Quaternion.identity;
        }

        if (_vrFrameOutline != null)
        {
            _vrFrameOutline.enabled = false;
        }

        if (_vrStatusText != null)
        {
            _vrStatusText.gameObject.SetActive(false);
        }
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
}
