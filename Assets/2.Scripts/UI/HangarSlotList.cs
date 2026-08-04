/*
 * [HangarSlotList]
 * 격납고 파츠 리스트(Scroll View) 관리자.
 * 인벤토리(InventoryManager)의 PartData들을 가져와 Content에 슬롯으로 채움.
 *
 * [부착 위치]
 * Slot(또는 Scroll View 루트)에 부착.
 *
 * [인스펙터 연결]
 * - ItemSlot : HangarItemSlot 프리팹 (Project 창에서 드래그)
 * - content  : Scroll View > Viewport > Content (Hierarchy에서 드래그)
 *
 * [동작]
 * - OnEnable(탭으로 켜질 때) 인벤토리의 모든 파츠를 자동으로 다시 그림
 * - 외부에서 Refresh(stacks)로 특정 목록만 그릴 수도 있음
 */

using System.Collections.Generic;
using UnityEngine;

public class HangarSlotList : MonoBehaviour
{
    [Header("연동")]
    [SerializeField] private GameObject ItemSlot;  // HangarItemSlot 프리팹
    [SerializeField] private Transform  content;   // Scroll View > Content

    [Header("동작")]
    [Tooltip("체크 시 RefreshFiltered 목록에 '현재 장착된 파츠'도 맨 위에 함께 표시")]
    [SerializeField] private bool includeEquipped = true;

    private void OnEnable()
    {
        RefreshFromInventory();
    }

    /// <summary>인벤토리의 여분 PartData를 가져와 리스트를 다시 그림.
    /// (기본 장착 파츠는 리스트가 아니라 노드에 아이콘으로 표시 — HangarEquipController가 담당)</summary>
    public void RefreshFromInventory()
    {
        if (InventoryManager.Instance == null) { Clear(); return; }
        Refresh(InventoryManager.Instance.GetAllOfType<PartData>());
    }

    /// <summary>특정 종류의 파츠만 골라 리스트를 다시 그림 (노드 클릭 시).
    /// includeEquipped면 현재 그 슬롯에 장착된 파츠도 맨 위에 함께 표시.</summary>
    public void RefreshFiltered(PART_TYPE type)
    {
        Clear();

        // ① 현재 장착된 파츠(들)를 맨 위에 — 기본 파츠는 인벤토리가 아니라 UnitParts에 있으므로 여기서 챙김
        PartData equippedPart = null;
        if (includeEquipped)
        {
            UnitParts parts = GetPlayerParts();
            if (parts != null)
            {
                foreach (PartSlotEntry slot in parts.partSlots)
                {
                    if (slot == null || slot.slotType != type || slot.equippedPart == null) continue;
                    AddSlot(slot.equippedPart, 1, equipped: true);
                    equippedPart = slot.equippedPart;
                }
            }
        }

        // ② 인벤토리의 여분 파츠 (장착된 것과 같은 건 제외 — 중복 방지)
        if (InventoryManager.Instance != null)
        {
            foreach (ItemStack stack in InventoryManager.Instance.GetAllOfType<PartData>())
            {
                if (stack == null || stack.data == null) continue;
                PartData part = stack.data as PartData;
                if (part == null || part.partType != type) continue;
                if (part == equippedPart) continue;   // 이미 장착 항목으로 위에 뜸
                AddSlot(part, stack.count, equipped: false);
            }
        }
    }

    // 현재 로컬 플레이어의 UnitParts (장착 파츠 조회용)
    private UnitParts GetPlayerParts()
    {
        Player player = GameManager.Instance != null ? GameManager.Instance.playerRef : null;
        return player != null ? player.GetComponent<UnitParts>() : null;
    }

    /// <summary>ItemStack 목록을 받아 슬롯으로 채움.</summary>
    public void Refresh(List<ItemStack> stacks)
    {
        Clear();
        if (stacks == null) return;

        foreach (ItemStack stack in stacks)
        {
            // 팀장 테스트용 빈 스택 / 파츠 아닌 항목 방어
            if (stack == null || stack.data == null) continue;
            PartData part = stack.data as PartData;
            if (part == null) continue;

            AddSlot(part, stack.count, equipped: false);
        }
    }

    // 슬롯 한 칸 생성 (Refresh/RefreshFiltered 공용)
    private void AddSlot(PartData part, int count, bool equipped)
    {
        if (content == null || ItemSlot == null || part == null) return;

        GameObject go = Instantiate(ItemSlot, content);
        HangarItemSlot slot = go.GetComponent<HangarItemSlot>();
        if (slot != null)
            slot.Setup(part, count, equipped);   // 장착은 드래그로 — 클릭 이벤트 불필요
    }

    /// <summary>목록을 비움.</summary>
    public void Clear()
    {
        if (content == null) return;
        foreach (Transform child in content)
            Destroy(child.gameObject);
    }
}
