using UnityEngine.UI;
using UnityEngine;

public class HangarUIManager : MonoBehaviour
{
    public static HangarUIManager Instance { get; private set; }

    [Header("References")]
    public HangarTreeManager treeManager;
    public StatPanelUI       statPanel;

    [Header("Player Resources")]
    public int currentMRP = 989_221_079;
    public Text mrpDisplayText;

    private PlaneNodeButton _selected;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start() => UpdateMRPDisplay();

    public void SelectNode(PlaneNodeButton btn)
    {
        _selected?.SetSelected(false);
        _selected = btn;
        _selected.SetSelected(true);
        statPanel?.Refresh(btn.Data);
    }

    // UI 버튼(잠금해제)에 연결
    public void TryUnlockSelected()
    {
        if (!_selected) return;
        var data = _selected.Data;

        if (data.isUnlocked)      { Debug.Log("Already unlocked."); return; }
        if (currentMRP < data.mrpCost) { Debug.Log("Not enough MRP."); return; }

        currentMRP   -= data.mrpCost;
        data.isUnlocked = true;

        _selected.RefreshLockState();
        UpdateMRPDisplay();
        statPanel?.Refresh(data);
    }

    void UpdateMRPDisplay()
    {
        if (mrpDisplayText)
            mrpDisplayText.text = $"CURRENT MRP  {currentMRP:N0}";
    }
}