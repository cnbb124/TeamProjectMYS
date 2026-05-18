using UnityEngine;
using UnityEngine.UI;

public class PlaneNodeButton : MonoBehaviour
{
    [Header("References")]
    public Image iconImage;
    public Image borderImage;
    public Image lockOverlay;
    public Text labelText;
    public Button button;

    [Header("Visual States")]
    public Color normalColor   = new(0.2f, 0.6f, 1f, 1f);
    public Color selectedColor = new(1f, 0.85f, 0.2f, 1f);
    public Color lockedColor   = new(0.35f, 0.35f, 0.35f, 0.6f);

    public PlaneNodeData Data { get; private set; }

    public void Initialize(PlaneNodeData data)
    {
        Data = data;
        if (iconImage && data.icon) iconImage.sprite = data.icon;
        if (labelText) labelText.text = data.isUnlocked ? data.unitName : $"{data.mrpCost:N0}";
        if (lockOverlay) lockOverlay.gameObject.SetActive(!data.isUnlocked);

        SetSelected(false);
        button.onClick.AddListener(() => HangarUIManager.Instance.SelectNode(this));
    }

    public void SetSelected(bool selected)
    {
        if (!borderImage) return;
        borderImage.color = selected ? selectedColor
                          : Data.isUnlocked ? normalColor
                          : lockedColor;
    }

    public void RefreshLockState()
    {
        if (lockOverlay) lockOverlay.gameObject.SetActive(!Data.isUnlocked);
        if (labelText) labelText.text = Data.isUnlocked ? Data.unitName : $"{Data.mrpCost:N0}";
        SetSelected(false);
    }
}