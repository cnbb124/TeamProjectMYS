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
    [Tooltip("이 파츠의 현재 HP. 플레이 중 자동으로 갱신됨(여기 편집한 값은 무시됨). 실시간 확인용.")]
    public int curPartHp;
    // 지금 이 파츠가 유닛 스탯에 반영해둔 비율(0=파괴, 0.5=절반, 1=온전).
    // HP가 바뀌면 이 값과의 차이만큼만 유닛 스탯에 더하거나 빼서 갱신함.
    [System.NonSerialized] public float appliedStatRatio;
    // statBonuses와 같은 인덱스로, 지금 이 파츠가 유닛 스탯에 넣어둔 실제 값.
    [System.NonSerialized] public float[] appliedStatValues;
}

public class UnitParts : MonoBehaviour
{
    private Unit _unit;
    private WeaponSystem _weaponSystem;

    // ENGINE, FRAME은 프레임과 무관하게 항상 존재하는 기본 슬롯
    private static PART_TYPE[] BASE_SLOT_TYPES = { PART_TYPE.ENGINE, PART_TYPE.FRAME };

    [Header("<size=14>기본 로드아웃 (씬 직접 재생용)</size>")]
    [Tooltip("정상 흐름에선 PlayerProfile이 덮어씀. 씬을 단독 재생할 때만 쓰임.")]
    [SerializeField] private DefaultLoadout _defaultLoadout;

    // 스폰된 파츠 프리팹이 부착될 부모. 닷지/부스트 등 연출 애니메이션이 VIsual을 움직이므로,
    // 파츠(메쉬+발사위치)도 같이 따라가도록 VIsual 자식으로 부착한다.
    [Tooltip("미지정 시 \"Visual\" 이름의 자식을 자동 탐색. 못 찾으면 자기 자신(루트)에 부착.")]
    [SerializeField] private Transform _visualRoot;

    [Header("<size=14>파츠 슬롯 (출력용)</size>")]
    public List<PartSlotEntry> partSlots = new List<PartSlotEntry>();

