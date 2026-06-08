// ================================================================
// [외부 참조 가이드]
// ================================================================
// ▶ HUD팀 참조용 (읽기 전용으로 사용할 것)
//   partSlots[i].slotType      : 슬롯 종류 (PART_TYPE)
//   partSlots[i].equippedPart  : 장착된 파츠 데이터. null이면 미장착.
//   partSlots[i].curPartHp     : 현재 파츠 HP (런타임 전용)
//
//   예시)
//   foreach (var slot in unitParts.partSlots)
//   {
//       if (slot.equippedPart == null) continue;
//       float ratio = (float)slot.curPartHp / slot.equippedPart.maxPartHp;
//   }
//
// ▶ 격납고 UI팀 참조용
//   Equip(PART_TYPE, PartData)   : 파츠 장착 (기존 파츠 자동 해제)
//   EquipAt(int, PartData)       : 인덱스로 장착 (런처 다중 슬롯 구분 시)
//   Unequip(PART_TYPE)           : 파츠 해제
//   GetEquipped(PART_TYPE)       : 현재 장착된 PartData 반환
//
//   예시)
//   unitParts.Equip(PART_TYPE.ARMOR, selectedPartData);
//   PartData cur = unitParts.GetEquipped(PART_TYPE.ENGINE);
// ================================================================

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PartSlotEntry
{
    public PART_TYPE slotType;
    public PartData equippedPart;
    // 장착 시 Instantiate된 파츠 오브젝트 (런타임 전용, 직렬화 제외)
    [System.NonSerialized] public GameObject spawnedInstance;
    // 파츠 현재 HP (런타임 전용)
    [System.NonSerialized] public int curPartHp;
}

public class UnitParts : MonoBehaviour
{
    private Unit _unit;
    private WeaponSystem _weaponSystem;

    // ENGINE, FRAME은 프레임과 무관하게 항상 존재하는 기본 슬롯
    private static readonly PART_TYPE[] BASE_SLOT_TYPES = { PART_TYPE.ENGINE, PART_TYPE.FRAME };

    [Header("<size=14>기본 로드아웃 (설정 시 인스펙터 파츠 슬롯 무시)</size>")]
    [SerializeField] private DefaultLoadout _defaultLoadout;

    [Header("<size=14>파츠 슬롯 (출력용)</size>")]
    public List<PartSlotEntry> partSlots = new List<PartSlotEntry>();

    private void Awake()
    {
        _unit = GetComponent<Unit>();
        _weaponSystem = GetComponent<WeaponSystem>();
        EnsureBaseSlots();
    }

    // ENGINE, FRAME 슬롯이 없으면 자동 생성. 나머지는 프레임이 결정.
    private void EnsureBaseSlots()
    {
        if (GetSlot(PART_TYPE.ENGINE) == null)
        {
            partSlots.Insert(0, new PartSlotEntry { slotType = PART_TYPE.ENGINE });
        }
        if (GetSlot(PART_TYPE.FRAME) == null)
        {
            partSlots.Insert(1, new PartSlotEntry { slotType = PART_TYPE.FRAME });
        }
    }

    private void Start()
    {
        if (_defaultLoadout != null)
        {
            ApplyDefaultLoadout();
            return;
        }

        // 인스펙터에 미리 세팅된 파츠 스탯 + 프리팹 적용
        foreach (PartSlotEntry slot in partSlots)
        {
            if (slot.equippedPart == null)
            {
                continue;
            }
            SpawnPartPrefab(slot);
            ApplyStatBonuses(slot.equippedPart, 1);
            slot.curPartHp = slot.equippedPart.maxPartHp;
        }
    }

