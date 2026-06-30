/*
 * [AffinityShopManager]
 * 호감도 상점의 구매/판매 로직. ShopStock(진열) + AffinityTierTable(레벨/할인) + InventoryManager(골드/아이템) 연동.
 * 호감도 점수는 AffectionManager에서 읽어 "레벨 → 할인율"로만 반영 (호감도 상승은 대화 선택지가 담당, 여기선 안 올림).
 *
 * [부착]
 * 상점 UI 루트(ShopPanel 등)에 부착.
 *
 * [인스펙터 연결]
 * - stock     : ShopStock 에셋 (판매 목록)
 * - tierTable : AffinityTierTable 에셋 (레벨/할인 변환)
 * - npc       : 이 상점 캐릭터의 NPC_ID (호감도 조회 대상)
 * - sellRatio : 판매가 비율 (0.5 = 기본가의 50%)
 *
 * [UI 사용 예]
 *   foreach (var e in shop.GetDisplay(ITEM_CATEGORY.PARTS)) {
 *       int price = shop.GetBuyPrice(e);  // 할인 적용가
 *       ... 카드 생성 ...
 *   }
 *   shop.TryBuy(item);  // 결과(ShopResult)로 토스트 분기
 */

using System.Collections.Generic;
using UnityEngine;

public enum ShopResult { Success, NotEnoughGold, OutOfStock, NoneOwned, Invalid }

public class AffinityShopManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShopStock         stock;
    [SerializeField] private AffinityTierTable tierTable;
    [SerializeField] private NPC_ID            npc;

    [Header("판매 설정")]
    [Tooltip("판매가 = 기본가 × 이 비율")]
    [Range(0f, 1f)] [SerializeField] private float sellRatio = 0.5f;

    // 유한 재고 런타임 추적 (SO 원본을 직접 깎으면 에디터에 영구 반영되므로 분리)
    private readonly Dictionary<ItemData, int> _runtimeStock = new Dictionary<ItemData, int>();

    // 구매/판매로 진열·재고가 바뀌면 발행 → UI가 목록 다시 그림
    public event System.Action OnShopChanged;

    // ── 호감도 상태 조회 ──
    public int CurrentPoints =>
        AffectionManager.Instance != null ? AffectionManager.Instance.GetAffection(npc) : 0;

    public int CurrentLevel =>
        tierTable != null ? tierTable.GetLevel(CurrentPoints) : 1;

    // ── 진열 목록 ──
    /// <summary>해당 탭(카테고리)의 진열 항목 — 호감도 해금 반영.</summary>
    public List<ShopEntry> GetDisplay(ITEM_CATEGORY category)
    {
        return stock != null ? stock.GetEntries(category, CurrentLevel) : new List<ShopEntry>();
    }

    // ── 가격 ──
    /// <summary>할인 적용된 구매가.</summary>
    public int GetBuyPrice(ShopEntry entry)
    {
        if (entry == null) return 0;
        return tierTable != null ? tierTable.ApplyDiscount(entry.BasePrice, CurrentLevel) : entry.BasePrice;
    }

    /// <summary>판매가 (기본가 × sellRatio). 진열에 없으면 item.price 기준.</summary>
    public int GetSellPrice(ItemData item)
    {
        if (item == null) return 0;
        ShopEntry e = stock != null ? stock.Find(item) : null;
        int basePrice = e != null ? e.BasePrice : item.price;
        return Mathf.RoundToInt(basePrice * sellRatio);
    }

    /// <summary>현재 남은 재고. -1이면 무한.</summary>
    public int GetStock(ShopEntry entry)
    {
        if (entry == null) return 0;
        if (entry.stock < 0) return -1; // 무한
        if (_runtimeStock.TryGetValue(entry.item, out int s)) return s;
        return entry.stock;
    }

    // ── 구매 ──
    public ShopResult TryBuy(ItemData item)
    {
        if (item == null || stock == null) return ShopResult.Invalid;
        ShopEntry entry = stock.Find(item);
        if (entry == null) return ShopResult.Invalid;

        // 재고 체크 (유한일 때만)
        int remain = GetStock(entry);
        if (remain == 0) return ShopResult.OutOfStock;

        int price = GetBuyPrice(entry);

        if (InventoryManager.Instance == null) return ShopResult.Invalid;
        if (!InventoryManager.Instance.SpendGold(price)) return ShopResult.NotEnoughGold;

        InventoryManager.Instance.AddItem(item, 1);

        // 유한 재고 차감
        if (entry.stock >= 0)
            _runtimeStock[item] = Mathf.Max(0, remain - 1);

        OnShopChanged?.Invoke();
        return ShopResult.Success;
    }

    // ── 판매 ──
    public ShopResult TrySell(ItemData item)
    {
        if (item == null || InventoryManager.Instance == null) return ShopResult.Invalid;
        if (InventoryManager.Instance.GetCount(item) <= 0) return ShopResult.NoneOwned;

        if (!InventoryManager.Instance.RemoveItem(item, 1)) return ShopResult.NoneOwned;

        int gain = GetSellPrice(item);
        InventoryManager.Instance.AddGold(gain);

        OnShopChanged?.Invoke();
        return ShopResult.Success;
    }
}
