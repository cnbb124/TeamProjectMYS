using UnityEngine;

public class QuickSlot : MonoBehaviour
{
    public static QuickSlot Instance { get; private set; }

    private const int SLOT_COUNT = 3;

    [Header("Slots (0~2)")]
    public ConsumableData[] slots = new ConsumableData[SLOT_COUNT];

    private float[] _cooldownTimers = new float[SLOT_COUNT];

    private Unit _unit;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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

    // Inventory에서 슬롯에 소모품 할당
    public void AssignSlot(int slotIndex, ConsumableData data)
    {
        if (!IsValidIndex(slotIndex))
        {
            return;
        }
        slots[slotIndex] = data;
        _cooldownTimers[slotIndex] = 0f;
    }

    // 슬롯 사용 (InputManager에서 키 입력 시 호출)
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

        // 인벤토리 수량 소진 시 슬롯 비우기
        if (InventoryManager.Instance.GetCount(slots[slotIndex]) <= 0)
        {
            slots[slotIndex] = null;
        }
    }

    // HUD용 - 쿨다운 진행률 (0=사용가능, 1=쿨다운 시작 직후)
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

    // HUD용 - 남은 쿨다운 초
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

        switch (data.consumableType)
        {
            case CONSUMABLE_TYPE.HP_RESTORE:
                _unit.curHpRemaining = Mathf.Min(
                    _unit.curHpRemaining + (int)data.value,
                    _unit.maxHpRemaining
                );
                break;
            case CONSUMABLE_TYPE.SHIELD_RESTORE:
                _unit.curShieldRemaining = Mathf.Min(
                    _unit.curShieldRemaining + (int)data.value,
                    _unit.maxShieldCapacity
                );
                break;
            case CONSUMABLE_TYPE.BOOST_RESTORE:
                _unit.curBoostRemaining = Mathf.Min(
                    _unit.curBoostRemaining + data.value,
                    _unit.maxBoostCapacity
                );
                break;
        }
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < SLOT_COUNT;
    }
}
