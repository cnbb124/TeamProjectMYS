/*
 * [GoldDisplayUI]
 * 보유 골드를 텍스트로 표시. InventoryManager.gold를 읽고, 변동 시 자동 갱신.
 *
 * [부착] 골드를 표시할 TMP_Text가 있는 오브젝트(또는 그 부모)에 부착.
 *
 * [인스펙터 연결]
 * - goldText : 골드 수치 텍스트 (TMP)
 * - prefix   : 앞에 붙일 문구 (예: "", "₡ ", "Credits ")
 * - useThousandComma : 천 단위 콤마 (1,250)
 *
 * [동작] OnEnable에서 1회 갱신 + InventoryManager.OnInventoryChanged 구독해 구매/판매 시 자동 갱신.
 */

using UnityEngine;
using TMPro;

public class GoldDisplayUI : MonoBehaviour
{
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private string   prefix = "";
    [SerializeField] private bool      useThousandComma = true;

    // InventoryManager 참조. Start에서 1회만 잡고 OnEnable/OnDisable은 이 필드만 씀.
    private InventoryManager _inventoryManager;

    // 매니저 최초 취득은 Start에서만 — Awake/OnEnable에서 .Instance를 부르면 매니저 자신의 Awake보다
    // 먼저 instance를 선점해서, 매니저 Awake의 초기화가 통째로 스킵됨.
    private void Start()
    {
        _inventoryManager = InventoryManager.Instance;
        if (_inventoryManager != null)
            _inventoryManager.OnInventoryChanged += Refresh;
        Refresh();
    }

    private void OnEnable()
    {
        // 캐시된 것만 씀. 최초 1회는 아직 null이라 넘어가고 바로 뒤의 Start가 구독을 마무리함.
        if (_inventoryManager != null)
            _inventoryManager.OnInventoryChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (_inventoryManager != null)
            _inventoryManager.OnInventoryChanged -= Refresh;
    }

    /// <summary>현재 골드로 텍스트 갱신.</summary>
    public void Refresh()
    {
        if (goldText == null || _inventoryManager == null) return;

        int gold = _inventoryManager.gold;
        string num = useThousandComma ? gold.ToString("N0") : gold.ToString();
        goldText.text = prefix + num;
    }
}
