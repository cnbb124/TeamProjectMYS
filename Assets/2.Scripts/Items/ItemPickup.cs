using UnityEngine;



// ======================사용법========================
// 부착 시 참고:

// 픽업 프리팹에 Trigger 콜라이더 필요
// 풀에서 꺼낸 직후 Init(ItemData, amount) 또는 Init(goldAmount) 호출해서 데이터 세팅
// Player는 GetComponentInParent<Player>()로 감지 (다른 트리거/투사체와 동일한 방식)
// 월드에 드랍된 아이템/골드. 플레이어와 충돌 시 인벤토리로 이동 후 풀 반납.
// PoolManager.Get(POOL_TYPE)으로 꺼낸 직후 Init()으로 데이터 설정.

// ex)
// Asteroid.파괴() / Enemy.Die() 등에서 (드랍시점에서)
// ItemPickup pickup = PoolManager.Instance.Get(POOL_TYPE.ITEM_GOLD).GetComponent<ItemPickup>();
// pickup.Init(50); 골드 50 드랍
// pickup.Init(somePartData, 1); // 아이템 드랍
// ======================================================
public class ItemPickup : MonoBehaviour
{
    [Header("고정 아이템 드랍용 (프리팹에서 설정)")]
    [Tooltip("이 픽업이 주는 아이템. 설정 시 풀에서 꺼낼 때 자동 초기화됨")]
    [SerializeField] private ItemData itemData;
    [SerializeField] private int amount = 1;

    private ItemData _data;
    private int _amount;
    private int _goldAmount;

    // 풀에서 꺼낼 때(SetActive true)마다 프리팹에 설정된 itemData로 자기 초기화.
    // itemData 미설정(골드 픽업 등)이면 스킵 — 그 경우 외부에서 Init(gold)로 세팅.
    private void OnEnable()
    {
        if (itemData != null)
        {
            _data = itemData;
            _amount = amount;
            _goldAmount = 0;
        }
    }

    // 아이템 드랍용 초기화
    public void Init(ItemData data, int amount)
    {
        _data = data;
        _amount = amount;
        _goldAmount = 0;
    }

    // 골드 드랍용 초기화
    public void Init(int goldAmount)
    {
        _data = null;
        _amount = 0;
        _goldAmount = goldAmount;
    }

    private void OnTriggerEnter(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null)
        {
            return;
        }

        if (_data != null)
        {
            InventoryManager.Instance.AddItem(_data, _amount);
        }
        else if (_goldAmount > 0)
        {
            InventoryManager.Instance.AddGold(_goldAmount);
        }

        PoolManager.Instance.Return(gameObject);
    }

    private void OnDisable()
    {
        _data = null;
        _amount = 0;
        _goldAmount = 0;
    }
}
