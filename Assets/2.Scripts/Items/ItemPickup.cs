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
    [Tooltip("아이템 갯수")]
    [SerializeField]
    private int amount = 1;
    [SerializeField]
    private float _turnRate;
    [SerializeField]
    private float _speed;


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

    public void TracePlayerForPickup(Transform targetTr, Vector3 targetVelocity)
    {
		Vector3 toTarget = targetTr.position - transform.position;
		float dist = toTarget.magnitude;

		//타겟과 일정 거리 이내로 좁혀지면 미사일이 맴도는 현상 방지
		//거리가 가까울 때는 복잡한 예측을 버리고 타겟을 향해 즉시 내리꽂도록 강제
		if (dist < 20.0f)
		{
			Vector3 finalDir = Vector3.RotateTowards(transform.forward, toTarget.normalized, _turnRate * 2f * Mathf.Deg2Rad * Time.deltaTime, 0f);
			transform.forward = finalDir;
			transform.position += transform.forward * _speed * Time.deltaTime;
			return;
		}

		Vector3 desiredDir = toTarget.normalized;

		// 타겟의 위치를 계산하는 예측 추적(Predictive Pursuit) 알고리즘
		if (targetVelocity.sqrMagnitude > 0.1f)
		{
			// 현재 속도로 타겟까지 도달하는 데 걸리는 예상 시간(ETA)
			float timeToHit = dist / Mathf.Max(_speed, 1f);

			// 거리가 너무 멀 때 예측 좌표가 우주로 튀는 것을 막기 위해 최대 1.5초 후의 위치까지만 예측
			timeToHit = Mathf.Min(timeToHit, 1.5f);

			// 타겟의 미래 예측 위치 도출
			Vector3 predictedPos = targetTr.position + (targetVelocity * timeToHit);

			desiredDir = (predictedPos - transform.position).normalized;
		}

		// 예측된 방향으로 부드럽게 회전 및 전진
		Vector3 newDir = Vector3.RotateTowards(transform.forward, desiredDir, _turnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
		transform.forward = newDir;
		transform.position += transform.forward * _speed * Time.deltaTime;
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
