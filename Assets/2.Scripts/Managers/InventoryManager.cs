using System.Collections.Generic;
using UnityEngine;

// ItemData(PartData, ConsumableData 등)와 수량을 묶은 인벤토리 슬롯 단위.
[System.Serializable]
public class ItemStack
{
    public ItemData data;
    public int count;
}

// ================================================================
// [외부 참조 가이드] - InventoryPanelUI 연동용
// ================================================================
// ▶ 탭 전환 (장비 / 소모품 / 재료 등, ITEM_CATEGORY 기준)
//   GetAllOfCategory(ITEM_CATEGORY category) : 해당 카테고리 아이템만 필터링해서 반환
//   예시) List<ItemStack> list = InventoryManager.Instance.GetAllOfCategory(ITEM_CATEGORY.PARTS);
//
//   탭 버튼 클릭 -> RefreshItemGrid(category) 호출 흐름 예시:
//     void RefreshItemGrid(ITEM_CATEGORY category)
//     {
//         // 1. 기존 ITEM 그리드 슬롯 전부 제거
//         // 2. GetAllOfCategory(category)로 현재 탭 목록 가져오기
//         List<ItemStack> list = InventoryManager.Instance.GetAllOfCategory(category);
//         // 3. 리스트 순회하며 슬롯 prefab 생성, stack.data.icon / itemName / stack.count 표시
//         foreach (ItemStack stack in list) { /* 슬롯 생성 */ }
//     }
//
// ▶ 타입 기준 필터링 (카테고리 대신 C# 타입으로 구분할 때)
//   GetAllOfType<PartData>() / GetAllOfType<ConsumableData>() 등도 사용 가능
//
// ▶ 골드 표시
//   gold : 보유 골드
//   예시) goldText.text = InventoryManager.Instance.gold.ToString();
//
// ▶ 보유 수량 확인
//   GetCount(ItemData data) : 없으면 0 반환
//   예시) int count = InventoryManager.Instance.GetCount(someItemData);
//
// ▶ 추가 / 제거 (탭 구분 없이 동일 items 리스트에 직접 반영됨)
//   AddItem(ItemData data, int amount = 1)
//   RemoveItem(ItemData data, int amount = 1) : 수량 0되면 items에서 자동 제거
//   예시) InventoryManager.Instance.RemoveItem(someItemData, 1);
//
// ▶ UI 갱신용 이벤트 (신규) — AddItem/RemoveItem/AddGold/SpendGold(성공 시) 끝에서 자동 발행됨
//   public event System.Action OnInventoryChanged;
//   UI팀 할 일: InventoryPanelUI(또는 InventoryTabGroup) Awake/OnEnable에서 구독,
//              OnDisable/OnDestroy에서 구독 해제. 콜백 안에서 RefreshCurrent()(현재 탭 갱신) +
//              골드 텍스트 갱신 호출.
//   예시)
//     void OnEnable()  { InventoryManager.Instance.OnInventoryChanged += HandleInventoryChanged; }
//     void OnDisable() { InventoryManager.Instance.OnInventoryChanged -= HandleInventoryChanged; }
//     void HandleInventoryChanged() { RefreshCurrent(); goldText.text = InventoryManager.Instance.gold.ToString(); }
//   이제 패널이 열려있는 동안 아이템을 먹어도 그 자리에서 바로 갱신됨(전엔 패널 재오픈/탭전환 때만 갱신됐음).
//
// ▶ 퀵슬롯 등록 (드래그앤드롭 등)
//   AssignToQuickSlot(ItemData data, int slotIndex) : 소모품만 가능 (파츠 넘기면 경고 후 무시)
//   예시) InventoryManager.Instance.AssignToQuickSlot(consumableData, 0);
//
// ▶ 장착 (UI 장착 버튼 → 여기만 호출. UnitParts/WeaponSystem을 직접 부르지 말 것)
//   장착은 전부 InventoryManager를 경유합니다. 내부에서 RegisterPlayer로 받은 Player의
//   UnitParts / WeaponSystem / QuickSlot으로 위임하므로, UI는 InventoryManager만 알면 됩니다.
//
//   EquipPart(PartData part)                                       : 파츠 장착 (part.partType 슬롯에)
//   EquipMissile(int slotIndex, MISSILE_TYPE type, int maxAmmo, MissileData data) : 미사일 슬롯 장착
//   EquipBullet(BulletData data)                                   : 총알 데이터 장착 (무한탄, 슬롯 없음)
//   AssignToQuickSlot(ItemData data, int slotIndex)                : 소모품 → 퀵슬롯 (위 참조)
//
//   예시)
//     InventoryManager.Instance.EquipPart(partData);
//     InventoryManager.Instance.EquipMissile(0, MISSILE_TYPE.HOMING, 10, missileData);
//     InventoryManager.Instance.EquipBullet(bulletData);
//
//   ※ 이 메서드들은 "장착만" 합니다. 장착 시 가방(items)에서도 빼야 하면 RemoveItem을 따로 호출하세요.
//     (AssignToQuickSlot과 동일 정책 — 저장 관리와 장착을 분리)
//   ※ Player가 아직 씬에 없으면(RegisterPlayer 전) 경고만 뜨고 무시됩니다.
// ================================================================

