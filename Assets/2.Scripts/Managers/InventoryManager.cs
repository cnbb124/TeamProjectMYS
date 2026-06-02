using System.Collections.Generic;
using UnityEngine;

// ItemData(PartData, ConsumableData 등)와 수량을 묶은 인벤토리 슬롯 단위.
[System.Serializable]
public class ItemStack
{
    public ItemData data;
    public int count;
}

public class InventoryManager : MonoBehaviour
{
    private static InventoryManager instance;
    public static InventoryManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<InventoryManager>();
                if (instance == null)
                {
                    Debug.Log("씬에 InventoryManager 누락! 하이어라키에 추가 필요");
                }
            }
            return instance;
        }
    }

    [Header("보유 골드")]
    public int gold;

    [Header("보유 아이템 목록 (파츠 / 소모품 / 재료 등)")]
    public List<ItemStack> items = new List<ItemStack>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Debug.LogWarning("중복된 InventoryManager 발견. 파괴 후 실행");
            Destroy(gameObject);
        }
    }

    // ==================== Gold ====================

    /// <summary>골드 획득. 적 처치 드랍 등 보상 지급 시 호출.</summary>
    public void AddGold(int amount)
    {
        gold += Mathf.Max(0, amount);
        Debug.Log($"[Inventory] 골드 획득: +{amount} / 보유: {gold}");
    }

    /// <summary>골드 소모. 부족 시 false 반환. 상점 구매 등에서 호출.</summary>
    public bool SpendGold(int amount)
    {
        if (gold < amount)
        {
            Debug.LogWarning($"[Inventory] 골드 부족. 필요: {amount} / 보유: {gold}");
            return false;
        }
        gold -= amount;
        return true;
    }

    // ==================== Item (공통) ====================

    /// <summary>아이템 추가. 이미 있으면 수량 증가.</summary>
    public void AddItem(ItemData data, int amount = 1)
    {
        if (data == null || amount <= 0)
        {
            return;
        }

        ItemStack stack = FindStack(data);
        if (stack != null)
        {
            stack.count += amount;
        }
        else
        {
            items.Add(new ItemStack { data = data, count = amount });
        }
    }

    /// <summary>아이템 수량 감소. 수량 0이면 목록에서 제거. 부족 시 false 반환.</summary>
    public bool RemoveItem(ItemData data, int amount = 1)
    {
        ItemStack stack = FindStack(data);
        if (stack == null || stack.count < amount)
        {
            return false;
        }

        stack.count -= amount;
        if (stack.count <= 0)
        {
            items.Remove(stack);
        }
        return true;
    }

    /// <summary>보유 수량 반환. 없으면 0.</summary>
    public int GetCount(ItemData data)
    {
        ItemStack stack = FindStack(data);
        if (stack == null)
        {
            return 0;
        }
        return stack.count;
    }

    /// <summary>특정 타입의 아이템만 필터링해서 반환. GetAllOfType&lt;PartData&gt;() 등.</summary>
    public List<ItemStack> GetAllOfType<T>() where T : ItemData
    {
        List<ItemStack> result = new List<ItemStack>();
        foreach (ItemStack stack in items)
        {
            if (stack.data is T)
            {
                result.Add(stack);
            }
        }
        return result;
    }

    // ==================== 편의 래퍼 ====================

    /// <summary>파츠 1개 추가.</summary>
    public void AddPart(PartData part)
    {
        AddItem(part, 1);
    }

    /// <summary>파츠 제거. 성공 시 true.</summary>
    public bool RemovePart(PartData part)
    {
        return RemoveItem(part, 1);
    }

    /// <summary>소모품 추가.</summary>
    public void AddConsumable(ConsumableData data, int amount = 1)
    {
        AddItem(data, amount);
    }

    /// <summary>소모품 1개 소모. 성공 시 true. QuickSlot.UseSlot()에서 호출.</summary>
    public bool ConsumeOne(ConsumableData data)
    {
        return RemoveItem(data, 1);
    }

    // ==================== QuickSlot 연결 ====================

    /// <summary>소모품을 퀵슬롯에 할당. Inventory -> QuickSlot 단방향.</summary>
    public void AssignToQuickSlot(ItemData data, int slotIndex)
    {
        ConsumableData consumable = data as ConsumableData;
        if (consumable == null)
        {
            Debug.LogWarning("[Inventory] 소모품만 퀵슬롯에 등록 가능.");
            return;
        }
        if (QuickSlot.Instance == null)
        {
            Debug.LogWarning("[Inventory] QuickSlot.Instance is null");
            return;
        }
        QuickSlot.Instance.AssignSlot(slotIndex, consumable);
    }

    // ==================== 내부 ====================

    private ItemStack FindStack(ItemData data)
    {
        foreach (ItemStack stack in items)
        {
            if (stack.data == data)
            {
                return stack;
            }
        }
        return null;
    }
}