    /// <summary>
    /// DefaultLoadout SO 기반으로 자동 장착.
    /// FRAME 먼저 처리해야 providedSlots 기반 런처 슬롯이 생성됨.
    /// </summary>
    private void ApplyDefaultLoadout()
    {
        // 인스펙터 슬롯 전체 제거 후 기본 슬롯만 재생성 (이전 슬롯 구조 완전 무시)
        partSlots.Clear();
        EnsureBaseSlots();

        // FRAME 먼저 처리 — RebuildSlotsFromFrame으로 런처 슬롯 생성
        foreach (PartData part in _defaultLoadout.defaultParts)
        {
            if (part == null || part.partType != PART_TYPE.FRAME)
            {
                continue;
            }
            EquipFromDefault(part);
            break;
        }

        // 나머지 파츠
        foreach (PartData part in _defaultLoadout.defaultParts)
        {
            if (part == null || part.partType == PART_TYPE.FRAME)
            {
                continue;
            }
            EquipFromDefault(part);
        }
    }

    // DefaultLoadout 전용 장착. 빈 슬롯에 순서대로 채움.
    private void EquipFromDefault(PartData newPart)
    {
        PartSlotEntry slot = GetFirstEmptySlot(newPart.partType);
        if (slot == null)
        {
            Debug.LogWarning("[UnitParts] DefaultLoadout: 빈 슬롯 없음 - " + newPart.partType);
            return;
        }

        slot.equippedPart = newPart;
        SpawnPartPrefab(slot);
        ApplyStatBonuses(newPart, 1);
        slot.curPartHp = newPart.maxPartHp;

        if (newPart.partType == PART_TYPE.FRAME)
        {
            RebuildSlotsFromFrame(newPart);
        }
    }

    // 해당 타입의 equippedPart가 없는 첫 번째 슬롯 반환
    private PartSlotEntry GetFirstEmptySlot(PART_TYPE slotType)
    {
        foreach (PartSlotEntry slot in partSlots)
        {
            if (slot.slotType == slotType && slot.equippedPart == null)
            {
                return slot;
            }
        }
        return null;
    }