    private void Awake()
    {
        _unit = GetComponent<Unit>();
        _weaponSystem = GetComponent<WeaponSystem>();
        EnsureBaseSlots();

        if (_visualRoot == null)
        {
            _visualRoot = transform.Find("Visual");
            if (_visualRoot == null)
            {
                Debug.Log("UnitParts: \"Visual\" 자식을 찾지 못함. 파츠 프리팹을 루트에 부착함");
                _visualRoot = transform;
            }
        }
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

    // 풀에서 재사용(SetActive(true))될 때마다 호출 — Start()는 오브젝트 생애 단 한 번만 실행되므로,
    // 이전 생애에서 깎였던 curPartHp가 재사용 시 그대로 남아있던 문제를 막기 위함.
    // equippedPart가 아직 null인 슬롯(최초 활성화 시, Start()가 아직 장착하기 전)은 건너뜀 — Start()가 채움.
    private void OnEnable()
    {
        foreach (PartSlotEntry slot in partSlots)
        {
            if (slot.equippedPart != null)
            {
                slot.curPartHp = slot.equippedPart.maxPartHp;
                // 풀에서 다시 꺼내 쓸 때 HP가 가득 찬 상태로 돌아오므로, 스탯도 온전한 값으로 되돌림
                // (이전에 죽기 전 깎였던 스탯이 남아있지 않게).
                RefreshPartStat(slot, GetHpStatRatio(slot));
            }
        }
    }

    private void Start()
    {
        IList<PartData> startParts = GetStartParts();
        if (startParts != null)
        {
            ApplyStartParts(startParts);
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
            slot.curPartHp = slot.equippedPart.maxPartHp;
            RefreshPartStat(slot, GetHpStatRatio(slot));
        }

        // Unit.Start()와의 실행순서가 보장되지 않아 cur=max 초기화가 위 보너스 적용 전에 끝났을 수 있음.
        // 파츠 적용이 끝난 지금 시점 기준으로 cur을 다시 max로 동기화.
        _unit.RefillToMax();
    }

    private IList<PartData> GetStartParts()
    {
        return _defaultLoadout != null ? _defaultLoadout.defaultParts : null;
    }

    /// <summary>
    /// 시작 파츠 목록으로 자동 장착.
    /// FRAME 먼저 처리해야 providedSlots 기반 런처 슬롯이 생성됨.
    /// </summary>
    private void ApplyStartParts(IList<PartData> parts)
    {
        // 최초 로드아웃 — 아직 프리팹/스탯보너스가 적용되기 전(Start)이라 정리 없이 재생성.
        // 인스펙터 슬롯 전체 제거 후 기본 슬롯만 재생성 (이전 슬롯 구조 완전 무시)
        partSlots.Clear();
        EnsureBaseSlots();
        EquipPartsList(parts, null);

        // Unit.Start()와의 실행순서가 보장되지 않아 cur=max 초기화가 위 보너스 적용 전에 끝났을 수 있음.
        // 파츠 적용이 끝난 지금 시점 기준으로 cur을 다시 max로 동기화.
        _unit.RefillToMax();
    }

    /// <summary>
    /// 런타임 로드아웃 재구성 (세이브 복원 등). 이미 장착된 파츠의 프리팹/스탯보너스를
    /// 먼저 정리한 뒤, 주어진 목록으로 다시 장착한다.
    /// 같은 타입 슬롯이 여러 개(좌우 런처 등)면 목록 순서대로 빈 슬롯에 채워진다.
    /// </summary>
    public void ReloadLoadout(IList<PartData> parts)
    {
        ReloadLoadout(parts, null);
    }

    /// <summary>
    /// 위와 같되, 저장돼 있던 파츠별 HP까지 되돌린다.
    /// partHps는 parts와 같은 인덱스이며, 음수는 "만피로 둠"을 뜻함.
    /// </summary>
    public void ReloadLoadout(IList<PartData> parts, IList<int> partHps)
    {
        ClearEquippedParts();
        partSlots.Clear();
        EnsureBaseSlots();

        // parts[i]가 실제로 어느 슬롯에 들어갔는지 받아둠 — 장착 순서(FRAME 우선)와 슬롯 순서가
        // 달라서 인덱스만으로는 HP를 어느 슬롯에 넣어야 할지 알 수 없음.
        PartSlotEntry[] placedSlots = new PartSlotEntry[parts.Count];
        EquipPartsList(parts, placedSlots);

        _unit.RefillToMax();
        // RefillToMax가 전부 만피로 되돌리므로, 손상 상태는 그 뒤에 다시 입혀야 함.
        ApplyPartHps(placedSlots, partHps);
    }

    // FRAME을 먼저 장착해야 providedSlots 기반 런처 슬롯이 생성되므로 FRAME → 나머지 순으로 처리.
    // 같은 타입 슬롯이 여러 개면 EquipFromDefault의 GetFirstEmptySlot이 목록 순서대로 채운다.
    // placedSlots(선택): parts[i]가 들어간 슬롯을 같은 인덱스에 기록함. 안 쓰면 null.
    private void EquipPartsList(IList<PartData> parts, PartSlotEntry[] placedSlots)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            PartData part = parts[i];
            if (part == null || part.partType != PART_TYPE.FRAME)
            {
                continue;
            }
            PartSlotEntry slot = EquipFromDefault(part);
            if (placedSlots != null)
            {
                placedSlots[i] = slot;
            }
            break;
        }

