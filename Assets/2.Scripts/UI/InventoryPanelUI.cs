using UnityEngine;

/// <summary>
/// 인벤토리 패널 전체 관리. InventoryPanel 오브젝트에 부착.
/// I 키로 패널 열고 닫기.
/// </summary>
public class InventoryPanelUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    // InputManager가 읽기 전용으로 참조 — 인벤토리 열려있는 동안 마우스 커서 해제용.
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
        panelRoot.SetActive(!panelRoot.activeSelf);
        IsOpen = panelRoot.activeSelf;
    }
}
