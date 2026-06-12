using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인벤토리 상단 탭 (WEAPON / SKILLS / MONEYS / INSPECTOR) 전환 관리.
/// InventoryPanel에 부착.
/// </summary>
public class InventoryTabGroup : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        public Button     button;     // 탭 버튼
        public GameObject panel;      // 해당 콘텐츠 패널
        public TMP_Text   label;      // 버튼 텍스트 (색상 변경용)
    }

    [Header("Tabs")]
    [SerializeField] private Tab[] tabs;
    [SerializeField] private int   defaultTab = 0;

    [Header("Colors")]
    [SerializeField] private Color activeColor   = new Color(0f, 1f, 0.8f, 1f);   // 청록
    [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0.45f);  // 흐린 흰색

    [Header("Underline (선택)")]
    [SerializeField] private RectTransform underline;      // 빛나는 라인
    [SerializeField] private float underlineMoveSpeed = 12f;

    private int _currentIndex;
    private Vector2 _underlineTarget;

    private void Start()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i; // 클로저 캡처용 복사
            if (tabs[i].button != null)
                tabs[i].button.onClick.AddListener(() => SelectTab(index));
        }
        SelectTab(defaultTab);
    }

    private void Update()
    {
        // 언더라인 부드럽게 이동
        if (underline != null)
            underline.anchoredPosition = Vector2.Lerp(
                underline.anchoredPosition, _underlineTarget,
                underlineMoveSpeed * Time.deltaTime);
    }

    public void SelectTab(int index)
    {
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

        // 언더라인을 선택된 탭 버튼 아래로
        if (underline != null && tabs[index].button != null)
        {
            RectTransform btnRect = tabs[index].button.GetComponent<RectTransform>();
            _underlineTarget = new Vector2(
                btnRect.anchoredPosition.x,
                underline.anchoredPosition.y);
            underline.sizeDelta = new Vector2(btnRect.sizeDelta.x, underline.sizeDelta.y);
        }
    }
}