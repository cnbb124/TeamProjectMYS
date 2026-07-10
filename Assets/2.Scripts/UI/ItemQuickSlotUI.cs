/*
 * [ItemQuickSlotUI]
 * 아이템 전용 리볼버 퀵슬롯. 슬롯들이 실린더처럼 회전해 선택 아이템이 항상 맨 위(12시)로 옴.
 * 슬롯 수 = items 리스트 길이 (가변).
 *
 * [씬 구성]
 * ItemQuickSlot (이 스크립트 부착)
 *  └ Container (빈 RectTransform, 앵커·피벗 중앙)   ← container 연결
 *      (슬롯은 slotPrefab으로 런타임 자동 생성)
 *
 * [슬롯 프리팹] Slot(Image=프레임) ├ Icon(Image, 이름"Icon") └ Count(TMP, 이름"Count")
 *
 * [데이터]
 * 인스펙터 items 리스트에 ItemData 등록 (아이콘/수량 자동 표시)
 * 런타임: SetItem(index, item, count) / SetCount(index, count) / CurrentItem
 *
 * [입력]
 * useInputManager 체크 시 R키(InputManager.switchConsumable)로 전환.
 * 사용(T키)은 외부에서 CurrentItem 읽어 처리 → UseCurrent() 호출하면 플래시 연출.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemQuickSlotUI : MonoBehaviour
{
    [System.Serializable]
    public class ItemEntry
    {
        public ItemData item;
        public int      count = 1;
    }

    [Header("References")]
    [SerializeField] private RectTransform container;
    [SerializeField] private GameObject    slotPrefab;

    [Header("아이템 목록 (슬롯 수 = 리스트 길이)")]
    [SerializeField] private List<ItemEntry> items = new List<ItemEntry>();

    [Header("배치/회전")]
    [SerializeField] private float radius      = 70f;
    [SerializeField] private float rotateSpeed = 10f;   // 리볼버 회전 부드러움

    [Header("선택 연출")]
    [SerializeField] private float selectedScale = 1.25f;
    [SerializeField] private float normalScale   = 1f;
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color normalColor   = new Color(1f, 1f, 1f, 150f / 255f);
    [SerializeField] private Color flashColor    = new Color(0.4f, 1f, 0.6f, 1f); // 사용 플래시
    [SerializeField] private float flashTime     = 0.15f;

    [Header("입력")]
    [SerializeField] private bool useInputManager = true; // R키(switchConsumable) 전환

    /// <summary>현재 선택 슬롯 인덱스.</summary>
    public int CurrentIndex { get; private set; }

    /// <summary>현재 선택된 아이템 (사용 로직에서 참조).</summary>
    public ItemData CurrentItem =>
        (CurrentIndex >= 0 && CurrentIndex < items.Count) ? items[CurrentIndex].item : null;

    /// <summary>선택 변경 시 발행.</summary>
    public event Action<int> onSelectionChanged;

    private RectTransform[] _slots;
    private Image[]         _frames;
    private Image[]         _icons;
    private TMP_Text[]      _counts;

    private float _targetAngle;
    private float _currentAngle;
    private float _flashTimer;

    private void Start()
    {
        BuildSlots();
        RefreshAll();
        ApplySelection(instant: true);
    }

    private void Update()
    {
        // R키 전환
        if (useInputManager && InputManager.Instance != null && InputManager.Instance.switchConsumable)
            SelectNext();

        // 리볼버 회전 + 아이콘 역회전 (내용물 항상 똑바로)
        if (container != null)
        {
            _currentAngle = Mathf.LerpAngle(_currentAngle, _targetAngle, rotateSpeed * Time.deltaTime);
            container.localRotation = Quaternion.Euler(0f, 0f, _currentAngle);

            foreach (RectTransform slot in _slots)
                if (slot != null) slot.localRotation = Quaternion.Euler(0f, 0f, -_currentAngle);
        }

        // 사용 플래시 페이드
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(_flashTimer / flashTime);
            if (_frames != null && CurrentIndex < _frames.Length && _frames[CurrentIndex] != null)
                _frames[CurrentIndex].color = Color.Lerp(selectedColor, flashColor, t);
        }
    }

    // ── 슬롯 생성 (원형 배치, 0번이 12시) ──
    private void BuildSlots()
    {
        if (container == null || slotPrefab == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        int count = Mathf.Max(1, items.Count);
        _slots  = new RectTransform[count];
        _frames = new Image[count];
        _icons  = new Image[count];
        _counts = new TMP_Text[count];

        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(slotPrefab, container);
            RectTransform rt = go.GetComponent<RectTransform>();

            float rad = (360f / count * i) * Mathf.Deg2Rad;
            rt.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * radius;

            _slots[i]  = rt;
            _frames[i] = go.GetComponent<Image>();
            _icons[i]  = go.transform.Find("Icon")?.GetComponent<Image>();
            _counts[i] = go.transform.Find("Count")?.GetComponent<TMP_Text>();
        }
    }

    // ── 데이터 표시 ──

    /// <summary>모든 슬롯을 items 리스트 기준으로 다시 그림.</summary>
    public void RefreshAll()
    {
        if (_icons == null) return;
        for (int i = 0; i < _icons.Length; i++)
            RefreshSlot(i);
    }

    private void RefreshSlot(int i)
    {
        ItemData item = (i < items.Count) ? items[i].item : null;
        int count     = (i < items.Count) ? items[i].count : 0;

        if (_icons[i] != null)
        {
            _icons[i].sprite  = item != null ? item.icon : null;
            _icons[i].enabled = item != null && item.icon != null;
        }
        if (_counts[i] != null)
            _counts[i].text = (item != null && count > 1) ? count.ToString() : "";
    }

    /// <summary>슬롯 아이템 교체 (획득/장착 변경 시).</summary>
    public void SetItem(int index, ItemData item, int count = 1)
    {
        if (index < 0 || index >= items.Count) return;
        items[index].item  = item;
        items[index].count = count;
        RefreshSlot(index);
    }

    /// <summary>수량만 갱신 (사용/획득 시).</summary>
    public void SetCount(int index, int count)
    {
        if (index < 0 || index >= items.Count) return;
        items[index].count = count;
        RefreshSlot(index);
    }

    // ── 선택/사용 ──

    public void SelectNext() { Select((CurrentIndex + 1) % Mathf.Max(1, items.Count)); }
    public void SelectPrev() { Select((CurrentIndex - 1 + items.Count) % Mathf.Max(1, items.Count)); }

    public void Select(int index)
    {
        if (index == CurrentIndex || index < 0 || index >= items.Count) return;
        CurrentIndex = index;
        ApplySelection(instant: false);
        onSelectionChanged?.Invoke(CurrentIndex);
    }

    /// <summary>현재 아이템 사용 연출(플래시). 실제 소모 로직은 외부에서 CurrentItem으로 처리.</summary>
    public void UseCurrent()
    {
        _flashTimer = flashTime;
    }

    private void ApplySelection(bool instant)
    {
        if (_slots == null) return;

        for (int i = 0; i < _slots.Length; i++)
        {
            bool selected = (i == CurrentIndex);
            if (_slots[i]  != null) _slots[i].localScale = Vector3.one * (selected ? selectedScale : normalScale);
            if (_frames[i] != null) _frames[i].color     = selected ? selectedColor : normalColor;
        }

        // 선택 슬롯이 맨 위(12시)로 오도록 회전
        _targetAngle = 360f / Mathf.Max(1, items.Count) * CurrentIndex;
        if (instant)
        {
            _currentAngle = _targetAngle;
            if (container != null)
                container.localRotation = Quaternion.Euler(0f, 0f, _currentAngle);
        }
    }
}