// =====================================================================
// InventoryManager
//
// 역할:
//   1. 골드 보유량 관리 (AddGold / SpendGold)
//   2. 아이템(파츠, 소모품 등) 보유 목록 관리 (추가/제거/수량조회)
//   3. 장착 라우팅 (파츠/미사일/총알/소모품을 각 시스템에 위임 — UI는 InventoryManager만 호출)
//   4. Player 참조 캐싱 (RegisterPlayer — 장착 위임 대상)
//
// 연관 스크립트:
//   ItemPickup.cs   : 월드 드랍 아이템/골드 획득 시 AddItem/AddGold 호출
//   QuickSlot.cs    : AssignToQuickSlot(등록) / ConsumeOne(사용) 호출
//   UnitParts.cs    : EquipPart -> Equip() 위임
//   WeaponSystem.cs : EquipMissile/EquipBullet -> 위임
//   SaveData.cs     : 저장 데이터 (위치: 2.Scripts/Data)
// =====================================================================
public class InventoryManager : MonoBehaviour
{
    private static InventoryManager instance;
    // Awake에서만 세팅됨. Awake 전엔 null이므로 최초 접근은 Start부터 할 것.
    public static InventoryManager Instance => instance;

    private Player _player;

    [Header("보유 골드")]
    public int gold;

    [Header("보유 아이템 목록 (파츠 / 소모품 / 재료 등)")]
    public List<ItemStack> items = new List<ItemStack>();

    // 골드/아이템 변동 시 발행. UI팀이 구독해서 슬롯/골드 텍스트 갱신용으로 사용.
    public event System.Action OnInventoryChanged;

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
        OnInventoryChanged?.Invoke();
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
        OnInventoryChanged?.Invoke();
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
        OnInventoryChanged?.Invoke();
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
        OnInventoryChanged?.Invoke();
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

    /// <summary>ITEM_CATEGORY 기준으로 필터링해서 반환. 인벤토리 UI 탭 전환에서 사용.</summary>
    public List<ItemStack> GetAllOfCategory(ITEM_CATEGORY category)
    {
        List<ItemStack> result = new List<ItemStack>();
        foreach (ItemStack stack in items)
        {
            if (stack.data.category == category)
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

    /// <summary>씬 로드 시 Player.Start()에서 호출. QuickSlot 접근 경로 등록.</summary>
    public void RegisterPlayer(Player player)
    {
        _player = player;
    }

    /// <summary>소모품을 퀵슬롯에 할당. Inventory -> QuickSlot 단방향.</summary>
    public void AssignToQuickSlot(ItemData data, int slotIndex)
    {
        ConsumableData consumable = data as ConsumableData;
        if (consumable == null)
        {
            Debug.LogWarning("[Inventory] 소모품만 퀵슬롯에 등록 가능.");
            return;
        }
        if (_player == null || _player.quickSlot == null)
        {
            Debug.LogWarning("[Inventory] Player 또는 QuickSlot 참조 없음");
            return;
        }
        _player.quickSlot.AssignSlot(slotIndex, consumable);
    }

    // ==================== 장착 연결 (Inventory -> 각 시스템 단방향) ====================
    // 소모품(AssignToQuickSlot)과 동일하게, 장착은 InventoryManager를 경유해 각 시스템으로 라우팅.
    // 인벤 UI는 InventoryManager만 알면 됨. (인벤 저장 add/remove는 별개 — 여기선 장착만 위임)

    /// <summary>파츠 장착. part.partType 슬롯으로 Player의 UnitParts에 위임.</summary>
    public void EquipPart(PartData part)
    {
        if (part == null)
        {
            return;
        }
        if (_player == null)
        {
            Debug.LogWarning("[Inventory] Player 참조 없음");
            return;
        }
        UnitParts parts = _player.GetComponent<UnitParts>();
        if (parts == null)
        {
            Debug.LogWarning("[Inventory] UnitParts 없음");
            return;
        }
        parts.Equip(part.partType, part);
    }

    /// <summary>미사일 장착. Player의 WeaponSystem에 위임.</summary>
    public void EquipMissile(int slotIndex, MISSILE_TYPE type, int maxAmmo, MissileData data)
    {
        if (_player == null || _player.weaponSystem == null)
        {
            Debug.LogWarning("[Inventory] Player 또는 WeaponSystem 참조 없음");
            return;
        }
        _player.weaponSystem.EquipMissile(slotIndex, type, maxAmmo, data);
    }

    /// <summary>총알 데이터 장착. Player의 WeaponSystem에 위임(무한탄이라 슬롯/잔탄 없음).</summary>
    public void EquipBullet(BulletData data)
    {
        if (_player == null || _player.weaponSystem == null)
        {
            Debug.LogWarning("[Inventory] Player 또는 WeaponSystem 참조 없음");
            return;
        }
        _player.weaponSystem.EquipBullet(data);
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
