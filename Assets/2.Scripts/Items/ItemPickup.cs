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
    [SerializeField]
    private ItemData itemData;
    [Tooltip("아이템 획득 시 증가할 갯수")]
    [SerializeField]
    private int amount = 1;

    // 같은 프리팹에 붙은 ItemPickupVisual이 "무엇을 몇 개 줄지" 읽어가기 위한 getter.
    // 데이터를 양쪽에 중복 입력하지 않도록 이 필드를 단일 출처로 씀.
    public ItemData ItemData => itemData;
    public int Amount => amount;
    //[SerializeField]
    //private float _turnRate;
    //[SerializeField]
    //private float _speed;


    private ItemData _data;
    private int _amount;
    private int _goldAmount;

    // 플레이어 히트박스가 여러 개라 같은 프레임에 OnTriggerEnter가 여러 번 들어옴 — 중복 획득 방지용.
    private bool _requested;

    // 풀에서 꺼낼 때(SetActive true)마다 프리팹에 설정된 itemData로 자기 초기화 + 픽업 플래그 리셋.
    // itemData 미설정(골드 픽업 등)이면 스킵 — 그 경우 외부에서 Init(gold)로 세팅.
    private void OnEnable()
    {
        _requested = false;
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


    // 픽업 판정. 이 픽업들은 전부 로컬 오브젝트(PoolManager/Instantiate로 생성, PhotonView 없음)라
    // 다른 클라에는 존재하지 않는다 — 그래서 "누가 먼저 먹었나" 중재가 필요 없고, 밟은 사람이 바로 가진다.
    // (적 처치 드랍은 이 경로를 안 탐 — 획득자가 이미 정해져 있어 ItemPickupVisual이 날아가서 직접 지급함)
    private void OnTriggerEnter(Collider other)
    {
        if (_requested)
        {
            return;
        }
        Player player = other.GetComponentInParent<Player>();
        if (player == null || !player.IsMine)   // 내 함선만 픽업 시도
        {
            return;
        }

        _requested = true;
        GrantToLocalInventory();
        PoolManager.Instance.Return(gameObject);
    }

    // 로컬 인벤토리 지급. 데이터는 OnEnable/Init으로 이미 세팅돼 있음.
    private void GrantToLocalInventory()
    {
        if (_data != null)
        {
            InventoryManager.Instance.AddItem(_data, _amount);
        }
        else if (_goldAmount > 0)
        {
            InventoryManager.Instance.AddGold(_goldAmount);
        }
    }

    private void OnDisable()
    {
        _data = null;
        _amount = 0;
        _goldAmount = 0;
        _requested = false;
    }
}
