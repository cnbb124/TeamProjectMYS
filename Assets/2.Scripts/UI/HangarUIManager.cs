using UnityEngine;
using UnityEngine.UI;

public class HangarUIManager : MonoBehaviour
{
    public static HangarUIManager Instance { get; private set; }

    [Header("패널")]
    public GameObject hangarCanvas;

    [Header("레퍼런스")]
    public HangarTreeManager treeManager;
    public StatPanelUI statPanel;

    [Header("플레이어 자원")]
    public int currentMRP = 989221079;
    public Text mrpDisplayText;

    private PlaneNodeButton _selectedButton;

    // ───────────────────────────────
    //  초기화
    // ───────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (hangarCanvas != null)
            hangarCanvas.SetActive(false);

        UpdateMRPDisplay();
    }

    // ───────────────────────────────
    //  격납고 열기 / 닫기
    // ───────────────────────────────

    // 3D 기체 클릭 시 호출
    public void OpenHangar(PlaneNodeData data)
    {
        if (hangarCanvas != null)
            hangarCanvas.SetActive(true);

        treeManager.rootNode = data;
        treeManager.BuildTree();
        statPanel.Refresh(data);
        _selectedButton = null;
    }

    // 닫기 버튼에 연결
    public void CloseHangar()
    {
        if (hangarCanvas != null)
            hangarCanvas.SetActive(false);

        _selectedButton = null;
    }

    // ───────────────────────────────
    //  노드 선택
    // ───────────────────────────────

    // PlaneNodeButton 클릭 시 호출
    public void SelectNode(PlaneNodeButton button)
    {
        if (_selectedButton != null)
            _selectedButton.SetSelected(false);

        _selectedButton = button;
        _selectedButton.SetSelected(true);

        statPanel.Refresh(button.Data);
    }

    // ───────────────────────────────
    //  잠금 해제
    // ───────────────────────────────

    // 잠금해제 버튼에 연결
    public void TryUnlockSelected()
    {
        if (_selectedButton == null)
        {
            Debug.Log("선택된 노드 없음");
            return;
        }

        PlaneNodeData data = _selectedButton.Data;

        if (data.isUnlocked)
        {
            Debug.Log("이미 해금된 유닛");
            return;
        }

        if (currentMRP < data.mrpCost)
        {
            Debug.Log("MRP 부족");
            return;
        }

        currentMRP -= data.mrpCost;
        data.isUnlocked = true;

        _selectedButton.RefreshLockState();
        statPanel.Refresh(data);
        UpdateMRPDisplay();
    }

    // ───────────────────────────────
    //  MRP 표시 갱신
    // ───────────────────────────────
    void UpdateMRPDisplay()
    {
        if (mrpDisplayText != null)
            mrpDisplayText.text = $"CURRENT MRP  {currentMRP:N0}";
    }
}