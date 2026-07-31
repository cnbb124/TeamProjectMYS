/*
 * [HangarEquipController]
 * 격납고 파츠 장착 흐름의 두뇌. (클릭 방식)
 *
 *   노드 클릭 → 그 종류(partType) 파츠만 목록에 필터 → 목록 항목 클릭 → 실제 장착
 *
 * ★partType은 노드에 따로 안 넣는다. PartNodeConnector에 이미 배선된 (노드↔앵커) 짝을
 *   그대로 가져와, 앵커의 PartNodeAnchor.partType을 종류로 쓴다.
 *   → 노드/앵커/커넥터가 partType의 단일 출처. 노드에 값 중복 입력·불일치 걱정 없음.
 *
 * 실제 데이터 처리는 각 시스템에 위임:
 *   - 목록 표시/필터 : HangarSlotList
 *   - 실제 장착      : UnitParts.Equip(partType, part)
 *   - 인벤토리 정리  : InventoryManager (새 파츠 제거 + 헌 파츠 반환)
 *
 * [부착 / 연결]
 * 1. HangarPartPanel에 이 스크립트 부착
 * 2. Connector : 라인 그리는 PartNodeConnector 연결 (짝 정보 재활용)
 * 3. List      : 파츠 목록 패널(HangarSlotList) 연결
 * ※ 앵커(PartNodeAnchor)의 Part Type이 올바르게 지정돼 있어야 함 — 여기가 유일한 진짜 출처
 *
 * [주의]
 * - Equip(partType)은 그 종류의 '첫 슬롯'에 장착됨. LAUNCHER처럼 슬롯 2개인 종류는 첫 슬롯만 대상.
 * - Manage Inventory 체크 시: 장착 파츠는 인벤토리에서 빠지고, 벗겨진 파츠는 인벤토리로 돌아온다.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HangarEquipController : MonoBehaviour
{
    [Header("연동")]
    [Tooltip("노드↔앵커 짝을 가진 PartNodeConnector (라인 그리는 그것)")]
    [SerializeField] private PartNodeConnector connector;
    [SerializeField] private HangarSlotList    list;
    [Tooltip("장착 후 스탯 갱신용. 비우면 씬에서 자동 탐색.")]
    [SerializeField] private HangarStatPanelUI statPanel;

    [Header("동작")]
    [Tooltip("장착 시 인벤토리 정리: 새 파츠 제거 + 벗겨진 파츠 반환")]
    [SerializeField] private bool manageInventory = true;

    [Tooltip("체크 시 노드를 처음 누르기 전에는 목록을 비워둠")]
    [SerializeField] private bool clearListUntilSelected = true;

    [Header("선택 노드 강조색")]
    [SerializeField] private Color selectedColor = new Color(0f, 1f, 0.8f, 1f);
    [SerializeField] private Color normalColor   = Color.white;

    // 커넥터에서 뽑아낸 노드 하나의 조작 단위
    private class NodeEntry
    {
        public Button    button;
        public Image     graphic;   // 강조색 적용 대상(버튼의 Image)
        public PART_TYPE partType;
        public UnityEngine.Events.UnityAction handler;
    }

    private readonly List<NodeEntry> _entries = new List<NodeEntry>();
    private NodeEntry _selected;

    private void OnEnable()
    {
        BuildEntries();

        foreach (NodeEntry e in _entries)
        {
            if (e.button != null) e.button.onClick.AddListener(e.handler);
            if (e.graphic != null) e.graphic.color = normalColor;
        }

        if (list != null) list.onPartSelected += OnPartSelected;

        _selected = null;
        if (clearListUntilSelected && list != null) list.Clear();
    }

    private void OnDisable()
    {
        foreach (NodeEntry e in _entries)
            if (e.button != null && e.handler != null) e.button.onClick.RemoveListener(e.handler);

        if (list != null) list.onPartSelected -= OnPartSelected;
    }

    // 커넥터의 (노드↔앵커) 짝에서 버튼/파츠종류를 뽑아 조작 단위로 구성.
    private void BuildEntries()
    {
        _entries.Clear();
        if (connector == null)
        {
            Debug.LogWarning("[HangarEquip] Connector 미연결 — 노드 클릭을 잡을 수 없습니다.");
            return;
        }

        foreach (PartNodeConnector.Connection c in connector.Connections)
        {
            if (c == null || c.node == null || c.anchor == null) continue;

            Button button = c.node.GetComponent<Button>();
            PartNodeAnchor anchor = c.anchor.GetComponent<PartNodeAnchor>();
            if (button == null || anchor == null) continue;

            NodeEntry entry = new NodeEntry
            {
                button   = button,
                graphic  = c.node.GetComponent<Image>(),
                partType = anchor.partType,
            };
            entry.handler = () => OnNodeClicked(entry);
            _entries.Add(entry);
        }
    }

    // 노드 클릭 → 선택 강조 + 그 종류 파츠만 목록에 표시
    private void OnNodeClicked(NodeEntry entry)
    {
        _selected = entry;

        foreach (NodeEntry e in _entries)
            if (e.graphic != null) e.graphic.color = (e == entry) ? selectedColor : normalColor;

        if (list != null) list.RefreshFiltered(entry.partType);
    }

    // 목록 항목 클릭 → 선택된 노드의 종류로 장착
    private void OnPartSelected(PartData part)
    {
        if (_selected == null || part == null) return;

        Player player = GameManager.Instance != null ? GameManager.Instance.playerRef : null;
        if (player == null) { Debug.LogWarning("[HangarEquip] playerRef 없음"); return; }

        UnitParts parts = player.GetComponent<UnitParts>();
        if (parts == null) { Debug.LogWarning("[HangarEquip] UnitParts 없음"); return; }

        PART_TYPE type = _selected.partType;
        if (part.partType != type)
        {
            Debug.LogWarning($"[HangarEquip] 종류 불일치: 노드={type}, 파츠={part.partType}");
            return;
        }

        PartData old = parts.GetEquipped(type);
        if (old == part) return;   // 이미 장착돼 있으면 무시

        parts.Equip(type, part);

        if (manageInventory && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.RemoveItem(part, 1);              // 장착됐으니 목록에서 제거
            if (old != null) InventoryManager.Instance.AddItem(old, 1); // 벗긴 건 목록으로 반환
        }

        if (list != null) list.RefreshFiltered(type);  // 기체 색은 ShipPartDamageView가 매프레임 갱신
        PlayerProfile.CaptureFrom(player);             // 씬을 안 나가도 장착 즉시 프로필에 반영
        player.CollectParticles();                     // 파츠 프리팹이 새로 생겨 이전 파티클 캐시가 죽음
        RefreshStatPanel();                            // 파츠가 바뀌면 최대 HP/실드/아머도 바뀜
    }

    // 탭으로 꺼져 있을 수 있어 비활성 포함으로 찾음.
    private void RefreshStatPanel()
    {
        if (statPanel == null)
        {
            statPanel = FindObjectOfType<HangarStatPanelUI>(true);
        }
        if (statPanel != null)
        {
            statPanel.Refresh();
        }
    }
}
