using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ConsumableStack
{
    public ConsumableData data;
    public int count;
}

public class InventoryManager : MonoBehaviour
{
    private static InventoryManager instance;
    public static InventoryManager Instance
    {
        get
        {
            if(instance==null)
            {
                instance = FindObjectOfType<InventoryManager>();
                if(instance==null)
                {
                    Debug.Log("씬에 InventoryManager 누락! 하이어라키에 사운드매니저 필요");
                }
            }
            return instance;
        }
       
        
    }


    [Header("보유 골드")]
    public int gold;

    [Header("보유 파츠 목록")]
    public List<PartData> parts = new List<PartData>();

    [Header("보유 소모품 목록")]
    public List<ConsumableStack> consumables = new List<ConsumableStack>();

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

    // ==================== Parts ====================

    public void AddPart(PartData part)
    {
        if (part == null)
        {
            return;
        }
        parts.Add(part);
    }

    public bool RemovePart(PartData part)
    {
        return parts.Remove(part);
    }

    // ==================== Consumables ====================

    public void AddConsumable(ConsumableData data, int amount = 1)
    {
        if (data == null || amount <= 0)
        {
            return;
        }

        ConsumableStack stack = FindStack(data);
        if (stack != null)
        {
            stack.count += amount;
        }
        else
        {
            consumables.Add(new ConsumableStack { data = data, count = amount });
        }
    }

    // 1개 소모. 성공 시 true 반환.
    public bool ConsumeOne(ConsumableData data)
    {
        ConsumableStack stack = FindStack(data);
        if (stack == null || stack.count <= 0)
        {
            return false;
        }

        stack.count--;
        if (stack.count <= 0)
        {
            consumables.Remove(stack);
        }
        return true;
    }

    public int GetCount(ConsumableData data)
    {
        ConsumableStack stack = FindStack(data);
        if (stack == null)
        {
            return 0;
        }
        return stack.count;
    }

    // QuickSlot 슬롯에 소모품 할당 (Inventory -> QuickSlot 단방향)
    public void AssignToQuickSlot(ConsumableData data, int slotIndex)
    {
        if (QuickSlot.Instance == null)
        {
            Debug.LogWarning("[Inventory] QuickSlot.Instance is null");
            return;
        }
        QuickSlot.Instance.AssignSlot(slotIndex, data);
    }

    private ConsumableStack FindStack(ConsumableData data)
    {
        foreach (ConsumableStack stack in consumables)
        {
            if (stack.data == data)
            {
                return stack;
            }
        }
        return null;
    }
}
