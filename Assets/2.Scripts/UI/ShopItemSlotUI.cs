/*
 * [ShopItemSlotUI]
 * 호감도 상점의 아이템 카드(버튼) 한 칸. ShopEntry/ItemData를 받아 UI를 채우고, 클릭 시 콜백 실행(구매/판매).
 *
 * [프리팹 구조] ItemSlot (Button + 이 스크립트)
 *   ├ Image       — 아이콘
 *   ├ ItemName    — 이름 (TMP)
 *   ├ Description — 설명 (TMP)
 *   └ Value       — 가격 (TMP)
 *
 * [사용법]
 * 목록 매니저(ShopListUI)가 프리팹을 Instantiate한 뒤 Setup(...) 호출.
 * 클릭 콜백으로 어떤 아이템인지(Item) 넘겨서 매니저가 TryBuy/TrySell 처리.
 */

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopItemSlotUI : MonoBehaviour
{
    [SerializeField] private Image    icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Button   button;   // ItemSlot 버튼 (비우면 자동 GetComponent)

    public ItemData Item { get; private set; }
    private Action<ItemData> _onClick;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
    }

    /// <summary>
    /// 슬롯 한 칸을 데이터로 채움.
    /// </summary>
    /// <param name="item">표시할 아이템</param>
    /// <param name="price">표시할 가격 (할인 적용가 또는 판매가)</param>
    /// <param name="onClick">클릭 시 호출(어떤 아이템인지 전달) — 구매/판매 처리용</param>
    public void Setup(ItemData item, int price, Action<ItemData> onClick)
    {
        Item     = item;
        _onClick = onClick;

        if (item == null)
        {
            if (icon != null)      { icon.sprite = null; icon.enabled = false; }
            if (nameText != null)  nameText.text  = "";
            if (descText != null)  descText.text  = "";
            if (valueText != null) valueText.text = "";
            return;
        }

        if (icon != null)
        {
            icon.sprite  = item.icon;
            icon.enabled = item.icon != null;
        }
        if (nameText != null)  nameText.text  = item.itemName;
        if (descText != null)  descText.text  = item.description;
        if (valueText != null) valueText.text = price.ToString();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => _onClick?.Invoke(Item));
        }
    }
}
