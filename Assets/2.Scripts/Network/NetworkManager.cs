using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// ▶ 멀티플레이 연결/방 관리 (Photon PUN2)
//   Instance.IsReady          : 방에 입장 완료(스폰 가능) 상태인지
//   Instance.OnRoomReady      : 방 입장 완료 이벤트 (씬별 PlayerSpawner가 구독)
//   Instance.SpawnPlayer(name, pos, rot) : 지정 프리팹을 내 소유로 스폰 (보통 PlayerSpawner가 호출)
//
// ▶ 싱글플레이(OfflineMode)
//   offlineMode = true 이면 Photon 서버에 붙지 않고 '1인 방'으로 취급.
//   같은 코드가 싱글/멀티 양쪽에서 돌아가므로 분기를 최소화한다.
// ================================================================

// =====================================================================
// NetworkManager
//
// 역할:
//   Photon 연결 → 방 입장 → 내 함선 스폰까지의 최소 흐름을 담당.
//   (ⓐ단계: '내 함선만 움직이고 남 함선 위치가 보이는' 것까지)
//
// 권위 모델(설계):
//   - 내 함선 입력/이동 : 각자 자기 것(photonView.IsMine)만 조작
//   - 적 스폰/AI/데미지 : Master Client 권위 (ⓑ/ⓒ단계에서 적용 예정)
//   - 투사체           : '쐈다'만 RPC, 각자 로컬 풀에서 생성 (ⓒ단계)
//
// 사용법:
//   1. 로비 등 '가장 먼저 연결을 시작할 씬'에 빈 오브젝트 만들고 이 스크립트 부착 (DDOL로 이후 씬까지 유지)
//   2. offlineMode : 싱글플레이 테스트면 체크(기본값)
//   ※ '무엇을·어디에 스폰'은 이 매니저가 아니라 각 씬의 PlayerSpawner가 결정한다(연결과 스폰 분리).
//     프리팹/위치가 씬마다 다를 수 있으므로(전투기 vs 스테이션 유닛) 씬별 PlayerSpawner에서 설정.
// =====================================================================
public class NetworkManager : MonoBehaviourPunCallbacks
{
    // =====================================================================
    // 싱글톤
    // =====================================================================
    private static NetworkManager instance;
    public static NetworkManager Instance
    {
		get
		{
			if (instance == null)
			{
				instance = FindObjectOfType<NetworkManager>();
				if (instance == null)
				{
					Debug.Log("씬에 NetworkManager 누락! 하이어라키에 추가 필요");
				}
			}
			return instance;
		}
	}

    // =====================================================================
    // 설정
    // =====================================================================
    [Header("━━━━━━ 모드 ━━━━━━")]
    [Tooltip("켜면 Photon 서버에 붙지 않고 '1인 방'으로 취급(싱글플레이). 같은 코드로 싱글/멀티 둘 다 돌아감.")]
    [SerializeField] private bool offlineMode = true;

    [Header("━━━━━━ 방 설정 ━━━━━━")]
    [Tooltip("한 방에 들어올 수 있는 최대 인원.")]
    [SerializeField] private byte maxPlayersPerRoom = 3;

    // =====================================================================
    // 상태
    // =====================================================================
    /// <summary>방 입장 완료(내 함선 스폰 가능) 여부.</summary>
    public bool IsReady { get; private set; }

    /// <summary>방 입장 완료 순간 발생. 씬별 PlayerSpawner가 구독해 스폰 타이밍을 잡는다(연결과 스폰 분리).</summary>
    public event System.Action OnRoomReady;

    // =====================================================================
    // 초기화
    // =====================================================================
    private void Awake()
    {
		if (instance == null)
		{
			instance = this;
			// 연결/방 입장 상태는 로비 → 게임플레이 씬 전환에도 유지되어야 하므로 DDOL.
			// (웨이브·카메라처럼 씬마다 초기화되는 SpawnManager/CameraShaker와는 정반대 성격)
			DontDestroyOnLoad(gameObject);

			// 네트워크 오브젝트(적 등)를 로컬 PoolManager로 재사용하도록 커스텀 풀 등록.
			// 풀 대상이 아닌 것(플레이어 등)은 어댑터 내부에서 기본 방식(Resources)으로 폴백.
			PhotonNetwork.PrefabPool = new PhotonPoolAdapter();

			// 이 게임은 플레이어마다 다른 씬에 있을 수 있음(한 명 스테이션, 한 명 스테이지)이라
			// AutomaticallySyncScene(전원 씬 강제 통일)은 쓰지 않는다. 대신 각자 자기 씬을 룸에 알리고
			// (PublishLocalScene), 같은 씬끼리만 서로 보이게 필터한다(PlayerSceneVisibility).
			SceneManager.sceneLoaded += OnSceneLoaded;
		}
		else if (instance != this)
		{
			Debug.LogWarning("중복된 NetworkManager 발견. 파괴 후 실행");
			Destroy(gameObject);
			return;
		}
	}

