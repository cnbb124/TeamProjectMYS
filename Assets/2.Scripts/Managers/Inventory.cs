using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ConsumableStack
{
    public ConsumableData data;
    public int count;
}

public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    [Header("Parts")]
    public List<PartData> parts = new List<PartData>();

    [Header("Consumables")]
    public List<ConsumableStack> consumables = new List<ConsumableStack>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
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