        for (int i = 0; i < parts.Count; i++)
        {
            PartData part = parts[i];
            if (part == null || part.partType == PART_TYPE.FRAME)
            {
                continue;
            }
            PartSlotEntry slot = EquipFromDefault(part);
            if (placedSlots != null)
            {
                placedSlots[i] = slot;
            }
        }
    }

    // 저장돼 있던 파츠 HP를 되돌림. 스탯 기여도도 그 HP에 맞게 다시 계산됨
    // (RefreshPartStat 안의 ClampCurrentToMax가 줄어든 최대치에 맞춰 현재값도 잘라줌).
    private void ApplyPartHps(PartSlotEntry[] placedSlots, IList<int> partHps)
    {
        if (placedSlots == null || partHps == null)
        {
            return;
        }

        int count = Mathf.Min(placedSlots.Length, partHps.Count);
        for (int i = 0; i < count; i++)
        {
            PartSlotEntry slot = placedSlots[i];
            if (slot == null || slot.equippedPart == null || slot.equippedPart.maxPartHp <= 0)
            {
                continue;
            }
            // 음수 = 저장된 정보 없음(구버전 세이브/새 게임). 만피 그대로 둠.
            if (partHps[i] < 0)
            {
                continue;
            }
            slot.curPartHp = Mathf.Clamp(partHps[i], 0, slot.equippedPart.maxPartHp);
            RefreshPartStat(slot, GetHpStatRatio(slot));
        }
    }

    // 현재 장착된 모든 파츠의 프리팹 파괴 + 스탯보너스 해제. 런타임 재장착 전 정리용.
    private void ClearEquippedParts()
    {
        foreach (PartSlotEntry slot in partSlots)
        {
            if (slot.equippedPart != null)
            {
                DestroyPartPrefab(slot);
                RefreshPartStat(slot, 0f);
                slot.equippedPart = null;
            }
        }
    }

    // 시작 파츠 전용 장착. 빈 슬롯에 순서대로 채움. 들어간 슬롯을 돌려줌(빈 슬롯이 없으면 null).
    private PartSlotEntry EquipFromDefault(PartData newPart)
    {
        PartSlotEntry slot = GetFirstEmptySlot(newPart.partType);
        if (slot == null)
        {
            Debug.LogWarning("[UnitParts] 시작 파츠: 빈 슬롯 없음 - " + newPart.partType);
            return null;
        }

        slot.equippedPart = newPart;
        SpawnPartPrefab(slot);
        slot.curPartHp = newPart.maxPartHp;
        RefreshPartStat(slot, GetHpStatRatio(slot));

        if (newPart.partType == PART_TYPE.FRAME)
        {
            RebuildSlotsFromFrame(newPart);
        }
        return slot;
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
            RefreshPartStat(slot, 0f);
        }

        slot.equippedPart = newPart;

        if (newPart != null)
        {
            SpawnPartPrefab(slot);
            slot.curPartHp = newPart.maxPartHp;
            RefreshPartStat(slot, GetHpStatRatio(slot));
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
            RefreshPartStat(slot, 0f);
        }

        slot.equippedPart = newPart;

        if (newPart != null)
        {
            SpawnPartPrefab(slot);
            slot.curPartHp = newPart.maxPartHp;
            RefreshPartStat(slot, GetHpStatRatio(slot));
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
                RefreshPartStat(slot, 0f);
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

        slot.spawnedInstance = Instantiate(slot.equippedPart.partPrefab, _visualRoot);
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
            if (slot.equippedPart == null || slot.slotType == PART_TYPE.FRAME || slot.slotType == PART_TYPE.ARMOR || slot.equippedPart.maxPartHp <= 0)
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
            if (slot.equippedPart == null || slot.slotType == PART_TYPE.FRAME || slot.slotType == PART_TYPE.ARMOR || slot.equippedPart.maxPartHp <= 0)
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

    // 파츠 HP를 깎고, 줄어든 HP에 맞춰 그 파츠의 스탯 기여도 다시 맞춤.
    private void ApplyPartDamage(PartSlotEntry slot, int damage)
    {
        slot.curPartHp = Mathf.Max(0, slot.curPartHp - damage);
        RefreshPartStat(slot, GetHpStatRatio(slot));
    }

    /// <summary>
    /// 장착된 파츠 중 HP가 깎인 게 하나라도 있는지. 수리 UI가 "고칠 게 있는지" 판단할 때 씀.
    /// </summary>
    public bool HasDamagedPart()
    {
        foreach (PartSlotEntry slot in partSlots)
        {
            if (slot.equippedPart == null || slot.equippedPart.maxPartHp <= 0)
            {
                continue;
            }
            if (slot.curPartHp < slot.equippedPart.maxPartHp)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 장착된 모든 파츠의 HP를 최대치로 되돌리고, 깎여 있던 스탯 기여도도 온전한 값으로 복구.
    /// 레벨업/수리 등 "완전 회복" 상황에서 Unit.RefillToMax()가 호출함.
    /// (파츠 HP가 안 차면 엔진/스러스터 스탯이 깎인 채로 남아 최대치 회복이 반쪽이 됨)
    /// </summary>
    public void RefillAllPartsHp()
    {
        foreach (PartSlotEntry slot in partSlots)
        {
            if (slot.equippedPart == null)
            {
                continue;
            }
            slot.curPartHp = slot.equippedPart.maxPartHp;
            RefreshPartStat(slot, GetHpStatRatio(slot));
        }
    }

    // ================== [파츠 HP에 따른 스탯 조정] ==================

    // 파츠 HP에 따라 스탯을 얼마나 줄지 비율로 알려줌.
    // HP 50% 초과면 온전(1), 50% 이하면 절반(0.5), HP 0(파괴)이면 스탯 없음(0).
    // maxPartHp가 0인 파츠(FRAME처럼 HP 개념이 없는 것)는 항상 온전(1).
    private float GetHpStatRatio(PartSlotEntry slot)
    {
        if (slot.equippedPart == null || slot.equippedPart.maxPartHp <= 0)
        {
            return 1f;
        }
        if (slot.curPartHp <= 0)
        {
            return 0f;
        }
        float ratio = (float)slot.curPartHp / slot.equippedPart.maxPartHp;
        return ratio > 0.5f ? 1f : 0.5f;
    }

    // 이 파츠가 유닛 스탯에 넣는 양을 targetRatio(0/0.5/1)에 맞춰 갱신함.
    // 최대실드/최대부스트 같은 값은 여러 파츠가 같이 더하는 공용 값이라, 전부 다시 계산하지 않고
    // '이 파츠가 이전에 넣어둔 양'과의 차이만큼만 더하거나 뺌.
    // 장착(가득 넣기)·피격이나 수리(HP만큼)·해제(0으로 빼기)가 전부 이 함수 하나로 처리됨.
    private void RefreshPartStat(PartSlotEntry slot, float targetRatio)
    {
        if (_unit == null || slot.equippedPart == null)
        {
            return;
        }
        List<PartStatBonus> bonuses = slot.equippedPart.statBonuses;
        if (slot.appliedStatValues == null || slot.appliedStatValues.Length != bonuses.Count)
        {
            slot.appliedStatValues = new float[bonuses.Count];
        }

        for (int i = 0; i < bonuses.Count; i++)
        {
            PartStatBonus bonus = bonuses[i];
            float target = bonus.value * targetRatio;
            // 정수로 반영되는 스탯은 여기서 미리 잘라야 함. 차이만 넘기면 절삭이 매번 따로 일어나
            // 0.5씩 두 번 내린 값과 1로 한 번 올린 값이 안 맞음(방어력 3이 파괴 후 1로 남는 문제).
            if (IsIntegerStat(bonus.statType))
            {
                target = (int)target;
            }

            float change = target - slot.appliedStatValues[i];
            if (!Mathf.Approximately(change, 0f))
            {
                ApplySingleBonus(bonus.statType, change);
            }
            slot.appliedStatValues[i] = target;
        }
        slot.appliedStatRatio = targetRatio;
        // 최대치 스탯(실드/부스트/아머)이 줄었으면 현재값이 그 위로 튀지 않게 맞춤.
        ClampCurrentToMax();
    }

    // 최대치가 줄었을 때 현재값이 그 위로 튀지 않게 맞춤. HP는 사망 유발 위험이라 건드리지 않음.
    private void ClampCurrentToMax()
    {
        if (_unit.curShieldRemaining > _unit.maxShieldCapacity)
        {
            _unit.curShieldRemaining = _unit.maxShieldCapacity;
        }
        if (_unit.curBoostRemaining > _unit.maxBoostCapacity)
        {
            _unit.curBoostRemaining = _unit.maxBoostCapacity;
        }
        if (_unit.curArmorRemaining > _unit.maxArmor)
        {
            _unit.curArmorRemaining = _unit.maxArmor;
        }
    }

    // ApplySingleBonus에서 (int)로 잘려 들어가는 스탯들.
    private static bool IsIntegerStat(STAT_TYPE statType)
    {
        switch (statType)
        {
            case STAT_TYPE.HP_MAX:
            case STAT_TYPE.SHIELD_MAX:
            case STAT_TYPE.ARMOR_MAX:
            case STAT_TYPE.ARMOR_DEF:
                return true;
            default:
                return false;
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
            // 연사 딜레이 감소 — 쿨다운(초)에서 보너스만큼 빼서 발사 간격을 줄인다.
            // 장착(+1)이면 val>0이라 쿨다운 감소, 해제(-1)면 val<0이라 정확히 원복(대칭).
            case STAT_TYPE.FIRE_BULLET_DELAY_DECREASE:
                if (_weaponSystem != null)
                {
                    _weaponSystem.bulletFireCooldown -= val;
                }
                break;
            case STAT_TYPE.FIRE_MISSILE_DELAY_DECREASE:
                if (_weaponSystem != null)
                {
                    _weaponSystem.missileFireCooldown -= val;
                }
                break;
        }
    }
}
