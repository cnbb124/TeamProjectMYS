using UnityEngine;

// =====================================================================
// HangarShipPresenter — 격납고에 스폰된 내 함선을 착륙 연출 앵커에 물림.
//
// 격납고에서도 함선을 정식 스폰해야 장착이 전투씬까지 이어짐(씬 배치본은 DDOL이 안 돼 씬과 함께 사라짐).
// 조종 차단은 여기가 아니라 GameManager.ShipControlDisabled를 보는 Unit.ShouldPause / InputManager가 함.
//
// [에디터 세팅]
//   1. 격납고 씬 빈 오브젝트에 부착.
//   2. 같은 씬에 PlayerSpawner 배치.
//   3. _poseAnchor : ShipLanding.target과 같은 오브젝트를 지정.
// =====================================================================
public class HangarShipPresenter : MonoBehaviour
{
	[Header("<size=14>착륙 연출 앵커</size>")]
	[Tooltip("ShipLanding이 움직이는 트랜스폼. 비우면 이 오브젝트 자신을 씀.")]
	[SerializeField] private Transform _poseAnchor;

	[Header("<size=14>탐색</size>")]
	[Tooltip("함선을 못 찾았을 때 다시 찾는 간격(초).")]
	[SerializeField] private float _acquireInterval = 0.25f;

	private Player _ship;
	private float _acquireTimer;

	private void Awake()
	{
		if (_poseAnchor == null)
		{
			_poseAnchor = transform;
		}
	}

	// 앵커를 움직이는 ShipLanding 코루틴이 Update 직후 재개되므로 LateUpdate에서 읽음.
	// 자식으로 붙이지 않는 이유 — 씬 오브젝트 밑으로 들어가면 DDOL이 풀려 씬과 함께 파괴됨.
	private void LateUpdate()
	{
		if (_ship == null)
		{
			TryAcquireShip();
			if (_ship == null)
			{
				return;
			}
		}

		_ship.transform.SetPositionAndRotation(_poseAnchor.position, _poseAnchor.rotation);
	}

	// 진입 시점엔 아직 스폰 전일 수 있어 주기적으로 재시도함. 남의 함선을 잡지 않게 playerRef만 봄.
	private void TryAcquireShip()
	{
		_acquireTimer -= Time.deltaTime;
		if (_acquireTimer > 0f)
		{
			return;
		}
		_acquireTimer = _acquireInterval;

		Player found = GameManager.Instance != null ? GameManager.Instance.playerRef : null;
		if (found == null)
		{
			return;
		}

		_ship = found;
		Debug.Log($"[HangarShipPresenter] 함선 확보: {_ship.name}");
	}
}
