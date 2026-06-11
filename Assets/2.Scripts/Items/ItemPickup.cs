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
    private ItemData data;
    private int amount;
    private int goldAmount;

    // 아이템 드랍용 초기화
    public void Init(ItemData data, int amount)
    {
        this.data = data;
        this.amount = amount;
        this.goldAmount = 0;
    }

    // 골드 드랍용 초기화
    public void Init(int goldAmount)
    {
        this.data = null;
        this.amount = 0;
        this.goldAmount = goldAmount;
    }

    private void OnTriggerEnter(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null)
        {
            return;
        }

        if (data != null)
        {
            InventoryManager.Instance.AddItem(data, amount);
        }
        else if (goldAmount > 0)
        {
            InventoryManager.Instance.AddGold(goldAmount);
        }

        PoolManager.Instance.Return(gameObject);
    }

    private void OnDisable()
    {
        data = null;
        amount = 0;
        goldAmount = 0;
    }
}
