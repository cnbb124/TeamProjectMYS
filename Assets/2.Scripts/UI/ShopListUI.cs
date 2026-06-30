/*
 * [ShopListUI]
 * 호감도 상점의 "진열 목록" 관리자 (ShopStock → ShopItemSlotUI 다리).
 * 탭(카테고리) + 구매/판매 모드에 맞춰 ItemSlot 프리팹을 Content에 채우고, 클릭 시 구매/판매 처리.
 *
 * [부착] ShopPanel (또는 진열 영역 루트)
 *
 * [인스펙터 연결]
 * - shop           : AffinityShopManager
 * - itemSlotPrefab : ShopItemSlotUI 붙은 ItemSlot 프리팹
 * - content        : 슬롯이 쌓일 부모 (Scroll View Content / 그리드)
 * - startCategory  : 시작 탭
 *
 * [탭/모드 버튼 연결]
 * 탭 버튼 OnClick → SetCategoryParts / SetCategoryConsumable / SetCategoryMaterial
 * 모드 버튼 OnClick → SetBuyMode / SetSellMode
 *
 * [자동 갱신] 구매/판매로 인벤토리가 바뀌면(OnInventoryChanged) 목록 자동 새로고침.
 */

using System.Collections.Generic;
using UnityEngine;

public class ShopListUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AffinityShopManager shop;
    [SerializeField] private GameObject          itemSlotPrefab;
    [SerializeField] private Transform           content;

    [Header("초기 상태")]
    [SerializeField] private ITEM_CATEGORY startCategory = ITEM_CATEGORY.PARTS;
    [SerializeField] private bool sellMode = false;

    private ITEM_CATEGORY _category;

    private void Awake()
    {
        _category = startCategory;
    }

    private void OnEnable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= Refresh;
    }

    // ── 탭 / 모드 전환 (버튼 OnClick에 연결) ──
    public void SetCategoryParts()      { SetCategory(ITEM_CATEGORY.PARTS); }
    public void SetCategoryConsumable() { SetCategory(ITEM_CATEGORY.CONSUMABLE); }
    public void SetCategoryMaterial()   { SetCategory(ITEM_CATEGORY.MATERIAL); }

    public void SetCategory(ITEM_CATEGORY category)
    {
        _category = category;
        Refresh();
    }

    public void SetBuyMode()  { sellMode = false; Refresh(); }
    public void SetSellMode() { sellMode = true;  Refresh(); }

    // ── 목록 그리기 ──
    public void Refresh()
    {
        if (content == null || itemSlotPrefab == null || shop == null) return;

        // 기존 슬롯 제거
        foreach (Transform child in content)
            Destroy(child.gameObject);

        if (sellMode) BuildSellList();
        else          BuildBuyList();
    }

    // 구매 모드: ShopStock 진열 목록 (호감도 해금 반영)
    private void BuildBuyList()
    {
        List<ShopEntry> entries = shop.GetDisplay(_category);
        foreach (ShopEntry e in entries)
        {
            if (e == null || e.item == null) continue;
            CreateSlot(e.item, shop.GetBuyPrice(e));
        }
    }

    // 판매 모드: 플레이어가 보유한 해당 카테고리 아이템
    private void BuildSellList()
    {
        if (InventoryManager.Instance == null) return;

        foreach (ItemStack stack in InventoryManager.Instance.items)
        {
            if (stack == null || stack.data == null || stack.count <= 0) continue;
            if (stack.data.category != _category) continue;
            CreateSlot(stack.data, shop.GetSellPrice(stack.data));
        }
    }

    private void CreateSlot(ItemData item, int price)
    {
        GameObject go = Instantiate(itemSlotPrefab, content);
        ShopItemSlotUI slot = go.GetComponent<ShopItemSlotUI>();
        if (slot != null)
            slot.Setup(item, price, OnSlotClicked);
    }

    // ── 클릭 = 구매/판매 ──
    private void OnSlotClicked(ItemData item)
    {
        ShopResult result = sellMode ? shop.TrySell(item) : shop.TryBuy(item);

        switch (result)
        {
            case ShopResult.Success:
                // 성공 — OnInventoryChanged가 Refresh를 자동 호출함
                break;
            case ShopResult.NotEnoughGold:
                Debug.Log("[Shop] 골드 부족");
                break;
            case ShopResult.OutOfStock:
                Debug.Log("[Shop] 재고 없음");
                break;
            case ShopResult.NoneOwned:
                Debug.Log("[Shop] 팔 아이템 없음");
                break;
        }
        // 성공 시 InventoryManager.OnInventoryChanged가 Refresh를 자동 호출하므로 여기선 추가 처리 불필요.
    }
}
