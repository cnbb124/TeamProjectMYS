using UnityEngine;

/// <summary>
/// 인벤토리 패널 전체 관리. InventoryPanel 오브젝트에 부착.
/// I 키로 패널 열고 닫기. 열려있는 동안 게임은 일시정지(A안).
/// </summary>
public class InventoryPanelUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    // InputManager가 읽기 전용으로 참조 — 인벤토리 열려있는 동안 커서 해제 + 게임플레이 입력 잠금용.
    public static bool IsOpen { get; private set; }

    private void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        IsOpen = false;
    }

    private void Update()
    {
        if (InputManager.Instance != null && InputManager.Instance.inventoryToggle)
            Toggle();
    }

    public void Toggle()
    {
        if (panelRoot == null) return;
        SetOpen(!panelRoot.activeSelf);
    }

    /// <summary>인벤토리 열기 (다른 UI에서 호출 가능).</summary>
    public void Open()  { SetOpen(true); }

    /// <summary>인벤토리 닫기 (다른 UI에서 호출 가능).</summary>
    public void Close() { SetOpen(false); }

    // 패널 표시 상태 변경 + 일시정지 요청/해제(A안). 상태가 실제로 바뀔 때만 Pause/Resume 호출.
    private void SetOpen(bool open)
    {
        if (panelRoot == null || panelRoot.activeSelf == open) return;
        panelRoot.SetActive(open);
        IsOpen = open;

        if (GameManager.Instance != null)
        {
            if (open) GameManager.Instance.PauseGame();
            else      GameManager.Instance.ResumeGame();
        }
    }
}
