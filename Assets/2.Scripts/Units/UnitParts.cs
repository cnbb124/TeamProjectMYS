using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PartSlotEntry
{
    public PART_TYPE slotType;
    public PartData equippedPart;
    // 장착 시 Instantiate된 파츠 오브젝트 (런타임 전용, 직렬화 제외)
    [System.NonSerialized] public GameObject spawnedInstance;
}

public class UnitParts : MonoBehaviour
{
    private Unit _unit;
    private WeaponSystem _weaponSystem;

    [Header("<size=14>파츠 슬롯 (출력용, 스크립트에서 자동입력)</size>")]
    public List<PartSlotEntry> partSlots = new List<PartSlotEntry>();

    private void Awake()
    {
        _unit = GetComponent<Unit>();
        _weaponSystem = GetComponent<WeaponSystem>();

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
        // 인스펙터에 미리 세팅된 파츠 스탯 + 프리팹 적용
        foreach (PartSlotEntry slot in partSlots)
        {
            if (slot.equippedPart == null)
            {
                continue;
            }
            SpawnPartPrefab(slot);
            ApplyStatBonuses(slot.equippedPart, 1);
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
            DestroyPartPrefab(slot);
            ApplyStatBonuses(slot.equippedPart, -1);
        }

        slot.equippedPart = newPart;

        if (newPart != null)
        {
            SpawnPartPrefab(slot);
            ApplyStatBonuses(newPart, 1);
        }
    }

    /// <summary>
    /// 인덱스로 직접 슬롯 지정해 장착. LAUNCHER 다중 슬롯 구분 시 사용.
    /// </summary>
    public void EquipAt(int slotIndex, PartData newPart)
    {
        if (slotIndex < 0 || slotIndex >= partSlots.Count)
        {
            Debug.LogWarning("[UnitParts] 슬롯 인덱스 초과: " + slotIndex);
            return;
        }
        PartSlotEntry slot = partSlots[slotIndex];

        if (slot.equippedPart != null)
        {
            DestroyPartPrefab(slot);
            ApplyStatBonuses(slot.equippedPart, -1);
        }

        slot.equippedPart = newPart;

        if (newPart != null)
        {
            SpawnPartPrefab(slot);
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

    /// <summary>
    /// LAUNCHER 슬롯을 partSlots에 추가. 런처 베이가 늘어날 때 호출.
    /// </summary>
    public void AddLauncherSlot(PART_TYPE launcherType)
    {
        partSlots.Add(new PartSlotEntry { slotType = launcherType });
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


    // ================== [파츠 프리팹 처리] ==================

    /// <summary>
    /// partData.partPrefab이 있으면 Instantiate 후 WeaponFirePos 컴포넌트를 탐색해
    /// WeaponSystem에 발사 위치 등록. ENGINE/FRAME/ARMOR처럼 프리팹 없는 파츠는 스킵.
    /// </summary>
    private void SpawnPartPrefab(PartSlotEntry slot)
    {
        if (slot.equippedPart == null || slot.equippedPart.partPrefab == null)
        {
            return;
        }
        if (_weaponSystem == null)
        {
            return;
        }

        slot.spawnedInstance = Instantiate(slot.equippedPart.partPrefab, transform);

        // WeaponFirePos 마커가 붙은 자식을 전부 찾아 WeaponSystem에 등록
        WeaponFirePos[] firePoses = slot.spawnedInstance.GetComponentsInChildren<WeaponFirePos>();
        foreach (WeaponFirePos wfp in firePoses)
        {
            _weaponSystem.RegisterFirePos(wfp.posType, wfp.transform);
        }
    }

    /// <summary>
    /// 파츠 프리팹 파괴 및 WeaponSystem에서 발사 위치 제거.
    /// </summary>
    private void DestroyPartPrefab(PartSlotEntry slot)
    {
        if (slot.spawnedInstance == null || _weaponSystem == null)
        {
            return;
        }

        WeaponFirePos[] firePoses = slot.spawnedInstance.GetComponentsInChildren<WeaponFirePos>();
        foreach (WeaponFirePos wfp in firePoses)
        {
            _weaponSystem.UnregisterFirePos(wfp.posType, wfp.transform);
        }

        Destroy(slot.spawnedInstance);
        slot.spawnedInstance = null;
    }


    // ================== [스탯 보너스] ==================

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
