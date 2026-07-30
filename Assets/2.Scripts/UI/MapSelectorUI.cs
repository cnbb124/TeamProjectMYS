using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

// 맵 선택 UI. 멀티에서는 방장(Master)만 맵을 고를 수 있고, 선택 시 RaiseEvent로 전원이 동시에 같은 스테이지로 진입함.
// (WaitingRoomUI의 시작 버튼 호스트 게이팅과 동일한 패턴 — 각자 따로 진입하던 임시 동작을 호스트 권위로 교체함.)
// 맵 버튼의 onClick은 코드(Awake)에서 연결함 → 인스펙터 onClick은 비워둘 것.
public class MapSelectorUI : MonoBehaviourPunCallbacks, IOnEventCallback
{
	private const byte MapSelectEventCode = 72; // WaitingRoomUI(71)와 겹치지 않는 별도 코드

	[Header("맵 목록 (버튼별 진입 씬)")]
	[Tooltip("버튼 i가 진입시킬 씬을 담은 데이터. Hub의 씬 생성이 여기에 씬을 배정함.")]
	[SerializeField] private MapListData mapList;

	[Header("맵 버튼 (방장만 활성)")]
	[Tooltip("순서대로 mapList.maps[i]에 대응됨(0번 버튼 = maps[0]).\n" +
			 "onClick은 코드(Awake)에서 연결함 → 인스펙터 onClick은 비워둘 것(중복 호출 방지).\n" +
			 "mapList에서 씬이 안 잡힌 버튼과 비방장은 interactable=false로 잠김.")]
	[SerializeField] private Button[] mapButtons;

	private bool isStartingGame;

	private void Awake()
	{
		// 버튼마다 자기 인덱스를 캡처해 onClick에 연결 — 인스펙터 onClick은 비워둠.
		// 씬 이름은 클릭 시점에 mapList에서 조회하므로, 배정이 바뀌어도 코드 수정이 필요 없음.
		if (mapButtons == null)
		{
			return;
		}
		for (int i = 0; i < mapButtons.Length; i++)
		{
			if (mapButtons[i] == null)
			{
				continue;
			}
			int index = i;
			mapButtons[i].onClick.AddListener(() => OnClickMapButton(index));
		}
	}

	private void OnDestroy()
	{
		if (mapButtons == null)
		{
			return;
		}
		for (int i = 0; i < mapButtons.Length; i++)
		{
			if (mapButtons[i] != null)
			{
				mapButtons[i].onClick.RemoveAllListeners();
			}
		}
	}

	// 버튼 인덱스 → mapList에서 씬 이름 조회 → 진입 요청.
	private void OnClickMapButton(int index)
	{
		string sceneName = mapList != null ? mapList.GetSceneName(index) : null;
		if (string.IsNullOrWhiteSpace(sceneName))
		{
			Debug.LogWarning($"[MapSelectorUI] {index}번 버튼에 배정된 씬이 없음.");
			return;
		}
		RequestMapStart(sceneName);
	}

	private void Start()
	{
		RefreshButtons();
	}

	public override void OnJoinedRoom()
	{
		RefreshButtons();
	}

	public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
	{
		RefreshButtons();
	}

	// 방장만 실제 진입을 트리거함. 멀티면 RaiseEvent로 전원 동시 진입, 싱글(오프라인)이면 로컬 진입.
    private void RequestMapStart(string sceneName)
	{
		if (isStartingGame || string.IsNullOrWhiteSpace(sceneName))
		{
			return;
		}

		// 방장이 아니면 무시(버튼은 잠겨있지만 이중 안전장치).
		if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
		{
			Debug.LogWarning("[MapSelectorUI] 방장이 아닌 플레이어의 맵 선택을 무시했습니다.");
			return;
		}

		isStartingGame = true;

		// 싱글(오프라인) 또는 룸 밖 → 로컬 진입.
		if (!PhotonNetwork.InRoom || PhotonNetwork.OfflineMode)
		{
			LoadStageLocally(sceneName);
			return;
		}

		// 멀티 → 방장이 전원에게 브로드캐스트. 자기 자신도 OnEvent로 받아서 함께 로드함.
		RaiseEventOptions eventOptions = new RaiseEventOptions
		{
			Receivers = ReceiverGroup.All
		};

		bool requestSent = PhotonNetwork.RaiseEvent(
			MapSelectEventCode,
			sceneName,
			eventOptions,
			SendOptions.SendReliable);

		if (!requestSent)
		{
			isStartingGame = false;
			Debug.LogError("[MapSelectorUI] 맵 선택 이벤트 전송에 실패했습니다.");
		}
	}

	public void OnEvent(EventData photonEvent)
	{
		if (photonEvent.Code != MapSelectEventCode || !PhotonNetwork.InRoom)
		{
			return;
		}

		// 방장이 보낸 것만 신뢰(비방장 위조 요청 차단).
		Photon.Realtime.Player masterClient = PhotonNetwork.MasterClient;
		if (masterClient == null || photonEvent.Sender != masterClient.ActorNumber)
		{
			Debug.LogWarning("[MapSelectorUI] 방장이 아닌 플레이어가 보낸 맵 선택 요청을 무시했습니다.");
			return;
		}

		string requestedScene = photonEvent.CustomData as string;
		if (string.IsNullOrWhiteSpace(requestedScene))
		{
			return;
		}

		isStartingGame = true;
		LoadStageLocally(requestedScene);
	}

	private void LoadStageLocally(string sceneName)
	{
		LoadingManager.NextScene = sceneName;
		GameManager.Instance.LoadScene(SCENE_TYPE.LOADING_SEQUENCE);
	}

	// 방장만 맵 버튼 활성. 싱글(오프라인)은 LocalPlayer가 곧 Master라 항상 활성됨.
	// 추가로, mapList에서 씬이 배정되지 않은 버튼은 방장이라도 잠금(빈 버튼 클릭 방지).
	private void RefreshButtons()
	{
		bool canSelect = !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;

		if (mapButtons == null)
		{
			return;
		}
		for (int i = 0; i < mapButtons.Length; i++)
		{
			if (mapButtons[i] == null)
			{
				continue;
			}
			bool hasScene = mapList != null && !string.IsNullOrWhiteSpace(mapList.GetSceneName(i));
			mapButtons[i].interactable = canSelect && hasScene;
		}
	}
}
