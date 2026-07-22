using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Threading;

/// <summary>
/// 인벤토리 슬롯 1칸.
/// WeaponSlot_1~3 / ItemSlot_1~16 오브젝트에 부착.
///
/// [슬롯 카테고리]
///   Weapon : LAUNCHER 계열 PartData만 수용 (WeaponSlot에 사용)
///   Any    : 모든 ItemData 수용            (ItemSlot에 사용)
/// </summary>
public class InvSlot : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public enum SlotCategory { Any, Weapon }

    [Header("슬롯 설정")]
    [SerializeField] public SlotCategory category = SlotCategory.Any;

    [Header("UI References")]
    [SerializeField] private Image    iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Sprite   emptySprite;
    [SerializeField] private Color    emptyColor = new Color(1f, 1f, 1f, 0.2f);

    // 현재 슬롯 아이템
    public ItemData Item { get; private set; }
    public int Count { get; private set; }

    // 드래그 전역 상태
    private static InvSlot _dragSource;
    private static Image   _dragIcon;
    private static Canvas  _rootCanvas;

    // 이번 드래그가 다른 슬롯에서 처리(교환)됐는지.
    // OnDrop이 OnEndDrag보다 먼저 불리므로, 여기서 true면 "밖에 버린 것"이 아니다.
    private static bool _handledBySlot;

    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();
    }

    // ── 아이템 세팅 ───────────────────────────────────────────

    public void SetItem(ItemData newItem, int count = 1)
    {
        Item  = newItem;
     Count = newItem != null ? count : 0;
        Refresh();
    }

    public void ClearSlot()
    {
        Item = null;
        Count = 0;
        Refresh();
    }

    public void Refresh()
    {
        bool has = Item != null;

        if (iconImage != null)
        {
            iconImage.sprite = has ? Item.icon : emptySprite;
            iconImage.color  = has ? Color.white : emptyColor;
        }

        if (nameText != null)
            nameText.text = has ? Item.itemName : string.Empty;

        if (countText != null)
            countText.text = (has && Count > 1) ? Count.ToString() : string.Empty;
    }

    // ── 드래그 시작 ───────────────────────────────────────────

    public void OnBeginDrag(PointerEventData e)
    {
        if (Item == null) { e.pointerDrag = null; return; }

        _dragSource    = this;
        _handledBySlot = false;

        _canvasGroup.alpha          = 0.35f;
        _canvasGroup.blocksRaycasts = false;

        // 커서 따라다닐 아이콘 생성
        _dragIcon = new GameObject("DragIcon").AddComponent<Image>();
        _dragIcon.transform.SetParent(_rootCanvas.transform, false);
        _dragIcon.transform.SetAsLastSibling();
        _dragIcon.sprite        = Item.icon;
        _dragIcon.raycastTarget = false;
        _dragIcon.rectTransform.sizeDelta = new Vector2(64f, 64f);
    }

    // ── 드래그 중 ─────────────────────────────────────────────

    public void OnDrag(PointerEventData e)
    {
        if (_dragIcon == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvas.transform as RectTransform,
            e.position,
            _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera,
            out Vector2 pos);

        _dragIcon.rectTransform.anchoredPosition = pos;
    }

    // ── 드래그 종료 ───────────────────────────────────────────

    public void OnEndDrag(PointerEventData e)
    {
        _canvasGroup.alpha          = 1f;
        _canvasGroup.blocksRaycasts = true;

        if (_dragIcon != null)
        {
            Destroy(_dragIcon.gameObject);
            _dragIcon = null;
        }

        // 다른 슬롯이 받아가지 않았고, 인벤토리 패널 바깥에서 손을 놨으면 월드에 버린다.
        if (!_handledBySlot && Item != null && IsPointerOutsideInventory(e))
        {
            TryDropToWorld();
        }

        _dragSource = null;
    }

    // ── 인벤토리 밖으로 버리기 ────────────────────────────────

    // 인벤토리 패널(InventoryTabGroup이 붙은 오브젝트)의 사각형 밖에서 손을 놨는지.
    // 패널을 못 찾으면 "밖 판정"을 하지 않는다 — 실수로 버려지는 것보다 안 버려지는 게 안전.
    private bool IsPointerOutsideInventory(PointerEventData e)
    {
        InventoryTabGroup group = GetComponentInParent<InventoryTabGroup>();
        if (group == null) return false;

        RectTransform panelRect = group.transform as RectTransform;
        if (panelRect == null) return false;

        Camera cam = (_rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? _rootCanvas.worldCamera : null;

        return !RectTransformUtility.RectangleContainsScreenPoint(panelRect, e.position, cam);
    }

    // 실제 드랍은 InventoryDropper가 담당 (인벤토리 차감 + 월드 생성).
    private void TryDropToWorld()
    {
        if (InventoryDropper.Instance == null)
        {
            Debug.LogWarning("[InvSlot] 씬에 InventoryDropper가 없어 버리기를 건너뜁니다.");
            return;
        }

        if (InventoryDropper.Instance.TryDrop(Item, Count))
        {
            ClearSlot();   // 그리드 전체 갱신은 InventoryTabGroup이 OnInventoryChanged로 처리
        }
    }

    // ── 드롭 수신 ─────────────────────────────────────────────

    public void OnDrop(PointerEventData e)
    {
        if (_dragSource == null || _dragSource == this) return;

        // 이 슬롯이 드롭을 받을 수 있는지 확인
        if (!CanAccept(_dragSource.Item)) return;
        // 출발 슬롯도 교환될 아이템을 받을 수 있는지 확인 (swap)
        if (Item != null && !_dragSource.CanAccept(Item)) return;

        // 두 슬롯 아이템 교환
        ItemData temp = Item;
        SetItem(_dragSource.Item);
        _dragSource.SetItem(temp);

        _handledBySlot = true;   // 슬롯끼리 처리됨 — 밖에 버린 게 아님
    }

    // ── 수용 가능 여부 ────────────────────────────────────────

    /// <summary>
    /// 이 슬롯이 들어오는 아이템을 받을 수 있는지.
    /// - 빈 슬롯이면 카테고리 규칙만 통과하면 허용
    /// - Weapon 슬롯 : LAUNCHER 계열 PartData만 허용
    /// - Any 슬롯    : 모든 ItemData 허용
    /// </summary>
    public bool CanAccept(ItemData incoming)
    {
        if (incoming == null) return false;

        switch (category)
        {
            case SlotCategory.Weapon:
                if (!(incoming is PartData part)) return false;
                return part.partType == PART_TYPE.LAUNCHER_BULLET
                    || part.partType == PART_TYPE.LAUNCHER_MISSILE;

            case SlotCategory.Any:
            default:
                return true;
        }
    }
}
