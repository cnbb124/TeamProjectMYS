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

    private void OnEnable()
    {
        RefreshFromInventory();
    }

    /// <summary>인벤토리의 모든 PartData를 가져와 리스트를 다시 그림.</summary>
    public void RefreshFromInventory()
    {
        if (InventoryManager.Instance == null) return;
        Refresh(InventoryManager.Instance.GetAllOfType<PartData>());
    }

    /// <summary>ItemStack 목록을 받아 슬롯으로 채움.</summary>
    public void Refresh(List<ItemStack> stacks)
    {
        if (content == null || ItemSlot == null) return;

        // 기존 슬롯 전부 제거
        foreach (Transform child in content)
            Destroy(child.gameObject);

        if (stacks == null) return;

        foreach (ItemStack stack in stacks)
        {
            // 팀장 테스트용 빈 스택 / 파츠 아닌 항목 방어
            if (stack == null || stack.data == null) continue;
            PartData part = stack.data as PartData;
            if (part == null) continue;

            GameObject go = Instantiate(ItemSlot, content);
            HangarItemSlot slot = go.GetComponent<HangarItemSlot>();
            if (slot != null) slot.Setup(part, stack.count);
        }
    }
}
