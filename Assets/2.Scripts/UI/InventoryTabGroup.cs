using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인벤토리 상단 탭 (PARTS / CONSUMABLE / MATERIAL) 전환 + 그리드 갱신.
/// InventoryPanel에 부착.
/// 탭 전환 시 InventoryManager.GetAllOfCategory()로 해당 탭 아이템을 슬롯에 그림.
/// </summary>
public class InventoryTabGroup : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        public Button        button;     // 탭 버튼
        public GameObject    panel;      // 해당 콘텐츠 패널
        public TMP_Text      label;      // 버튼 텍스트 (색상 변경용)
        public ITEM_CATEGORY category;   // 이 탭이 보여줄 카테고리
        public InvSlot[]     slots;      // 패널 안 슬롯들 (그리드 순서대로)
    }

    [Header("Tabs")]
    [SerializeField] private Tab[] tabs;
    [SerializeField] private int   defaultTab = 0;

    [Header("Colors")]
    [SerializeField] private Color activeColor   = new Color(0f, 1f, 0.8f, 1f);
    [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0.45f);

    [Header("Underline (선택)")]
    [SerializeField] private RectTransform underline;
    [SerializeField] private float underlineMoveSpeed = 12f;

    private int _currentIndex;
    private Vector2 _underlineTarget;

    private void Awake()
    {
        Debug.Log($"[Tab] Awake 호출 / tabs.Length={tabs.Length}");
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            if (tabs[i].button != null)
            {
                tabs[i].button.onClick.AddListener(() => SelectTab(index));
                Debug.Log($"[Tab] 버튼 {i} 리스너 등록 완료");
            }
            else
            {
                Debug.LogWarning($"[Tab] 버튼 {i} 가 null!");
            }
        }
    }

    private void Start()
    {
        SelectTab(defaultTab);
    }

    private void OnEnable()
    {
        // 패널 열 때마다 현재 탭 갱신 (아이템 획득/소모 반영)
        RefreshGrid(_currentIndex);
    }

    private void Update()
    {
        if (underline != null)
            underline.anchoredPosition = Vector2.Lerp(
                underline.anchoredPosition, _underlineTarget,
                underlineMoveSpeed * Time.deltaTime);
    }

    public void SelectTab(int index)
    {
        Debug.Log($"[Tab] SelectTab({index}) 호출");
        if (index < 0 || index >= tabs.Length) return;
        _currentIndex = index;

        for (int i = 0; i < tabs.Length; i++)
        {
            bool isActive = (i == index);

            if (tabs[i].panel != null)
                tabs[i].panel.SetActive(isActive);

            if (tabs[i].label != null)
                tabs[i].label.color = isActive ? activeColor : inactiveColor;
        }

        RefreshGrid(index);

        if (underline != null && tabs[index].button != null)
        {
            RectTransform btnRect = tabs[index].button.GetComponent<RectTransform>();
            _underlineTarget = new Vector2(
                btnRect.anchoredPosition.x,
                underline.anchoredPosition.y);
            underline.sizeDelta = new Vector2(btnRect.sizeDelta.x, underline.sizeDelta.y);
        }
    }

    /// <summary>
    /// InventoryManager에서 해당 탭 카테고리 아이템을 받아 슬롯에 채움.
    /// 아이템 추가/제거 후 외부에서 호출해도 됨.
    /// </summary>
    public void RefreshGrid(int index)
{
    if (index < 0 || index >= tabs.Length) return;

    Tab tab = tabs[index];
    if (tab.slots == null || tab.slots.Length == 0) return;
    if (InventoryManager.Instance == null) return;

    // null 데이터 항목 걸러내며 직접 필터링 (팀장님이 테스트용으로
    // 비워둔 ItemStack이 있어도 죽지 않도록 GetAllOfCategory 대신 사용)
    List<ItemStack> list = new List<ItemStack>();
    foreach (ItemStack stack in InventoryManager.Instance.items)
    {
        if (stack != null && stack.data != null && stack.data.category == tab.category)
            list.Add(stack);
    }

    for (int i = 0; i < tab.slots.Length; i++)
    {
        if (tab.slots[i] == null) continue;

        if (i < list.Count)
            tab.slots[i].SetItem(list[i].data, list[i].count);
        else
            tab.slots[i].ClearSlot();
    }
}

    /// <summary>현재 보고 있는 탭 갱신 (아이템 사용/획득 직후 호출용)</summary>
    public void RefreshCurrent() => RefreshGrid(_currentIndex);
}