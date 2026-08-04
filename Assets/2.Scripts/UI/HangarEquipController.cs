/*
 * [HangarEquipController]
 * 격납고 파츠 장착 흐름의 두뇌. (드래그앤드롭 방식)
 *
 *   리스트에 여분 파츠 표시 → 파츠를 노드로 드래그 → 드롭하면 그 부위에 장착
 *
 * ★partType은 노드에 직접 안 넣는다. PartNodeConnector에 배선된 (노드↔앵커) 짝에서
 *   앵커(PartNodeAnchor).partType을 가져와, 각 노드 버튼에 HangarNodeDropTarget을 런타임 부착한다.
 *   → 노드/앵커/커넥터가 partType의 단일 출처.
 *
 * 실제 데이터 처리는 각 시스템에 위임:
 *   - 목록 표시    : HangarSlotList (여분 파츠 전체)
 *   - 실제 장착    : UnitParts.Equip(partType, part)
 *   - 인벤토리 정리 : InventoryManager (새 파츠 제거 + 벗겨진 파츠 반환)
 *
 * [부착 / 연결]
 * 1. HangarPartPanel에 이 스크립트 부착
 * 2. Connector : 라인 그리는 PartNodeConnector 연결 (짝 정보 재활용)
 * 3. List      : 파츠 목록 패널(HangarSlotList) 연결
 * ※ 앵커(PartNodeAnchor)의 Part Type이 올바르게 지정돼 있어야 함
 *
 * [주의]
 * - Equip(partType)은 그 종류의 '첫 슬롯'에 장착됨. LAUNCHER처럼 슬롯 2개인 종류는 첫 슬롯만 대상.
 * - Manage Inventory 체크 시: 장착 파츠는 인벤토리에서 빠지고, 벗겨진 파츠는 인벤토리로 돌아온다.
 */

using System.Collections.Generic;
using UnityEngine;

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

    private readonly List<HangarNodeDropTarget> _dropTargets = new List<HangarNodeDropTarget>();

    private void OnEnable()
    {
        BuildDropTargets();
        RefreshNodeIcons();   // 각 노드에 현재 장착된 파츠 아이콘 표시
        // 여분 파츠 전체를 리스트에 표시 (드래그해서 노드로 끌어다 장착)
        if (list != null) list.RefreshFromInventory();
    }

    private void OnDisable()
    {
        foreach (HangarNodeDropTarget t in _dropTargets)
            if (t != null) t.onDropPart -= OnPartDropped;
    }

    // 커넥터의 (노드↔앵커) 짝에서 노드 버튼에 드롭 타겟을 부착하고 partType을 채운다.
    private void BuildDropTargets()
    {
        foreach (HangarNodeDropTarget t in _dropTargets)
            if (t != null) t.onDropPart -= OnPartDropped;
        _dropTargets.Clear();

        if (connector == null)
        {
            Debug.LogWarning("[HangarEquip] Connector 미연결 — 드롭 타겟을 만들 수 없습니다.");
            return;
        }

        foreach (PartNodeConnector.Connection c in connector.Connections)
        {
            if (c == null || c.node == null || c.anchor == null) continue;

            PartNodeAnchor anchor = c.anchor.GetComponent<PartNodeAnchor>();
            if (anchor == null) continue;

            // 노드 버튼에 드롭 타겟 부착(이미 있으면 재사용) + partType 주입
            HangarNodeDropTarget target = c.node.GetComponent<HangarNodeDropTarget>();
            if (target == null) target = c.node.gameObject.AddComponent<HangarNodeDropTarget>();

            target.partType   = anchor.partType;
            target.onDropPart += OnPartDropped;
            _dropTargets.Add(target);
        }
    }

    // 각 노드에 현재 장착된 파츠 아이콘을 표시 (UnitParts에서 조회).
    private void RefreshNodeIcons()
    {
        Player player = GameManager.Instance != null ? GameManager.Instance.playerRef : null;
        UnitParts parts = player != null ? player.GetComponent<UnitParts>() : null;

        foreach (HangarNodeDropTarget t in _dropTargets)
        {
            if (t == null) continue;
            PartData equipped = parts != null ? parts.GetEquipped(t.partType) : null;
            t.ShowEquipped(equipped);   // 없으면 빈 슬롯(아이콘 숨김)
        }
    }

    // 노드에 파츠가 드롭됨 → 그 부위에 장착
    private void OnPartDropped(PART_TYPE nodeType, PartData part)
    {
        if (part == null) return;

        // 파츠 종류가 노드와 안 맞으면 무시 (엉뚱한 노드에 드롭)
        if (part.partType != nodeType)
        {
            Debug.Log($"[HangarEquip] 이 부위엔 못 낌: 노드={nodeType}, 파츠={part.partType}");
            return;
        }

        Player player = GameManager.Instance != null ? GameManager.Instance.playerRef : null;
        if (player == null) { Debug.LogWarning("[HangarEquip] playerRef 없음"); return; }

        UnitParts parts = player.GetComponent<UnitParts>();
        if (parts == null) { Debug.LogWarning("[HangarEquip] UnitParts 없음"); return; }

        PartData old = parts.GetEquipped(nodeType);
        if (old == part) return;   // 이미 장착돼 있으면 무시

        parts.Equip(nodeType, part);

        if (manageInventory && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.RemoveItem(part, 1);              // 장착됐으니 목록에서 제거
            if (old != null) InventoryManager.Instance.AddItem(old, 1); // 벗긴 건 목록으로 반환
        }

        RefreshNodeIcons();                             // 노드 아이콘 갱신 (새로 낀 파츠 반영)
        if (list != null) list.RefreshFromInventory();  // 리스트 갱신 (벗긴 파츠가 여분으로 돌아옴)
        PlayerProfile.CaptureFrom(player);              // 씬을 안 나가도 장착 즉시 프로필에 반영
        player.BroadcastLoadout();                      // 바뀐 구성을 남 클라 복제본에도 반영
        player.CollectParticles();                      // 파츠 프리팹이 새로 생겨 이전 파티클 캐시가 죽음
        RefreshStatPanel();                             // 파츠가 바뀌면 최대 HP/실드/아머도 바뀜
    }

    // 탭으로 꺼져 있을 수 있어 비활성 포함으로 찾음.
    private void RefreshStatPanel()
    {
        if (statPanel == null)
            statPanel = FindObjectOfType<HangarStatPanelUI>(true);
        if (statPanel != null)
            statPanel.Refresh();
    }
}
