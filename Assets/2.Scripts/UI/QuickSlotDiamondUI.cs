/*
 * [QuickSlotDiamondUI]
 * 어크 쉐도우식 다이아몬드(십자) 퀵슬롯 — 4칸 고정.
 *   상(Up)/하(Down) = 스킬 슬롯
 *   좌(Left)/우(Right) = 아이템 슬롯
 *
 * [씬 구성]
 * QuickSlotUI (이 스크립트 부착, 앵커·피벗 중앙, 화면 좌하단 배치 권장)
 *  └ (슬롯 4개는 slotPrefab으로 런타임 자동 생성 — 상/하/좌/우 배치)
 *
 * [슬롯 프리팹]
 * Slot (Image = 배경/프레임, 앵커·피벗 중앙)
 *  ├ Icon  (Image, 이름 "Icon")  — 아이템/스킬 아이콘
 *  └ Count (TMP,   이름 "Count") — 수량 (스킬이면 빈 칸)
 *
 * [슬롯 지정 (데이터)]
 * - 아이템: SetItem(SlotPos.Left/Right, ItemData, count) — 기존 ItemData SO 그대로 사용
 * - 스킬  : SetSkill(SlotPos.Up/Down, sprite)            — 스킬 아이콘 (SkillData 확정되면 오버로드 추가)
 * - 인스펙터 defaultItems로 시작 아이템 미리 지정 가능 (테스트용)
 *
 * [입력 — 아직 미연결]
 * InputManager 키 통합 전이므로 입력 코드는 없음.
 * 통합되면 Update에서 아래처럼 연결 (훅 = UseSlot):
 *   if (InputManager.Instance.useItemLeft)  UseSlot(SlotPos.Left);
 *   if (InputManager.Instance.useSkillUp)   UseSlot(SlotPos.Up);
 * UseSlot은 플래시 연출 + onSlotUsed(pos) 이벤트 발행 → 실제 사용 로직은 구독자가 처리.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuickSlotDiamondUI : MonoBehaviour
{
    public enum SlotPos { Up, Down, Left, Right }   // 상/하=스킬, 좌/우=아이템

    [Header("References")]
    [SerializeField] private GameObject slotPrefab;

    [Header("배치")]
    [SerializeField] private float radius = 60f;     // 중심에서 각 슬롯까지 거리

    [Header("시작 아이템 (테스트/기본 세팅용, 좌/우 슬롯)")]
    [SerializeField] private ItemData leftItem;
    [SerializeField] private ItemData rightItem;

    [Header("시작 스킬 (상/하 슬롯) — ActiveSkillData 연결 (아이콘은 SkillData.icon에서 자동)")]
    [SerializeField] private ActiveSkillData upSkill;
    [SerializeField] private ActiveSkillData downSkill;

    [Header("연출")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 150f / 255f);
    [SerializeField] private Color flashColor  = Color.white;
    [SerializeField] private float flashTime   = 0.15f;

    /// <summary>슬롯 발동 시 발행 — 실제 아이템/스킬 사용 로직 연결 지점.</summary>
    public event Action<SlotPos> onSlotUsed;

    private class Slot
    {
        public Image           frame;
        public Image           icon;
        public TMP_Text        count;
        public float           flashTimer;
        public ItemData        item;    // 좌/우 슬롯 데이터
        public ActiveSkillData skill;   // 상/하 슬롯 데이터 (쿨다운 표시 등에 활용)
    }

    private readonly Dictionary<SlotPos, Slot> _slots = new Dictionary<SlotPos, Slot>();

    // 다이아몬드 방향 벡터
    private static readonly Dictionary<SlotPos, Vector2> _dirs = new Dictionary<SlotPos, Vector2>
    {
        { SlotPos.Up,    Vector2.up },
        { SlotPos.Down,  Vector2.down },
        { SlotPos.Left,  Vector2.left },
        { SlotPos.Right, Vector2.right },
    };

    private void Start()
    {
        BuildSlots();

        // 인스펙터 기본 세팅 반영
        if (leftItem  != null) SetItem(SlotPos.Left,  leftItem);
        if (rightItem != null) SetItem(SlotPos.Right, rightItem);
        if (upSkill   != null) SetSkill(SlotPos.Up,   upSkill);
        if (downSkill != null) SetSkill(SlotPos.Down, downSkill);
    }

    private void Update()
    {
        // TODO: InputManager 키 통합 후 여기서 UseSlot(...) 연결
        // if (InputManager.Instance.useItemLeft) UseSlot(SlotPos.Left); 등

        // 플래시 페이드
        foreach (Slot s in _slots.Values)
        {
            if (s.flashTimer > 0f)
            {
                s.flashTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(s.flashTimer / flashTime);
                if (s.frame != null)
                    s.frame.color = Color.Lerp(normalColor, flashColor, t);
            }
        }
    }

    // ── 슬롯 생성 (상/하/좌/우) ──
    private void BuildSlots()
    {
        if (slotPrefab == null) return;
        _slots.Clear();

        foreach (KeyValuePair<SlotPos, Vector2> kv in _dirs)
        {
            GameObject go = Instantiate(slotPrefab, transform);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = kv.Value * radius;

            Slot slot = new Slot
            {
                frame = go.GetComponent<Image>(),
                icon  = go.transform.Find("Icon")?.GetComponent<Image>(),
                count = go.transform.Find("Count")?.GetComponent<TMP_Text>(),
            };
            if (slot.frame != null) slot.frame.color = normalColor;
            if (slot.icon  != null) slot.icon.enabled = false; // 비어있으면 아이콘 숨김
            if (slot.count != null) slot.count.text = "";

            _slots[kv.Key] = slot;
        }
    }

    // ── 슬롯 지정 API ──

    /// <summary>아이템 슬롯 세팅 (Left/Right 권장). ItemData의 아이콘/수량 표시.</summary>
    public void SetItem(SlotPos pos, ItemData item, int count = 1)
    {
        if (!_slots.TryGetValue(pos, out Slot slot)) return;

        slot.item  = item;
        slot.skill = null;

        if (slot.icon != null)
        {
            slot.icon.sprite  = item != null ? item.icon : null;
            slot.icon.enabled = item != null && item.icon != null;
        }
        if (slot.count != null)
            slot.count.text = (item != null && count > 1) ? count.ToString() : "";
    }

    /// <summary>스킬 슬롯 세팅 (Up/Down 권장). SkillData.icon에서 아이콘 자동 표시.</summary>
    public void SetSkill(SlotPos pos, ActiveSkillData skill)
    {
        if (!_slots.TryGetValue(pos, out Slot slot)) return;

        slot.skill = skill;
        slot.item  = null;

        if (slot.icon != null)
        {
            slot.icon.sprite  = skill != null ? skill.icon : null;
            slot.icon.enabled = skill != null && skill.icon != null;
        }
        if (slot.count != null) slot.count.text = ""; // 스킬은 수량 없음
    }

    /// <summary>슬롯에 지정된 아이템 조회 (사용 로직 연동용).</summary>
    public ItemData GetItem(SlotPos pos)
        => _slots.TryGetValue(pos, out Slot s) ? s.item : null;

    /// <summary>슬롯에 지정된 스킬 조회 (SkillSystem 발동/쿨다운 연동용).</summary>
    public ActiveSkillData GetSkill(SlotPos pos)
        => _slots.TryGetValue(pos, out Slot s) ? s.skill : null;

    /// <summary>수량만 갱신 (아이템 사용/획득 시).</summary>
    public void SetCount(SlotPos pos, int count)
    {
        if (!_slots.TryGetValue(pos, out Slot slot)) return;
        if (slot.count != null)
            slot.count.text = count > 1 ? count.ToString() : (count == 1 ? "" : "0");
    }

    /// <summary>슬롯 발동 — 플래시 연출 + 이벤트. InputManager 통합 후 입력에서 호출.</summary>
    public void UseSlot(SlotPos pos)
    {
        if (!_slots.TryGetValue(pos, out Slot slot)) return;
        slot.flashTimer = flashTime;
        onSlotUsed?.Invoke(pos);
    }
}
