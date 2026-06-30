/*
 * [ShopStock]
 * 호감도 상점의 "판매 진열 목록" SO. 기존 ItemData(PartData/ConsumableData 등)를 재활용하고,
 * 상점 고유 정보(가격 override / 호감도 해금 레벨 / 재고)만 추가로 담는다.
 *
 * [만들기]
 * Project 창 우클릭 → Create → Affinity → Shop Stock
 *
 * [구성]
 * entries: 판매할 항목 목록. 각 항목은 ItemData를 가리키고, 상점 정보를 덧붙임.
 *
 * [탭 필터]
 * 탭(Parts/Consumable/Material)은 item.category로 자동 분류 → 별도 에셋 불필요.
 *
 * [호감도 해금]
 * unlockLevel = 0 이면 항상 진열. 값이 있으면 그 호감도 레벨부터 진열(Breakpoint 평판식).
 */

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ShopEntry
{
    [Tooltip("판매할 아이템 (기존 ItemData 재활용)")]
    public ItemData item;

    [Tooltip("판매가. 0이면 item.price 사용")]
    public int priceOverride = 0;

    [Tooltip("이 호감도 레벨부터 진열. 0이면 항상 진열")]
    public int unlockLevel = 0;

    [Tooltip("재고. -1이면 무한")]
    public int stock = -1;

    /// <summary>실제 판매가 (override 있으면 그 값, 없으면 item.price).</summary>
    public int BasePrice => priceOverride > 0 ? priceOverride : (item != null ? item.price : 0);
}

[CreateAssetMenu(fileName = "ShopStock", menuName = "Affinity/Shop Stock")]
public class ShopStock : ScriptableObject
{
    public List<ShopEntry> entries = new List<ShopEntry>();

    /// <summary>
    /// 특정 카테고리(탭)의 진열 항목 반환.
    /// affinityLevel 이상으로 해금된 것만 포함.
    /// </summary>
    public List<ShopEntry> GetEntries(ITEM_CATEGORY category, int affinityLevel)
    {
        List<ShopEntry> result = new List<ShopEntry>();
        if (entries == null) return result;

        foreach (ShopEntry e in entries)
        {
            if (e == null || e.item == null) continue;        // 빈 항목 방어
            if (e.item.category != category) continue;        // 탭 필터
            if (e.unlockLevel > affinityLevel) continue;      // 호감도 해금 필터
            result.Add(e);
        }
        return result;
    }

    /// <summary>ItemData로 진열 항목 찾기 (구매/판매 시 가격·재고 조회용).</summary>
    public ShopEntry Find(ItemData item)
    {
        if (entries == null || item == null) return null;
        foreach (ShopEntry e in entries)
            if (e != null && e.item == item) return e;
        return null;
    }
}
