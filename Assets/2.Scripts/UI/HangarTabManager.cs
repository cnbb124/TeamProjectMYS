/*
 * [HangarTabManager]
 * 격납고(HomeBase) 상단 메뉴바 탭 전환 관리자.
 * 탭 버튼을 누르면 해당 콘텐츠 패널만 켜고 나머지는 끔.
 *
 * [부착 위치]
 * MenuBarPanel(또는 HangarUI 루트)에 부착.
 *
 * [사용법]
 * 1. tabs 리스트에 (버튼 + 콘텐츠 패널) 쌍을 등록
 *    - 예: Hangar 버튼 ↔ HangarSlot 패널
 *    - Storage/Store/Quest는 나중에 content 연결되면 자동 동작 (지금은 비워둬도 됨)
 * 2. defaultTab : 시작 시 열어둘 탭 인덱스 (Hangar = 0)
 * 3. 버튼 OnClick은 비워둠 — Start에서 자동으로 리스너 연결함
 *
 * [확장]
 * Storage/Store/Quest 콘텐츠 만들면 tabs에 추가만 하면 됨. 코드 수정 불필요.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HangarTabManager : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        public string     name;        // 식별용 (Hangar/Storage/...)
        public Button     button;      // 탭 버튼
        public GameObject content;     // 켜고 끌 콘텐츠 패널 (비어있으면 전환만 하고 표시 X)
        public TMP_Text   label;       // (선택) 활성/비활성 색 바꿀 텍스트
    }

    [Header("Tabs")]
    [SerializeField] private List<Tab> tabs = new List<Tab>();
    [SerializeField] private int defaultTab = 0;

    [Header("Label Colors (선택)")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.6f, 0.1f, 1f); // 주황 (활성)
    [SerializeField] private Color normalColor   = new Color(1f, 1f, 1f, 0.6f);   // 흐린 흰색 (비활성)

    private int _current = -1;

    private void Start()
    {
        // 버튼마다 클릭 리스너 연결
        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i; // 클로저 캡쳐 주의 — 지역 변수로 복사
            if (tabs[i].button != null)
                tabs[i].button.onClick.AddListener(() => SelectTab(index));
        }

        // 시작 탭 열기
        int start = Mathf.Clamp(defaultTab, 0, tabs.Count - 1);
        SelectTab(start);
    }

    /// <summary>탭 선택 — 해당 콘텐츠만 켜고 나머지는 끔.</summary>
    public void SelectTab(int index)
    {
        if (index < 0 || index >= tabs.Count) return;
        _current = index;

        for (int i = 0; i < tabs.Count; i++)
        {
            bool isOn = (i == index);

            if (tabs[i].content != null)
                tabs[i].content.SetActive(isOn);

            if (tabs[i].label != null)
                tabs[i].label.color = isOn ? selectedColor : normalColor;
        }
    }

    /// <summary>현재 선택된 탭 인덱스.</summary>
    public int CurrentTab => _current;
}
