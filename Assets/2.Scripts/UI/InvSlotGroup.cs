using UnityEngine;

/// <summary>
/// 인벤토리 슬롯 그룹 관리자.
/// WeaponSlotUI / ItemSlotUI 부모 오브젝트에 부착.
///
/// [인스펙터 세팅]
///   slots        : 자식 슬롯을 순서대로 배열에 연결
///   defaultItems : 시작 시 채울 ItemData SO 배열 (없으면 빈 슬롯)
/// </summary>
public class InvSlotGroup : MonoBehaviour
{
    [Header("슬롯 오브젝트 배열 (순서대로 연결)")]
    [SerializeField] private InvSlot[] slots;

    [Header("기본 아이템 (ItemData SO — slots 순서와 동일)")]
    [SerializeField] private ItemData[] defaultItems;

    private void Start()
    {
        Populate();
    }

    // ── 초기 아이템 세팅 ──────────────────────────────────────

    private void Populate()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            ItemData data = (defaultItems != null && i < defaultItems.Length)
                ? defaultItems[i]
                : null;

            slots[i].SetItem(data);
        }
    }

    // ── 외부 접근용 ───────────────────────────────────────────

    public ItemData GetItem(int index)
    {
        if (slots == null || index < 0 || index >= slots.Length) return null;
        return slots[index]?.Item;
    }

    public void SetItem(int index, ItemData data)
    {
        if (slots == null || index < 0 || index >= slots.Length) return;
        slots[index]?.SetItem(data);
    }

    public void RefreshAll()
    {
        if (slots == null) return;
        foreach (InvSlot slot in slots)
            slot?.Refresh();
    }

    public int SlotCount => slots != null ? slots.Length : 0;
}