    /// <summary>
    /// 지정 슬롯에 파츠 장착. 기존 파츠는 자동 해제.
    /// FRAME 장착 시 providedSlots 기반으로 슬롯 재구성.
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
            slot.curPartHp = newPart.maxPartHp;
        }

        // 프레임 교체 시 슬롯 재구성
        if (slotType == PART_TYPE.FRAME)
        {
            RebuildSlotsFromFrame(newPart);
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
            slot.curPartHp = newPart.maxPartHp;
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


    // ================== [프레임 슬롯 재구성] ==================

    /// <summary>
    /// 프레임의 providedSlots 기준으로 비기본 슬롯 재구성.
    /// 기존 장착 파츠는 해제되며 인벤토리 반환은 호출부(격납고 UI)에서 처리.
    /// </summary>
    private void RebuildSlotsFromFrame(PartData frameData)
    {
        // 비기본 슬롯 전부 해제 후 제거 (뒤에서부터 순회)
        for (int i = partSlots.Count - 1; i >= 0; i--)
        {
            PartSlotEntry slot = partSlots[i];

            bool isBase = false;
            foreach (PART_TYPE baseType in BASE_SLOT_TYPES)
            {
                if (slot.slotType == baseType)
                {
                    isBase = true;
                    break;
                }
            }

            if (isBase)
            {
                continue;
            }

            if (slot.equippedPart != null)
            {
                DestroyPartPrefab(slot);
                ApplyStatBonuses(slot.equippedPart, -1);
                slot.equippedPart = null;
            }

            partSlots.RemoveAt(i);
        }

        if (frameData == null)
        {
            return;
        }

        // 프레임이 정의한 슬롯 추가 (BASE 슬롯은 이미 존재하므로 제외)
        foreach (PART_TYPE slotType in frameData.providedSlots)
        {
            bool isBase = false;
            foreach (PART_TYPE baseType in BASE_SLOT_TYPES)
            {
                if (slotType == baseType)
                {
                    isBase = true;
                    break;
                }
            }
            if (isBase)
            {
                continue;
            }
            partSlots.Add(new PartSlotEntry { slotType = slotType });
        }
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
        slot.spawnedInstance.transform.localPosition = slot.equippedPart.mountOffset;

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


    // ================== [파츠 피격] ==================

    /// <summary>
    /// hitPosition에서 가장 가까운 파츠(FRAME 제외) 1개에 데미지.
    /// 단발 투사체(총알 등) 피격 시 호출.
    /// </summary>
    public void DamageNearestPart(Vector3 hitPosition, int damage)
    {
        PartSlotEntry nearest = null;
        float nearestDist = float.MaxValue;

        foreach (PartSlotEntry slot in partSlots)
        {
            if (slot.equippedPart == null || slot.slotType == PART_TYPE.FRAME || slot.equippedPart.maxPartHp <= 0)
            {
                continue;
            }
            float dist = Vector3.Distance(hitPosition, GetPartWorldPos(slot));
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = slot;
            }
        }

        if (nearest != null)
        {
            ApplyPartDamage(nearest, damage);
        }
    }

    /// <summary>
    /// center 기준 radius 반경 내 모든 파츠(FRAME 제외)에 각 1회 데미지.
    /// 미사일 폭발 AOE 피격 시 호출.
    /// </summary>
    public void DamagePartsInRange(Vector3 center, float radius, int damage)
    {
        foreach (PartSlotEntry slot in partSlots)
        {
            if (slot.equippedPart == null || slot.slotType == PART_TYPE.FRAME || slot.equippedPart.maxPartHp <= 0)
            {
                continue;
            }
            float dist = Vector3.Distance(center, GetPartWorldPos(slot));
            if (dist <= radius)
            {
                ApplyPartDamage(slot, damage);
            }
        }
    }

    // 파츠의 월드 좌표 반환. 프리팹 없는 파츠(ENGINE/ARMOR 등)는 기체 중심 사용.
    private Vector3 GetPartWorldPos(PartSlotEntry slot)
    {
        if (slot.spawnedInstance != null)
        {
            return slot.spawnedInstance.transform.position;
        }
        return transform.position;
    }

    // 파츠 HP 감소. 향후 HP 티어 변화 처리 위치.
    private void ApplyPartDamage(PartSlotEntry slot, int damage)
    {
        slot.curPartHp = Mathf.Max(0, slot.curPartHp - damage);
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
            case STAT_TYPE.HP_MAX:
                _unit.maxHpRemaining += (int)val;
                break;
            case STAT_TYPE.SHIELD_MAX:
                _unit.maxShieldCapacity += (int)val;
                break;
            case STAT_TYPE.SHIELD_REGEN_RATE:
                _unit.shieldRegainRate += val;
                break;
            case STAT_TYPE.ARMOR_MAX:
                _unit.maxArmor += (int)val;
                break;
            case STAT_TYPE.ARMOR_DEF:
                _unit.defense += (int)val;
                break;
            case STAT_TYPE.MOVE_SPEED_BASE:
                _unit.baseMoveSpeed += val;
                break;
            case STAT_TYPE.MOVE_SPEED_MAX:
                _unit.maxSpeed += val;
                break;
            case STAT_TYPE.MOVE_SPEED_BOOST:
                _unit.boostSpeed += val;
                break;
            case STAT_TYPE.BOOST_MAX:
                _unit.maxBoostCapacity += val;
                break;
            case STAT_TYPE.BOOST_REGEN_RATE:
                _unit.boostRegainRate += val;
                break;
            case STAT_TYPE.CRI_RATE:
                _unit.criChance += val;
                break;
            case STAT_TYPE.CRI_DMG_MULT:
                _unit.criDamageMultiplier += val;
                break;
            case STAT_TYPE.FUEL_MAX:
                Player fuelPlayer = _unit as Player;
                if (fuelPlayer != null)
                {
                    fuelPlayer.maxFuelCapacity += val;
                }
                break;
        }
    }
}
