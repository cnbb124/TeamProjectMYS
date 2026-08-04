/*
 * [HangarNodeDropTarget]
 * 격납고 파츠 노드(버튼)에 붙어, (1) 현재 장착된 파츠 아이콘을 노드에 표시하고
 * (2) 리스트에서 드래그해온 파츠를 받는 드롭 타겟 역할을 한다.
 * 드롭되면 자기 partType과 함께 onDropPart 이벤트를 발행 → 컨트롤러가 장착 처리.
 *
 * [부착]
 * 보통 HangarEquipController가 런타임에 각 노드 버튼에 자동 부착하고 partType을 채운다
 * (PartNodeConnector의 노드↔앵커 짝에서 앵커.partType을 가져옴).
 * 수동으로 붙일 때는 partType을 직접 지정하면 됨.
 *
 * [장착 아이콘]
 * ShowEquipped(part) 호출 시, 노드 안에 "EquippedIcon" 자식 Image를 자동 생성/갱신해
 * 장착된 파츠 아이콘을 띄운다. 이 아이콘은 raycastTarget이 꺼져 있어 드롭을 가로채지 않는다.
 *
 * ※ 노드 버튼에 raycastTarget 켜진 Image가 있어야 드롭이 잡힘(Button의 기본 Image면 OK).
 */

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class HangarNodeDropTarget : MonoBehaviour, IDropHandler
{
    /// <summary>이 노드가 담당하는 파츠 종류.</summary>
    public PART_TYPE partType;

    /// <summary>파츠가 드롭됐을 때 발행 — (노드 partType, 드롭된 파츠).</summary>
    public event Action<PART_TYPE, PartData> onDropPart;

    [Tooltip("노드 크기 대비 아이콘이 차지할 비율 여백(px). 0이면 노드에 꽉 참")]
    public float iconInset = 8f;

    private Image _iconImage;   // 장착 파츠 아이콘 (자동 생성)

    public void OnDrop(PointerEventData eventData)
    {
        PartData dropped = HangarItemSlot.DraggedPart;
        if (dropped != null)
            onDropPart?.Invoke(partType, dropped);
    }

    /// <summary>이 노드에 장착된 파츠 아이콘을 표시. null이면 아이콘을 숨김(빈 슬롯).</summary>
    public void ShowEquipped(PartData part)
    {
        if (_iconImage == null) _iconImage = CreateIconChild();

        Sprite sprite = part != null ? part.icon : null;
        _iconImage.sprite  = sprite;
        _iconImage.enabled = sprite != null;
    }

    // 노드 안에 아이콘용 Image 자식을 하나 만든다 (드롭을 막지 않도록 raycastTarget 끔).
    private Image CreateIconChild()
    {
        GameObject go = new GameObject("EquippedIcon", typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;                 // 노드에 꽉 채우고 inset만큼 안쪽으로
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(iconInset, iconInset);
        rt.offsetMax = new Vector2(-iconInset, -iconInset);
        rt.SetAsLastSibling();                        // 노드 배경 위에 그려지도록

        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;                    // 드롭은 밑의 노드 버튼이 받도록
        img.preserveAspect = true;
        return img;
    }
}
