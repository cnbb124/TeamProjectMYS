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

    private void OnEnable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= Refresh;
    }

    /// <summary>현재 골드로 텍스트 갱신.</summary>
    public void Refresh()
    {
        if (goldText == null || InventoryManager.Instance == null) return;

        int gold = InventoryManager.Instance.gold;
        string num = useThousandComma ? gold.ToString("N0") : gold.ToString();
        goldText.text = prefix + num;
    }
}
