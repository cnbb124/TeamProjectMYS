/*
 * [RadialQuickSlotUI]
 * 원형(방사형) 퀵슬롯 UI — 아이템/스킬 슬롯 4~6개 대응.
 * 두 가지 모드 지원 (인스펙터 rotateMode로 전환):
 *   - 고정 모드 (어크 쉐도우식) : 슬롯 위치 고정, 선택 슬롯만 확대+하이라이트
 *   - 리볼버 모드 (팀장 픽)     : 실린더처럼 컨테이너가 회전해 선택 슬롯이 항상 맨 위로
 *
 * [씬 구성]
 * QuickSlotUI (이 스크립트 부착)
 *  └ Container (빈 RectTransform, 중앙 피벗)   ← container 연결
 *      └ (슬롯들은 슬롯 프리팹으로 런타임 자동 생성 — 개수는 slotCount)
 *
 * [슬롯 프리팹 구조]
 * Slot (RectTransform, 앵커/피벗 중앙)
 *  ├ Frame (Image)  — 슬롯 배경/테두리
 *  ├ Icon  (Image)  — 아이템/스킬 아이콘 (이름 "Icon" 필수)
 *  └ Count (TMP)    — 수량 (이름 "Count", 없어도 됨)
 *
 * [외부 API]
 * SetSlot(index, sprite, count)  : 슬롯 아이콘/수량 세팅 (QuickSlot 데이터 연동용)
 * SelectNext() / SelectPrev()    : 선택 이동 (R키 등에서 호출)
 * CurrentIndex                   : 현재 선택 슬롯 (T키 사용 처리에서 참조)
 *
 * [입력]
 * useInputManager 체크 시 InputManager.switchConsumable(R키)로 자동 전환.
 * (사용(T키) 처리는 QuickSlot 로직 쪽 담당 — 이 UI는 표시/선택만)
 */

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RadialQuickSlotUI : MonoBehaviour
{
    public enum Mode { Fixed, Revolver }

    [Header("References")]
    [SerializeField] private RectTransform container;   // 슬롯들이 붙을 중앙 피벗
    [SerializeField] private GameObject    slotPrefab;  // 슬롯 프리팹

    [Header("구성")]
    [Range(2, 8)]
    [SerializeField] private int   slotCount = 4;       // 4 = 다이아몬드, 6 = 육각
    [SerializeField] private float radius    = 70f;     // 중심에서 슬롯까지 거리
    [SerializeField] private Mode  rotateMode = Mode.Revolver; // 고정(어크) / 리볼버(팀장픽)

    [Header("선택 연출")]
    [SerializeField] private float selectedScale   = 1.25f; // 선택 슬롯 확대 배율
    [SerializeField] private float normalScale     = 1f;
    [SerializeField] private Color selectedColor   = Color.white;
    [SerializeField] private Color normalColor     = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private float rotateSpeed     = 10f;   // 리볼버 회전 부드러움

    [Header("입력")]
    [SerializeField] private bool useInputManager = true;   // R키(switchConsumable)로 전환

    /// <summary>현재 선택된 슬롯 인덱스.</summary>
    public int CurrentIndex { get; private set; }

    /// <summary>선택이 바뀔 때 발행 (사운드/로직 연동용).</summary>
    public event Action<int> onSelectionChanged;

    private RectTransform[] _slots;
    private Image[]         _frames;
    private Image[]         _icons;
    private TMP_Text[]      _counts;

    private float _targetAngle;   // 리볼버 목표 회전각
    private float _currentAngle;  // 현재 회전각 (Lerp용)

    private void Start()
    {
        BuildSlots();
        ApplySelection(instant: true);
    }

    private void Update()
    {
        // R키 전환 (InputManager 경유)
        if (useInputManager && InputManager.Instance != null && InputManager.Instance.switchConsumable)
            SelectNext();

        // 리볼버 회전 애니메이션
        if (rotateMode == Mode.Revolver && container != null)
        {
            _currentAngle = Mathf.LerpAngle(_currentAngle, _targetAngle, rotateSpeed * Time.deltaTime);
            container.localRotation = Quaternion.Euler(0f, 0f, _currentAngle);

            // 아이콘 역회전 — 컨테이너가 돌아도 슬롯 내용물은 항상 똑바로
            foreach (RectTransform slot in _slots)
                if (slot != null) slot.localRotation = Quaternion.Euler(0f, 0f, -_currentAngle);
        }
    }

    // ── 슬롯 생성/배치 ──
    private void BuildSlots()
    {
        if (container == null || slotPrefab == null) return;

        // 기존 자식 정리 (에디터에서 미리 넣어둔 것 제거)
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        _slots  = new RectTransform[slotCount];
        _frames = new Image[slotCount];
        _icons  = new Image[slotCount];
        _counts = new TMP_Text[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            GameObject go = Instantiate(slotPrefab, container);
            RectTransform rt = go.GetComponent<RectTransform>();

            // 원형 배치 — 0번이 맨 위(12시), 시계방향
            float angle = 360f / slotCount * i;
            float rad = angle * Mathf.Deg2Rad;
            rt.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * radius;

            _slots[i]  = rt;
            _frames[i] = go.GetComponent<Image>();
            _icons[i]  = go.transform.Find("Icon")?.GetComponent<Image>();
            _counts[i] = go.transform.Find("Count")?.GetComponent<TMP_Text>();
        }
    }

    // ── 외부 API ──

    /// <summary>슬롯 아이콘/수량 세팅. QuickSlot 데이터 연동 시 사용.</summary>
    public void SetSlot(int index, Sprite icon, int count = -1)
    {
        if (_icons == null || index < 0 || index >= slotCount) return;

        if (_icons[index] != null)
        {
            _icons[index].sprite  = icon;
            _icons[index].enabled = icon != null;
        }
        if (_counts[index] != null)
            _counts[index].text = count > 0 ? count.ToString() : "";
    }

    /// <summary>다음 슬롯 선택 (R키).</summary>
    public void SelectNext() { Select((CurrentIndex + 1) % slotCount); }

    /// <summary>이전 슬롯 선택.</summary>
    public void SelectPrev() { Select((CurrentIndex - 1 + slotCount) % slotCount); }

    /// <summary>특정 슬롯 선택.</summary>
    public void Select(int index)
    {
        if (index == CurrentIndex || index < 0 || index >= slotCount) return;
        CurrentIndex = index;
        ApplySelection(instant: false);
        onSelectionChanged?.Invoke(CurrentIndex);
    }

    // ── 선택 반영 ──
    private void ApplySelection(bool instant)
    {
        if (_slots == null) return;

        // 하이라이트: 선택 슬롯 확대 + 밝게, 나머지 축소 + 반투명
        for (int i = 0; i < slotCount; i++)
        {
            bool selected = (i == CurrentIndex);
            if (_slots[i] != null)
                _slots[i].localScale = Vector3.one * (selected ? selectedScale : normalScale);
            if (_frames[i] != null)
                _frames[i].color = selected ? selectedColor : normalColor;
        }

        // 리볼버: 선택 슬롯이 맨 위(12시)로 오도록 컨테이너 회전
        if (rotateMode == Mode.Revolver)
        {
            _targetAngle = 360f / slotCount * CurrentIndex; // 슬롯 각도만큼 반대로 돌리면 맨 위로
            if (instant)
            {
                _currentAngle = _targetAngle;
                if (container != null)
                    container.localRotation = Quaternion.Euler(0f, 0f, _currentAngle);
            }
        }
    }
}
