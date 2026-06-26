using UnityEngine;

// PartData, ConsumableData 등 모든 아이템 공통 베이스.
// 인벤토리에서 ItemStack<ItemData>로 통합 관리.
public abstract class ItemData : ScriptableObject
{
    [Header("ID")]
    [Tooltip("고유 ID. 저장/로드 및 네트워크 전송에 사용. enum_IDs.cs 범위 안에서 설정.")]
    public ITEM_ID id;

    [Header("Info")]
    public string itemName;
    public Sprite icon;
    [TextArea]
    public string description;
    public int price;

    [Header("카테고리")]
    [Tooltip("인벤토리 UI 탭 분류. InventoryManager.GetAllOfCategory()로 필터링.")]
    public ITEM_CATEGORY category;
}
