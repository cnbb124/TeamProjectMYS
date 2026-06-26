using UnityEngine;

public class QuickSlot : MonoBehaviour
{
    private const int SLOT_COUNT = 3;

    [Header("¼Ò¸ðÇ° Äü½½·Ô")]
    public ConsumableData[] slots = new ConsumableData[SLOT_COUNT];

    private float[] _cooldownTimers = new float[SLOT_COUNT];

    private Unit _unit;

    private void Awake()
    {
        _unit = GetComponent<Unit>();
    }

    private void Update()
    {
        if (GameManager.Instance != null &&
            (GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver))
        {
            return;
        }

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (_cooldownTimers[i] > 0f)
            {
                _cooldownTimers[i] -= Time.deltaTime;
                if (_cooldownTimers[i] < 0f)
                {
                    _cooldownTimers[i] = 0f;
                }
            }
        }
    }

    public void AssignSlot(int slotIndex, ConsumableData data)
    {
        if (!IsValidIndex(slotIndex))
        {
            return;
        }
        slots[slotIndex] = data;
        _cooldownTimers[slotIndex] = 0f;
    }

    public void UseSlot(int slotIndex)
    {
        if (!IsValidIndex(slotIndex))
        {
            return;
        }
        if (slots[slotIndex] == null)
        {
            return;
        }
        if (_cooldownTimers[slotIndex] > 0f)
        {
            return;
        }
        if (InventoryManager.Instance == null)
        {
            return;
        }
        if (!InventoryManager.Instance.ConsumeOne(slots[slotIndex]))
        {
            return;
        }

        ApplyEffect(slots[slotIndex]);
        _cooldownTimers[slotIndex] = slots[slotIndex].cooldown;

        if (InventoryManager.Instance.GetCount(slots[slotIndex]) <= 0)
        {
            slots[slotIndex] = null;
        }
    }

    public float GetCooldownRatio(int slotIndex)
    {
        if (!IsValidIndex(slotIndex) || slots[slotIndex] == null)
        {
            return 0f;
        }
        if (slots[slotIndex].cooldown <= 0f)
        {
            return 0f;
        }
        return _cooldownTimers[slotIndex] / slots[slotIndex].cooldown;
    }

    public float GetCooldownRemaining(int slotIndex)
    {
        if (!IsValidIndex(slotIndex))
        {
            return 0f;
        }
        return _cooldownTimers[slotIndex];
    }

    private void ApplyEffect(ConsumableData data)
    {
        if (_unit == null)
        {
            return;
        }

        foreach (ConsumableEffect effect in data.effects)
        {
            switch (effect.effectType)
            {
                case CONSUMABLE_TYPE.HP_RESTORE:
                    _unit.curHpRemaining = Mathf.Min(
                        _unit.curHpRemaining + (int)effect.value,
                        _unit.maxHpRemaining
                    );
                    break;
                case CONSUMABLE_TYPE.SHIELD_RESTORE:
                    _unit.curShieldRemaining = Mathf.Min(
                        _unit.curShieldRemaining + (int)effect.value,
                        _unit.maxShieldCapacity
                    );
                    break;
                case CONSUMABLE_TYPE.BOOST_RESTORE:
                    _unit.curBoostRemaining = Mathf.Min(
                        _unit.curBoostRemaining + effect.value,
                        _unit.maxBoostCapacity
                    );
                    break;
            }
        }
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < SLOT_COUNT;
    }
}
