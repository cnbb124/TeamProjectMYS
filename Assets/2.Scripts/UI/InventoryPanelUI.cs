using UnityEngine;

/// <summary>
/// 인벤토리 패널 전체 관리. InventoryPanel 오브젝트에 부착.
/// I 키로 패널 열고 닫기.
/// </summary>
public class InventoryPanelUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private KeyCode    toggleKey = KeyCode.I;

    private void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
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
    }
}
