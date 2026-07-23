/*
 * [ItemQuickSlotUI]
 * 아이템(소모품) 리볼버 퀵슬롯 UI.
 * ★이 스크립트는 "표시 + 입력 전달"만 한다. 실제 데이터와 사용 처리는 전부 Player의 QuickSlot이 담당.
 *
 * [왜 이렇게 바뀌었나]
 * 예전엔 이 UI가 자기만의 items 리스트를 들고 플래시 연출만 했다.
 * 그래서 아이템을 먹어도 실제로 쓰이지 않았고(수량도 안 줄고 효과도 안 남),
 * QuickSlot.UseSlot()이라는 완성된 백엔드가 있는데 아무도 호출하지 않는 상태였다.
 * 이제는 QuickSlot을 그대로 비춰주는 뷰 역할만 한다.
 *
 * [데이터 흐름]
 *   아이템 획득 → InventoryManager.items
 *      → (이 스크립트) 빈 퀵슬롯에 자동 배치 = QuickSlot.AssignSlot()
 *      → T키 → QuickSlot.UseSlot(i) → 인벤토리 수량 차감 + 효과 적용 + 쿨다운
 *   ※ 슬롯 개수는 QuickSlot.slots 길이를 그대로 따라감(현재 3칸 고정).
 *
 * [씬 구성]
 * ItemQuickSlot (이 스크립트 부착)
 *  └ Container (빈 RectTransform, 앵커·피벗 중앙)   ← container 연결
 *      (슬롯은 slotPrefab으로 런타임 자동 생성)
 *
 * [슬롯 프리팹]
 * Slot(Image=프레임)
 *  ├ Icon     (Image, 이름 "Icon")
 *  ├ Count    (TMP,   이름 "Count")
 *  └ Cooldown (Image, 이름 "Cooldown", 선택) — Image Type=Filled/Radial360 권장
 *
 * [입력] InputManager 기준 — R: 슬롯 전환 / T: 사용
 */

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemQuickSlotUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform container;
    [SerializeField] private GameObject    slotPrefab;

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
    [SerializeField] private Color flashColor    = new Color(0.4f, 1f, 0.6f, 1f); // 사용 성공 플래시
    [SerializeField] private float flashTime     = 0.15f;

    [Header("동작")]
    [Tooltip("체크 시 InputManager의 R(전환)/T(사용) 키로 조작")]
    [SerializeField] private bool useInputManager = true;

    [Tooltip("체크 시 인벤토리의 소모품을 빈 퀵슬롯에 자동으로 채움 (먹으면 바로 쓸 수 있게)")]
    [SerializeField] private bool autoAssignFromInventory = true;

    /// <summary>현재 선택 슬롯 인덱스.</summary>
    public int CurrentIndex { get; private set; }

    /// <summary>현재 선택된 소모품 (없으면 null).</summary>
    public ConsumableData CurrentItem =>
        (_quickSlot != null && CurrentIndex >= 0 && CurrentIndex < _quickSlot.slots.Length)
            ? _quickSlot.slots[CurrentIndex] : null;

    /// <summary>선택 변경 시 발행.</summary>
    public event Action<int> onSelectionChanged;

    /// <summary>아이템을 실제로 사용했을 때 발행 (사운드/연출 연결용).</summary>
    public event Action<ConsumableData> onItemUsed;

    private QuickSlot _quickSlot;

    private RectTransform[] _slots;
    private Image[]         _frames;
    private Image[]         _icons;
    private TMP_Text[]      _counts;
    private Image[]         _cooldowns;

    private float _targetAngle;
    private float _currentAngle;
    private float _flashTimer;
    private bool  _subscribed;

    // 내가 UseSlot을 호출하는 동안 true. 이때 오는 OnInventoryChanged는 무시한다.
    // (UseSlot은 내부에서 ConsumeOne→OnInventoryChanged를 부른 뒤 아직 그 슬롯으로 ApplyEffect를 실행하므로,
    //  여기서 슬롯을 비우면 UseSlot이 null을 참조해 터진다. UseSlot이 끝나면 스스로 슬롯을 정리하고,
    //  TryUseCurrent가 마지막에 RefreshAll을 부르므로 화면 갱신도 문제없다.)
    private bool _usingSlot;

    private void OnDisable()
    {
        if (_subscribed && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= OnInventoryChanged;
            _subscribed = false;
        }
    }

    private void Update()
    {
        // 플레이어가 런타임에 스폰되므로(멀티) 매 프레임 확보를 시도한다.
        if (!EnsureQuickSlot()) return;

        HandleInput();
        UpdateRevolverRotation();
        UpdateCooldownGauges();
        UpdateFlash();
    }

    // ── 백엔드 확보 ───────────────────────────────────────────

    // QuickSlot을 찾으면 슬롯 UI를 만들고 인벤토리 이벤트를 구독한다.
    private bool EnsureQuickSlot()
    {
        if (_quickSlot != null) return true;

        Player player = GameManager.Instance != null ? GameManager.Instance.playerRef : null;
        if (player == null || player.quickSlot == null) return false;

        _quickSlot = player.quickSlot;

        BuildSlots(_quickSlot.slots.Length);

        if (InventoryManager.Instance != null && !_subscribed)
        {
            InventoryManager.Instance.OnInventoryChanged += OnInventoryChanged;
            _subscribed = true;
        }

        OnInventoryChanged();               // 이미 갖고 있던 아이템 반영
        ApplySelection(instant: true);
        return true;
    }

    private void OnInventoryChanged()
    {
        // UseSlot 실행 도중 들어온 이벤트는 무시 — UseSlot이 그 슬롯을 아직 쓰고 있어서
        // 지금 슬롯을 건드리면 재진입 크래시가 남. 처리는 TryUseCurrent가 끝나고 다시 한다.
        if (_usingSlot) return;

        ClearMissingSlots();                                  // 인벤토리에서 사라진 아이템의 슬롯을 먼저 비우고
        if (autoAssignFromInventory) AutoAssignEmptySlots();  // 빈 칸을 다시 채운 뒤
        RefreshAll();                                         // 화면에 반영
    }

    // 퀵슬롯에 물려있지만 인벤토리엔 더 이상 없는(수량 0) 아이템을 비운다.
    // 아이템 '사용'은 QuickSlot.UseSlot이 알아서 슬롯을 비우지만,
    // '드랍(밖으로 버리기)'은 InventoryManager를 직접 거쳐 QuickSlot을 안 타므로
    // 여기서 인벤토리 기준으로 슬롯을 정리해줘야 유령 슬롯이 안 남는다.
    private void ClearMissingSlots()
    {
        if (_quickSlot == null || InventoryManager.Instance == null) return;

        for (int i = 0; i < _quickSlot.slots.Length; i++)
        {
            ConsumableData data = _quickSlot.slots[i];
            if (data == null) continue;

            if (InventoryManager.Instance.GetCount(data) <= 0)
                _quickSlot.AssignSlot(i, null);   // 재고 없음 → 슬롯 비움
        }
    }

    // 인벤토리에 있는데 퀵슬롯엔 없는 소모품을 빈 칸에 채운다.
    // (다 쓰면 QuickSlot.UseSlot이 알아서 슬롯을 비우므로 다음 아이템이 자동으로 들어옴)
    private void AutoAssignEmptySlots()
    {
        if (_quickSlot == null || InventoryManager.Instance == null) return;

        foreach (ItemStack stack in InventoryManager.Instance.items)
        {
            if (stack == null || stack.count <= 0) continue;

            ConsumableData consumable = stack.data as ConsumableData;
            if (consumable == null) continue;
            if (IsAssigned(consumable)) continue;

            int empty = FindEmptySlotIndex();
            if (empty < 0) return;          // 빈 칸 없음 — 나머지는 인벤토리에만 보관
            _quickSlot.AssignSlot(empty, consumable);
        }
    }

    private bool IsAssigned(ConsumableData data)
    {
        foreach (ConsumableData slot in _quickSlot.slots)
            if (slot == data) return true;
        return false;
    }

    private int FindEmptySlotIndex()
    {
        for (int i = 0; i < _quickSlot.slots.Length; i++)
            if (_quickSlot.slots[i] == null) return i;
        return -1;
    }

    // ── 입력 ──────────────────────────────────────────────────

    private void HandleInput()
    {
        if (!useInputManager || InputManager.Instance == null) return;

        if (InputManager.Instance.switchConsumable) SelectNext();
        if (InputManager.Instance.useConsumable)    TryUseCurrent();
    }

    /// <summary>현재 슬롯 사용. 실제 소모/효과/쿨다운은 QuickSlot이 처리한다.</summary>
    public void TryUseCurrent()
    {
        if (_quickSlot == null) return;

        ConsumableData before = CurrentItem;
        if (before == null) return;

        // 쿨다운 중이면 UseSlot 내부에서 무시됨 — 여기선 사용 전후 수량으로 성공 여부를 판단한다.
        int countBefore = InventoryManager.Instance != null
            ? InventoryManager.Instance.GetCount(before) : 0;

        // UseSlot 내부에서 발행되는 OnInventoryChanged가 슬롯을 비우지 못하게 잠금 (재진입 방지)
        _usingSlot = true;
        _quickSlot.UseSlot(CurrentIndex);
        _usingSlot = false;

        int countAfter = InventoryManager.Instance != null
            ? InventoryManager.Instance.GetCount(before) : 0;

        if (countAfter < countBefore)
        {
            _flashTimer = flashTime;
            onItemUsed?.Invoke(before);
        }

        // 사용 중 미뤄뒀던 정리를 지금 수행 (다 쓴 슬롯 비우기 + 다음 아이템 자동 배치 + 표시 갱신)
        ClearMissingSlots();
        if (autoAssignFromInventory) AutoAssignEmptySlots();
        RefreshAll();
    }

    public void SelectNext()
    {
        if (_quickSlot == null) return;
        Select((CurrentIndex + 1) % _quickSlot.slots.Length);
    }

    public void SelectPrev()
    {
        if (_quickSlot == null) return;
        int n = _quickSlot.slots.Length;
        Select((CurrentIndex - 1 + n) % n);
    }

    public void Select(int index)
    {
        if (_quickSlot == null) return;
        if (index == CurrentIndex || index < 0 || index >= _quickSlot.slots.Length) return;

        CurrentIndex = index;
        ApplySelection(instant: false);
        onSelectionChanged?.Invoke(CurrentIndex);
    }

    // ── 슬롯 생성 / 표시 ──────────────────────────────────────

    private void BuildSlots(int count)
    {
        if (container == null || slotPrefab == null || count <= 0) return;

        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        _slots     = new RectTransform[count];
        _frames    = new Image[count];
        _icons     = new Image[count];
        _counts    = new TMP_Text[count];
        _cooldowns = new Image[count];

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
            _counts[i]    = go.transform.Find("Count")?.GetComponent<TMP_Text>();
            _cooldowns[i] = go.transform.Find("Cooldown")?.GetComponent<Image>();

            if (_cooldowns[i] != null) _cooldowns[i].fillAmount = 0f;
        }
    }

    /// <summary>QuickSlot 상태를 슬롯 UI에 그대로 반영.</summary>
    public void RefreshAll()
    {
        if (_quickSlot == null || _icons == null) return;

        for (int i = 0; i < _icons.Length && i < _quickSlot.slots.Length; i++)
        {
            ConsumableData data = _quickSlot.slots[i];

            if (_icons[i] != null)
            {
                _icons[i].sprite  = data != null ? data.icon : null;
                // 스프라이트가 없으면 흰 사각형으로 그려지므로 아예 꺼둔다
                _icons[i].enabled = data != null && data.icon != null;
            }

            if (_counts[i] != null)
            {
                int count = (data != null && InventoryManager.Instance != null)
                    ? InventoryManager.Instance.GetCount(data) : 0;
                _counts[i].text = count > 1 ? count.ToString() : string.Empty;
            }
        }
    }

    // ── 연출 ──────────────────────────────────────────────────

    private void UpdateRevolverRotation()
    {
        if (container == null || _slots == null) return;

        _currentAngle = Mathf.LerpAngle(_currentAngle, _targetAngle, rotateSpeed * Time.deltaTime);
        container.localRotation = Quaternion.Euler(0f, 0f, _currentAngle);

        // 슬롯 내용물은 항상 똑바로 보이도록 역회전
        foreach (RectTransform slot in _slots)
            if (slot != null) slot.localRotation = Quaternion.Euler(0f, 0f, -_currentAngle);
    }

    private void UpdateCooldownGauges()
    {
        if (_cooldowns == null) return;

        for (int i = 0; i < _cooldowns.Length && i < _quickSlot.slots.Length; i++)
        {
            if (_cooldowns[i] == null) continue;
            _cooldowns[i].fillAmount = _quickSlot.GetCooldownRatio(i);
        }
    }

    private void UpdateFlash()
    {
        if (_flashTimer <= 0f || _frames == null) return;

        _flashTimer -= Time.deltaTime;
        float t = Mathf.Clamp01(_flashTimer / flashTime);

        if (CurrentIndex < _frames.Length && _frames[CurrentIndex] != null)
            _frames[CurrentIndex].color = Color.Lerp(selectedColor, flashColor, t);
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
        _targetAngle = 360f / Mathf.Max(1, _slots.Length) * CurrentIndex;
        if (instant)
        {
            _currentAngle = _targetAngle;
            if (container != null)
                container.localRotation = Quaternion.Euler(0f, 0f, _currentAngle);
        }
    }
}
