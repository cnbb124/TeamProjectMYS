/*
 * [HangarItemSlot]
 * 격납고 파츠 리스트의 한 칸(아이템 슬롯). PartData를 표시하고, 드래그로 노드에 끌어다 장착.
 *
 * [프리팹 구조]
 * ItemSlot (이 스크립트 부착, 루트에 raycastTarget 켜진 Image 필요)
 *   ├ Icon (Image)        — 파츠 아이콘
 *   ├ NameText (TMP)      — 파츠 이름
 *   └ CountText (TMP)     — 보유 수량(1개면 숨김)
 *
 * [드래그 장착]
 * 이 슬롯을 잡아 노드(HangarNodeDropTarget)로 드래그 → 드롭하면 그 부위에 장착.
 * 드래그 중인 파츠는 DraggedPart(static)로 노출 — 노드 드롭 타겟이 읽어감.
 */

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class HangarItemSlot : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image    icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text countText;

    /// <summary>이 슬롯이 표시 중인 파츠 데이터.</summary>
    public PartData Data { get; private set; }

    /// <summary>현재 이 파츠가 기체에 장착된 것인지 (여분과 구분용).</summary>
    public bool IsEquipped { get; private set; }

    // 드래그 중인 파츠 — 노드 드롭 타겟이 읽는다. (드래그는 한 번에 하나뿐이라 static)
    public static PartData DraggedPart { get; private set; }

    private static GameObject _ghost;      // 커서 따라다니는 반투명 아이콘
    private static Canvas     _rootCanvas;
    private CanvasGroup       _canvasGroup;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (_rootCanvas == null)  _rootCanvas  = GetComponentInParent<Canvas>();
    }

    /// <summary>PartData를 받아 슬롯 한 칸을 채움. equipped면 이름에 "(장착됨)" 표시.</summary>
    public void Setup(PartData data, int count = 1, bool equipped = false)
    {
        Data = data;
        IsEquipped = equipped;

        if (data == null)
        {
            if (icon != null)      { icon.sprite = null; icon.enabled = false; }
            if (nameText != null)  nameText.text  = "";
            if (countText != null) countText.text = "";
            return;
        }

        if (icon != null)
        {
            icon.sprite  = data.icon;
            icon.enabled = data.icon != null;
        }
        if (nameText != null)
            nameText.text = equipped ? $"{data.itemName}  (장착됨)" : data.itemName;
        if (countText != null) countText.text = count > 1 ? $"x{count}" : "";
    }

    // ── 드래그 ────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData e)
    {
        if (Data == null) { e.pointerDrag = null; return; }

        DraggedPart = Data;
        _canvasGroup.blocksRaycasts = false;   // 커서 밑 노드가 드롭을 받도록 이 슬롯은 레이캐스트 비활성
        _canvasGroup.alpha          = 0.5f;

        // 커서 따라다닐 고스트 아이콘
        _ghost = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
        Image gi = _ghost.GetComponent<Image>();
        gi.sprite        = Data.icon;
        gi.raycastTarget = false;
        gi.preserveAspect = true;
        _ghost.transform.SetParent(_rootCanvas.transform, false);
        _ghost.transform.SetAsLastSibling();
        _ghost.GetComponent<RectTransform>().sizeDelta = new Vector2(64f, 64f);
    }

    public void OnDrag(PointerEventData e)
    {
        if (_ghost == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvas.transform as RectTransform, e.position,
            _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera,
            out Vector2 pos);
        _ghost.GetComponent<RectTransform>().anchoredPosition = pos;
    }

    public void OnEndDrag(PointerEventData e)
    {
        // OnDrop(노드)이 이 시점보다 먼저 실행되므로 DraggedPart는 드롭에서 이미 읽힘.
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha          = 1f;

        if (_ghost != null) { Destroy(_ghost); _ghost = null; }
        DraggedPart = null;
    }
}
