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

    private static readonly Color CbColorCandidate =
        new Color(1f, 0.82f, 0.05f, 1f);
    private static readonly Color CbColorLocked = Color.white;
    private static readonly Color CbOutlineColor =
        new Color(0f, 0f, 0f, 0.85f);
    private const float CbLockedScale = 1.4f;
    private const float CbCandidateScale = 1f;
    private const float CbLockedOutline = 4f;
    private const float CbCandidateOutline = 2f;

    private RectTransform _rect;
    private RectTransform _frameRect;
    private Text _vrStatusText;
    private Outline _frameOutline;
    private bool _vrStatusTextReady;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        if (frameImage == null)
        {
            return;
        }

        _frameRect = frameImage.rectTransform;
        _frameOutline = frameImage.GetComponent<Outline>();
        if (_frameOutline == null)
        {
            _frameOutline = frameImage.gameObject.AddComponent<Outline>();
        }

        _frameOutline.useGraphicAlpha = false;
        _frameOutline.enabled = false;
    }

    public void UpdateUI(float progress, bool isLocked)
    {
        bool useVrPresentation = XRRuntimeManager.IsRunning;
        bool colorBlind = ColorBlindSettings.Enabled;

        Color normalColor;
        if (useVrPresentation)
        {
            normalColor = isLocked ? VrColorLocked : VrColorCandidate;
        }
        else
        {
            normalColor = isLocked ? ColorLocked : ColorCandidate;
        }

        Color accessibleColor = isLocked ? CbColorLocked : CbColorCandidate;
        Color color = ColorBlindSettings.Pick(normalColor, accessibleColor);

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
            UpdateVrStatusText(progress, isLocked);
        }
        else
        {
            HideVrStatusText();
        }

        ApplyScale(isLocked, useVrPresentation, colorBlind);
        ApplyFrameRotation(isLocked, useVrPresentation);
        ApplyOutline(isLocked, useVrPresentation, colorBlind);
    }

    private void ApplyScale(bool isLocked, bool useVrPresentation, bool colorBlind)
    {
        float scale = 1f;

        if (useVrPresentation)
        {
            float pulse = 0.5f + 0.5f *
                Mathf.Sin(Time.unscaledTime * Mathf.PI * 3f);
            scale = isLocked ? 1.35f : Mathf.Lerp(1.08f, 1.2f, pulse);
        }
        else if (colorBlind)
        {
            scale = isLocked ? CbLockedScale : CbCandidateScale;
        }

        _rect.localScale = Vector3.one * scale;
    }

    private void ApplyFrameRotation(bool isLocked, bool useVrPresentation)
    {
        if (_frameRect == null)
        {
            return;
        }

        if (useVrPresentation && !isLocked)
        {
            _frameRect.localRotation =
                Quaternion.Euler(0f, 0f, -Time.unscaledTime * 45f);
        }
        else
        {
            _frameRect.localRotation = Quaternion.identity;
        }
    }

    private void ApplyOutline(bool isLocked, bool useVrPresentation, bool colorBlind)
    {
        if (_frameOutline == null)
        {
            return;
        }

        if (!colorBlind && !useVrPresentation)
        {
            _frameOutline.enabled = false;
            return;
        }

        _frameOutline.enabled = true;

        if (colorBlind)
        {
            _frameOutline.effectColor = CbOutlineColor;
            float thickness = isLocked ? CbLockedOutline : CbCandidateOutline;
            _frameOutline.effectDistance = new Vector2(thickness, thickness);
        }
        else
        {
            _frameOutline.effectColor = Color.black;
            _frameOutline.effectDistance = isLocked
                ? new Vector2(4f, -4f)
                : new Vector2(2f, -2f);
        }
    }

    private void UpdateVrStatusText(float progress, bool isLocked)
    {
        EnsureVrStatusText();
        if (_vrStatusText == null)
        {
            return;
        }

        _vrStatusText.gameObject.SetActive(true);
        _vrStatusText.text = isLocked
            ? "LOCKED"
            : $"LOCKING {Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%";
        _vrStatusText.fontSize = isLocked ? 30 : 24;
    }

    private void HideVrStatusText()
    {
        if (_vrStatusText != null)
        {
            _vrStatusText.gameObject.SetActive(false);
        }
    }

    private void EnsureVrStatusText()
    {
        if (_vrStatusTextReady)
        {
            return;
        }

        _vrStatusTextReady = true;

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

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
}
