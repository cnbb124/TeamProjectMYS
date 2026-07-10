/*
 * [SkillQuickSlotUI]
 * 스킬 전용 리볼버 퀵슬롯. 슬롯들이 실린더처럼 회전해 선택 스킬이 항상 맨 위(12시)로 옴.
 * 슬롯 수 = skills 리스트 길이 (가변). 아이콘은 SkillData.icon에서 자동.
 *
 * [씬 구성]
 * SkillQuickSlot (이 스크립트 부착)
 *  └ Container (빈 RectTransform, 앵커·피벗 중앙)   ← container 연결
 *      (슬롯은 slotPrefab으로 런타임 자동 생성)
 *
 * [슬롯 프리팹]
 * Slot(Image=프레임)
 *  ├ Icon     (Image, 이름 "Icon")
 *  └ Cooldown (Image, 이름 "Cooldown", 선택) — Image Type=Filled/Radial360 권장.
 *              쿨다운 남은 비율만큼 덮는 오버레이 (반투명 검정 등)
 *
 * [데이터]
 * 인스펙터 skills 리스트에 ActiveSkillData 등록.
 * 런타임: SetSkill(index, skill) / CurrentSkill / StartCooldown(index)
 *
 * [입력]
 * 스킬 전환 키는 InputManager 통합 전 — TODO 주석 위치에 연결 예정.
 * 발동은 외부(SkillSystem)에서 CurrentSkill 읽어 처리 → 성공 시 StartCooldown 호출.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SkillQuickSlotUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform container;
    [SerializeField] private GameObject    slotPrefab;

    [Header("스킬 목록 (슬롯 수 = 리스트 길이)")]
    [SerializeField] private List<ActiveSkillData> skills = new List<ActiveSkillData>();

    [Header("배치/회전")]
    [SerializeField] private float radius      = 70f;
    [Tooltip("인접 슬롯 사이 간격(px). 0보다 크면 radius 무시하고 이 간격 기준으로 반지름 자동 계산")]
    [SerializeField] private float slotSpacing = 0f;
    [SerializeField] private float rotateSpeed = 10f;

    [Header("선택 연출")]
    [SerializeField] private float selectedScale = 1.25f;
    [SerializeField] private float normalScale   = 1f;
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color normalColor   = new Color(1f, 1f, 1f, 150f / 255f);

    /// <summary>현재 선택 슬롯 인덱스.</summary>
    public int CurrentIndex { get; private set; }

    /// <summary>현재 선택된 스킬 (SkillSystem 발동 연동용).</summary>
    public ActiveSkillData CurrentSkill =>
        (CurrentIndex >= 0 && CurrentIndex < skills.Count) ? skills[CurrentIndex] : null;

    /// <summary>선택 변경 시 발행.</summary>
    public event Action<int> onSelectionChanged;

    private RectTransform[] _slots;
    private Image[]         _frames;
    private Image[]         _icons;
    private Image[]         _cooldowns;   // Filled 오버레이 (fillAmount = 남은 쿨다운 비율)
    private float[]         _cdTimers;    // 남은 쿨다운(초)

    private float _targetAngle;
    private float _currentAngle;

    private void Start()
    {
        BuildSlots();
        RefreshAll();
        ApplySelection(instant: true);
    }

    private void Update()
    {
        // TODO: InputManager 스킬 전환 키 통합 후 여기서 SelectNext() 연결

        // 리볼버 회전 + 아이콘 역회전
        if (container != null)
        {
            _currentAngle = Mathf.LerpAngle(_currentAngle, _targetAngle, rotateSpeed * Time.deltaTime);
            container.localRotation = Quaternion.Euler(0f, 0f, _currentAngle);

            foreach (RectTransform slot in _slots)
                if (slot != null) slot.localRotation = Quaternion.Euler(0f, 0f, -_currentAngle);
        }

        // 쿨다운 게이지 갱신
        UpdateCooldowns();
    }

    // ── 슬롯 생성 ──
    private void BuildSlots()
    {
        if (container == null || slotPrefab == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        int count = Mathf.Max(1, skills.Count);
        _slots     = new RectTransform[count];
        _frames    = new Image[count];
        _icons     = new Image[count];
        _cooldowns = new Image[count];
        _cdTimers  = new float[count];

        // slotSpacing 지정 시 인접 슬롯 간격 기준으로 반지름 자동 계산
        // (현의 길이 공식: spacing = 2 × r × sin(π/n) → r = spacing / (2 sin(π/n)))
        float r = slotSpacing > 0f && count > 1
            ? slotSpacing / (2f * Mathf.Sin(Mathf.PI / count))
            : radius;

        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(slotPrefab, container);
            RectTransform rt = go.GetComponent<RectTransform>();

            float rad = (360f / count * i) * Mathf.Deg2Rad;
            rt.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * r;

            _slots[i]     = rt;
            _frames[i]    = go.GetComponent<Image>();
            _icons[i]     = go.transform.Find("Icon")?.GetComponent<Image>();
            _cooldowns[i] = go.transform.Find("Cooldown")?.GetComponent<Image>();

            if (_cooldowns[i] != null) _cooldowns[i].fillAmount = 0f; // 시작은 쿨다운 없음
        }
    }

    // ── 데이터 표시 ──

    /// <summary>모든 슬롯을 skills 리스트 기준으로 다시 그림 (아이콘 = SkillData.icon).</summary>
    public void RefreshAll()
    {
        if (_icons == null) return;
        for (int i = 0; i < _icons.Length; i++)
        {
            ActiveSkillData skill = (i < skills.Count) ? skills[i] : null;
            if (_icons[i] != null)
            {
                _icons[i].sprite  = skill != null ? skill.icon : null;
                _icons[i].enabled = skill != null && skill.icon != null;
            }
        }
    }

    /// <summary>슬롯 스킬 교체 (스킬 배움/변경 시).</summary>
    public void SetSkill(int index, ActiveSkillData skill)
    {
        if (index < 0 || index >= skills.Count) return;
        skills[index] = skill;
        RefreshAll();
    }

    // ── 선택 ──

    public void SelectNext() { Select((CurrentIndex + 1) % Mathf.Max(1, skills.Count)); }
    public void SelectPrev() { Select((CurrentIndex - 1 + skills.Count) % Mathf.Max(1, skills.Count)); }

    public void Select(int index)
    {
        if (index == CurrentIndex || index < 0 || index >= skills.Count) return;
        CurrentIndex = index;
        ApplySelection(instant: false);
        onSelectionChanged?.Invoke(CurrentIndex);
    }

    // ── 쿨다운 ──

    /// <summary>스킬 발동 성공 시 호출 — SkillData.skillCoolDown만큼 쿨다운 게이지 시작.</summary>
    public void StartCooldown(int index)
    {
        if (index < 0 || index >= skills.Count || skills[index] == null) return;
        _cdTimers[index] = skills[index].skillCoolDown;
    }

    /// <summary>해당 슬롯이 쿨다운 중인지 (발동 가능 여부 체크용).</summary>
    public bool IsOnCooldown(int index)
    {
        return index >= 0 && index < _cdTimers.Length && _cdTimers[index] > 0f;
    }

    private void UpdateCooldowns()
    {
        if (_cdTimers == null) return;

        for (int i = 0; i < _cdTimers.Length; i++)
        {
            if (_cdTimers[i] <= 0f) continue;

            _cdTimers[i] -= Time.deltaTime;
            if (_cdTimers[i] < 0f) _cdTimers[i] = 0f;

            // 남은 비율만큼 오버레이 채움 (다 돌면 0 = 사라짐)
            if (_cooldowns[i] != null && skills[i] != null && skills[i].skillCoolDown > 0f)
                _cooldowns[i].fillAmount = _cdTimers[i] / skills[i].skillCoolDown;
        }
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

        _targetAngle = 360f / Mathf.Max(1, skills.Count) * CurrentIndex;
        if (instant)
        {
            _currentAngle = _targetAngle;
            if (container != null)
                container.localRotation = Quaternion.Euler(0f, 0f, _currentAngle);
        }
    }
}
