// ======================================================
// [외부 참조 가이드]
// ======================================================
// 적 처치 드랍 '연출' 전용 컴포넌트. 아이템이 획득자에게 날아가는 자석 연출을 재생하고,
// 도착하는 순간 인벤토리에 실제로 지급함.
//
// ▶ 사용법
//   PoolManager에서 꺼낸 뒤 위치를 잡고 StartChase(대상) 호출.
//   그 뒤는 알아서 날아가고, 도착하면 지급 + 풀 반납까지 스스로 함.
//
//   예시)
//   GameObject obj = PoolManager.Instance.Get(dropType);
//   obj.transform.position = 죽은자리;
//   obj.GetComponent<ItemPickupVisual>().StartChase(내함선.transform);
//
// ▶ ItemPickup과의 차이 (둘 다 같은 프리팹에 붙어도 됨)
//   ItemPickup       : 월드에 놓여 있다가 플레이어가 부딪혀 줍는 픽업(맵 배치·소행성 드랍).
//                      네트워크 오브젝트라 "누가 먼저 먹었나" 중재 RPC가 필요함.
//   ItemPickupVisual : 적 처치 드랍 전용. 획득자가 이미 정해져 있어(죽인 사람) 중재가 필요 없음.
//                      각 클라가 로컬 풀에서 꺼내 쓰므로 네트워크 스폰이 아예 없음.
//
//   → 연출이 시작되면 ItemPickup과 콜라이더를 꺼서, 날아가는 도중 다른 플레이어에게 닿아
//     중복 획득되는 걸 막음. 풀에 반납될 때 원래대로 되돌림.
//
// ▶ 지급할 아이템 정보
//   같은 오브젝트의 ItemPickup에서 읽어옴(ItemData / Amount). 데이터 이중 입력 방지.
//   → 이 컴포넌트는 ItemPickup이 같이 붙어 있는 걸 전제로 함.
// ======================================================

using UnityEngine;

public class ItemPickupVisual : MonoBehaviour
{
	[Header("연출 설정")]
	[Tooltip("획득자에게 날아가는 데 걸리는 시간(초). 거리와 무관하게 항상 이 시간이 걸림.\n" +
			 "0.3~0.5 권장 — 1초를 넘기면 획득이 늦게 느껴져서 답답해짐.")]
	[SerializeField] private float _chaseDuration = 0.45f;

	[Tooltip("날아가는 동안 회전하는 속도(도/초). 0이면 회전 안 함.")]
	[SerializeField] private float _spinSpeed = 540f;

	private ItemPickup _pickup;

	private Transform _target;
	private Vector3 _startPos;
	private float _elapsed;
	private bool _chasing;
	// 중복 지급 방지. 도착 지급과 OnDisable 정산이 겹쳐도 한 번만 나가게 함.
	private bool _granted;

	private void Awake()
	{
		_pickup = GetComponent<ItemPickup>();
	}

	/// <summary>
	/// 획득자에게 날아가는 연출 시작. 도착하면 인벤토리 지급 + 풀 반납까지 자동으로 함.
	/// </summary>
	/// <param name="target">날아갈 대상(보통 내 함선 transform). 매 프레임 현재 위치를 추적함.</param>
	public void StartChase(Transform target)
	{
		_target = target;
		_startPos = transform.position;
		_elapsed = 0f;
		_granted = false;
		_chasing = true;

		// 연출 중엔 일반 픽업 경로를 끔 — 안 그러면 날아가다 내 함선에 스쳐 ItemPickup이 한 번,
		// 도착해서 여기가 또 한 번 지급해 중복됨. 컴포넌트를 끄면 OnTriggerEnter 자체가 안 불림.
		if (_pickup != null)
		{
			_pickup.enabled = false;
		}
	}

	private void Update()
	{
		if (!_chasing)
		{
			return;
		}

		// 일시정지는 플래그 방식이라 timeScale로는 안 멈춤 — 여기서 직접 걸러야 함
		if (GameManager.Instance != null && GameManager.Instance.IsPaused)
		{
			return;
		}

		// 대상이 사라지면(사망·씬 언로드 등) 연출을 접고 지급만 처리 — 아이템은 유실되면 안 됨
		if (_target == null)
		{
			Arrive();
			return;
		}

		_elapsed += Time.deltaTime;
		float t = _chaseDuration > 0f ? Mathf.Clamp01(_elapsed / _chaseDuration) : 1f;

		// t*t로 가속 — 처음엔 천천히 떠오르다 끝에서 빨려들어가는 느낌이 남
		transform.position = Vector3.Lerp(_startPos, _target.position, t * t);

		if (_spinSpeed != 0f)
		{
			transform.Rotate(Vector3.up, _spinSpeed * Time.deltaTime, Space.Self);
		}

		if (t >= 1f)
		{
			Arrive();
		}
	}

	// 도착 처리 — 지급하고 풀에 반납.
	private void Arrive()
	{
		Grant();
		_chasing = false;
		PoolManager.Instance?.Return(gameObject);
	}

	// 실제 인벤토리 지급. _granted로 1회만 나가게 막음.
	private void Grant()
	{
		if (_granted)
		{
			return;
		}
		_granted = true;

		if (_pickup == null || InventoryManager.Instance == null)
		{
			return;
		}
		// null/0 개수 가드는 AddItem 안에 이미 있음
		InventoryManager.Instance.AddItem(_pickup.ItemData, _pickup.Amount);
	}

	// 씬 전환이나 강제 반납으로 연출이 중간에 끊겨도 아이템은 유실되지 않게 여기서 정산함.
	// (풀 최초 생성 시에도 호출되는데, 그땐 _chasing이 false라 그냥 지나감)
	private void OnDisable()
	{
		if (_chasing)
		{
			Grant();
		}
		_chasing = false;
		_target = null;

		// 다음에 이 오브젝트가 일반 픽업으로 재사용될 수 있으니 원래대로 되돌림
		if (_pickup != null)
		{
			_pickup.enabled = true;
		}
	}
}
