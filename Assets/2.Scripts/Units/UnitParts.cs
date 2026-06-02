using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PartSlotEntry
{
    public PART_TYPE slotType;
    public PartData equippedPart;
}

public class UnitParts : MonoBehaviour
{
    private Unit _unit;

    [Header("<size=14>파츠 슬롯 (출력용, 스크립트에서 자동입력)</size>")]
    public List<PartSlotEntry> partSlots = new List<PartSlotEntry>();

    private void Awake()
    {
        //
        _unit = GetComponent<Unit>();

        if (partSlots.Count == 0)
        {
            partSlots.Add(new PartSlotEntry { slotType = PART_TYPE.ENGINE });
            partSlots.Add(new PartSlotEntry { slotType = PART_TYPE.FRAME });
            partSlots.Add(new PartSlotEntry { slotType = PART_TYPE.ARMOR });
            partSlots.Add(new PartSlotEntry { slotType = PART_TYPE.LAUNCHER_MISSILE });
            partSlots.Add(new PartSlotEntry { slotType = PART_TYPE.LAUNCHER_BULLET });
            partSlots.Add(new PartSlotEntry { slotType = PART_TYPE.THRUSTER });
            //필요시 계속 추가
			//partSlots.Add(new PartSlotEntry { slotType = PART_TYPE. })

		}
    }

    private void Start()
    {
        // 인스펙터에 미리 세팅된 파츠 스탯 적용
        foreach (PartSlotEntry slot in partSlots)
        {
            if (slot.equippedPart != null)
            {
                ApplyStatBonuses(slot.equippedPart, 1);
            }
        }
    }

    /// <summary>
    /// 지정 슬롯에 파츠 장착. 기존 파츠는 자동 해제.
    /// </summary>
    public void Equip(PART_TYPE slotType, PartData newPart)
    {
        PartSlotEntry slot = GetSlot(slotType);
        if (slot == null)
        {
            Debug.LogWarning("[UnitParts] 슬롯 없음: " + slotType);
            return;
        }

        if (slot.equippedPart != null)
        {
            ApplyStatBonuses(slot.equippedPart, -1);
        }

        slot.equippedPart = newPart;

        if (newPart != null)
        {
            ApplyStatBonuses(newPart, 1);
        }
    }

    public void Unequip(PART_TYPE slotType)
    {
        Equip(slotType, null);
    }

    public PartData GetEquipped(PART_TYPE slotType)
    {
        PartSlotEntry slot = GetSlot(slotType);
        if (slot == null)
        {
            return null;
        }
        return slot.equippedPart;
    }

    private PartSlotEntry GetSlot(PART_TYPE slotType)
    {
        foreach (PartSlotEntry slot in partSlots)
        {
            if (slot.slotType == slotType)
            {
                return slot;
            }
        }
        return null;
    }

    // multiplier: +1 = 장착, -1 = 해제
    private void ApplyStatBonuses(PartData part, int multiplier)
    {
        if (_unit == null)
        {
            return;
        }

        foreach (PartStatBonus bonus in part.statBonuses)
        {
            float val = bonus.value * multiplier;
            ApplySingleBonus(bonus.statType, val);
        }
    }

    private void ApplySingleBonus(STAT_TYPE statType, float val)
    {
        switch (statType)
        {
            case STAT_TYPE.MAX_HP:
                _unit.maxHpRemaining += (int)val;
                break;
            case STAT_TYPE.MAX_SHIELD:
                _unit.maxShieldCapacity += (int)val;
                break;
            case STAT_TYPE.MAX_ARMOR:
                _unit.maxArmor += (int)val;
                break;
            case STAT_TYPE.DEFENSE:
                _unit.defense += (int)val;
                break;
            case STAT_TYPE.BASE_MOVE_SPEED:
                _unit.baseMoveSpeed += val;
                break;
            case STAT_TYPE.BOOST_SPEED:
                _unit.boostSpeed += val;
                break;
            case STAT_TYPE.MAX_SPEED:
                _unit.maxSpeed += val;
                break;
            case STAT_TYPE.MAX_BOOST:
                _unit.maxBoostCapacity += val;
                break;
            case STAT_TYPE.CRI_CHANCE:
                _unit.criChance += val;
                break;
            case STAT_TYPE.CRI_DAMAGE_MULT:
                _unit.criDamageMultiplier += val;
                break;
        }
    }
}
