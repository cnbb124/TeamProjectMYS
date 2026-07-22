/*
 * [InventoryDropper]
 * 인벤토리 밖으로 아이템을 드래그해서 버릴 때, 실제로 월드에 드랍 오브젝트를 생성해주는 담당.
 * InvSlot이 "슬롯 밖에서 손을 놨다"고 판단하면 이 스크립트의 TryDrop()을 호출한다.
 *
 * [왜 별도 스크립트인가]
 * InvSlot은 슬롯 한 칸의 표시/드래그만 알면 되고, "월드 어디에 무엇을 떨굴지"는 씬 사정이라
 * 슬롯 100개가 각자 프리팹 참조를 들고 있을 이유가 없다. 씬에 하나만 두고 공용으로 쓴다.
 *
 * [부착 / 연결]
 * 1. HUD 프리팹 하위에 빈 오브젝트를 만들고 이 스크립트 부착
 *    (HUD는 HUDManager가 DontDestroyOnLoad 하므로 씬을 넘어가도 살아남음)
 * 2. Drop Entries : 아이템 ↔ 드랍 프리팹 짝을 등록
 *      예) HPkit → TestItemPickup_HPkit / BoostKit → TestItemPickup_BoostKit ...
 * 3. Default Drop Prefab : 목록에 없는 아이템을 버렸을 때 쓸 예비 프리팹 (비워도 됨)
 * 4. 짝을 못 찾고 예비도 없으면 버리기가 취소됨 (아이템은 인벤토리에 그대로 유지 — 소실 방지)
 *
 * [주의]
 * - 플레이어 바로 앞에 떨구면 ItemPickup의 OnTriggerEnter가 즉시 반응해 도로 주워짐.
 *   그래서 dropDistance만큼 앞으로 던진다. 기체가 크면 이 값을 더 키울 것.
 * - 현재는 로컬 Instantiate라 멀티에서 남에게는 안 보인다.
 *   (기존 EnemyWorker/cs_Map_Asteroid의 드랍도 같은 방식이라 일단 맞춰둠.
 *    네트워크 공유가 필요해지면 PhotonNetwork.Instantiate로 교체 — 팀장 협의 필요)
 */

using System.Collections.Generic;
using UnityEngine;

public class InventoryDropper : MonoBehaviour
{
    /// <summary>아이템 하나와 그 아이템을 월드에 떨굴 때 쓸 프리팹의 짝.</summary>
    [System.Serializable]
    public class DropEntry
    {
        public ItemData   item;
        [Tooltip("ItemPickup 컴포넌트 + Trigger 콜라이더가 있는 드랍 프리팹")]
        public GameObject prefab;
    }

    public static InventoryDropper Instance { get; private set; }

    [Header("아이템별 드랍 프리팹")]
    [SerializeField] private List<DropEntry> dropEntries = new List<DropEntry>();

    [Tooltip("목록에 없는 아이템을 버렸을 때 쓸 예비 프리팹. 비워두면 그런 아이템은 버릴 수 없음")]
    [SerializeField] private GameObject defaultDropPrefab;

    [Tooltip("플레이어 앞쪽으로 이만큼 떨어진 위치에 생성. 너무 가까우면 즉시 다시 주워짐")]
    [SerializeField] private float dropDistance = 20f;

    [Tooltip("여러 개를 연속으로 버릴 때 겹치지 않도록 주는 무작위 흩뿌림 반경")]
    [SerializeField] private float randomSpread = 3f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 인벤토리에서 빼서 월드에 떨군다.
    /// 인벤토리 차감이 실패하면(수량 부족 등) 아무것도 생성하지 않고 false를 반환한다.
    /// </summary>
    public bool TryDrop(ItemData data, int count)
    {
        if (data == null || count <= 0) return false;

        GameObject prefab = FindDropPrefab(data);
        if (prefab == null)
        {
            Debug.LogWarning($"[InventoryDropper] '{data.itemName}'에 대응하는 드랍 프리팹이 없습니다. " +
                             "Drop Entries에 등록하거나 Default Drop Prefab을 지정하세요. — 버리기 취소됨");
            return false;
        }

        if (InventoryManager.Instance == null) return false;

        // 먼저 인벤토리에서 빼고, 성공했을 때만 월드에 생성한다(복제 방지).
        if (!InventoryManager.Instance.RemoveItem(data, count))
        {
            Debug.LogWarning($"[InventoryDropper] 인벤토리에서 제거 실패: {data.itemName} x{count}");
            return false;
        }

        GameObject go = Instantiate(prefab, GetDropPosition(), Quaternion.identity);

        ItemPickup pickup = go.GetComponent<ItemPickup>();
        if (pickup != null)
        {
            pickup.Init(data, count);
        }
        else
        {
            Debug.LogWarning("[InventoryDropper] dropPrefab에 ItemPickup이 없습니다.");
        }

        return true;
    }

    // 등록된 짝을 먼저 찾고, 없으면 예비 프리팹으로.
    private GameObject FindDropPrefab(ItemData data)
    {
        foreach (DropEntry entry in dropEntries)
        {
            if (entry != null && entry.item == data && entry.prefab != null)
                return entry.prefab;
        }
        return defaultDropPrefab;
    }

    // 플레이어 앞쪽 + 약간의 무작위 오프셋. 플레이어가 없으면 이 오브젝트 위치 기준.
    private Vector3 GetDropPosition()
    {
        Transform origin = transform;
        if (GameManager.Instance != null && GameManager.Instance.playerRef != null)
            origin = GameManager.Instance.playerRef.transform;

        Vector3 pos = origin.position + origin.forward * dropDistance;

        if (randomSpread > 0f)
            pos += Random.insideUnitSphere * randomSpread;

        return pos;
    }
}