    private void Start()
    {
        Connect();
    }

    /// <summary>모드에 따라 '연결'만 시작한다. 방 입장은 오프라인/온라인 공통으로 OnConnectedToMaster에서 처리(입장 경로 단일화).</summary>
    private void Connect()
    {
        if (offlineMode)
        {
            // 오프라인 = Photon 서버 없이 로컬 시뮬레이션. 이 설정만으로 OnConnectedToMaster가 즉시 불린다.
            // 방 입장은 온라인과 똑같이 그 콜백에서 하므로 여기선 방을 만들지 않는다(이중 입장 방지).
            PhotonNetwork.OfflineMode = true;
            return;
        }

        // 온라인 = 실제 Photon 마스터 서버에 접속. 완료되면 OnConnectedToMaster가 불린다.
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    // =====================================================================
    // Photon 콜백
    // =====================================================================
    // 오프라인/온라인 모두 '연결 완료' 시 여기로 온다(오프라인은 OfflineMode=true 설정 즉시 동기 호출).
    // 방 입장을 이 한 곳에서만 처리해 이중 입장("leave a room to enter another")을 구조적으로 차단한다.
    public override void OnConnectedToMaster()
    {
        if (PhotonNetwork.InRoom)
        {
            // 이미 방에 있으면(재콜백/중복 진입) 무시 — 멱등성 보장.
            return;
        }

        if (PhotonNetwork.OfflineMode)
        {
            // 오프라인은 매칭이 없으므로 로컬 방 하나만 만든다.
            PhotonNetwork.CreateRoom(null);
        }
        else
        {
            Debug.Log("[NetworkManager] 마스터 서버 연결 성공 → 방 입장 시도");
            // 아무 방이나 입장, 없으면 새로 만든다.
            PhotonNetwork.JoinRandomOrCreateRoom(roomOptions: new RoomOptions { MaxPlayers = maxPlayersPerRoom });
        }
    }

    public override void OnJoinedRoom()
    {
        IsReady = true;
        string modeLabel = PhotonNetwork.OfflineMode ? "싱글(오프라인)" : "멀티(온라인)";
        Debug.Log($"[NetworkManager] 방 입장 완료 [{modeLabel}] (인원 {PhotonNetwork.CurrentRoom.PlayerCount}/{maxPlayersPerRoom}, Master={PhotonNetwork.IsMasterClient})");
        PublishLocalScene();   // 입장 시점의 내 현재 씬을 룸에 알림(가시성 필터용)
        // 스폰은 이 매니저가 하지 않는다 — 준비됐다고 알리기만 하고, 무엇을·어디에 스폰할지는 씬별 PlayerSpawner가 결정.
        OnRoomReady?.Invoke();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        IsReady = false;
        Debug.LogWarning("[NetworkManager] 연결 끊김: " + cause);
    }

    // =====================================================================
    // 씬 가시성 — 각자 자기 씬을 룸에 공유. 같은 씬끼리만 서로 보이게(PlayerSceneVisibility가 이 값을 봄).
    // =====================================================================
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PublishLocalScene();
    }

    /// <summary>내가 지금 어느 씬에 있는지를 룸 전체에 알린다(룸 밖이면 무시).</summary>
    private void PublishLocalScene()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }
        var props = new ExitGames.Client.Photon.Hashtable
        {
            { PlayerSceneVisibility.SCENE_KEY, SceneManager.GetActiveScene().name }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    // =====================================================================
    // 플레이어 스폰 (호출자 = 씬별 PlayerSpawner)
    // =====================================================================
    /// <summary>
    /// 지정 프리팹을 내 소유로 방에 스폰. '무엇을·어디에'는 호출자(PlayerSpawner)가 결정
    /// 각 클라가 자기 것만 스폰하며 photonView.IsMine으로 소유권이 r갈림
    /// </summary>
    /// <param name="prefabName">Resources 폴더 안의 프리팹 이름(PhotonNetwork.Instantiate 제약).</param>
    public GameObject SpawnPlayer(string prefabName, Vector3 pos, Quaternion rot)
    {
        if (!IsReady)
        {
            Debug.LogWarning("[NetworkManager] 아직 방에 입장하지 않아 스폰 불가");
            return null;
        }

        // Resources 폴더 프리팹을 방 전원에게 생성. 소유권은 이 클라(스폰 주체)에게 있다.
        return PhotonNetwork.Instantiate(prefabName, pos, rot);
    }
}
