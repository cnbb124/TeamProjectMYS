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
//   ('내 함선만 움직이고 남 함선 위치가 보이는' 것까지)
//
// 권위 모델(설계):
//   - 내 함선 입력/이동 : 각자 자기 것(photonView.IsMine)만 조작
//   - 적 스폰/AI/데미지 : Master Client 권위
//   - 투사체           : '쐈다'만 RPC, 각자 로컬 풀에서 생성 
//
// 사용법:
//   1. 로비 등 '가장 먼저 연결을 시작할 씬'에 빈 오브젝트 만들고 이 스크립트 부착 (DDOL로 이후 씬까지 유지)
//   2. 연결 시점:
//      - autoConnectOnStart 켜짐(기존/단독 테스트) : 씬 시작 시 offlineMode 값대로 자동 연결.
//      - autoConnectOnStart 꺼짐(새 흐름) : 스테이션까지 비포톤 유지, 스테이지 입장 UI에서
//        StartSingleplayer()(싱글) 또는 ConnectMultiplayer()(멀티)를 호출. 복귀 시 Disconnect().
//   ※ '무엇을·어디에 스폰'은 이 매니저가 아니라 각 씬의 PlayerSpawner가 결정한다(연결과 스폰 분리).
//     프리팹/위치가 씬마다 다를 수 있으므로(전투기 vs 스테이션 유닛) 씬별 PlayerSpawner에서 설정.
// =====================================================================
public class NetworkManager : MonoBehaviourPunCallbacks
{
    // =====================================================================
    // 싱글톤
    // =====================================================================
    private static NetworkManager instance;
    // Awake에서만 세팅됨. Awake 전엔 null이므로 최초 접근은 Start부터 할 것.
    // (예전엔 여기서 FindObjectOfType으로 찾아줬는데, 그게 매니저 자신의 Awake보다 먼저
    //  instance를 채워버려서 Awake의 초기화 블록이 통째로 스킵되는 버그를 만들었음.
    //  특히 이 매니저는 Awake에서 PhotonNetwork.PrefabPool을 등록하므로, 읽기만 했는데
    //  Photon 전역 설정이 등록되는 부작용까지 났음)
    public static NetworkManager Instance => instance;

    // =====================================================================
    // 설정
    // =====================================================================
    [Header("━━━━━━ 연결 시점 ━━━━━━")]
    [Tooltip("켜면 씬 시작 시 자동 연결(기존 방식 — 씬 단독 테스트/현행 흐름용).\n" +
             "끄면 StartSingleplayer()/ConnectMultiplayer()를 명시 호출할 때까지 연결하지 않음 — 차후 수정 예정 임시.")]
    [SerializeField] private bool autoConnectOnStart = true;

    [Header("━━━━━━ 모드(autoConnectOnStart 켜졌을 때만 사용) ━━━━━━")]
    [Tooltip("autoConnectOnStart가 켜졌을 때의 모드. true=싱글(오프라인 1인 방), false=멀티(온라인).\n" +
             "새 흐름(스테이션에서 런타임 선택)에선 StartSingleplayer/ConnectMultiplayer로 직접 고르므로 이 값은 안 쓰임.")]
    [SerializeField] private bool offlineMode = true;

    [Header("━━━━━━ 방 설정 ━━━━━━")]
    [Tooltip("한 방에 들어올 수 있는 최대 인원.")]
    [SerializeField] private byte maxPlayersPerRoom = 3;

    // =====================================================================
    // 상태
    // =====================================================================
    /// <summary>방 입장 완료(내 함선 스폰 가능) 여부.</summary>
    public bool IsReady { get; private set; }

    /// <summary>내(로컬)가 소유해 스폰한 플레이어 함선. 함선은 DDOL이라 씬 로드에도 유지되며(남이 씬을 로드해도
    /// 그의 로컬에서 파괴되지 않게), 전투씬을 벗어날 때 DestroyLocalPlayerShip으로 명시적으로 제거한다.</summary>
    public GameObject LocalPlayerShip { get; private set; }

    /// <summary>지금 내 함선이 살아있는지(씬 전환에도 유지되므로 스폰 중복 방지 판정에 사용).</summary>
    public bool HasLocalPlayerShip => LocalPlayerShip != null;

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
        // 자동 연결은 '기존 흐름/씬 단독 테스트'용. 새 흐름(스테이션 비포톤)에선 autoConnectOnStart를 끄고,
        // 스테이지 입장 시 UI가 StartSingleplayer()/ConnectMultiplayer()를 직접 호출한다.
        if (!autoConnectOnStart)
        {
            return;
        }
        if (offlineMode)
        {
            StartSingleplayer();
        }
        else
        {
            ConnectMultiplayer();
        }
    }

    // =====================================================================
    // 연결 시작 — 싱글/멀티 명시 선택 (스테이션 등에서 스테이지 입장 시 호출)
    // 방 입장은 오프라인/온라인 공통으로 OnConnectedToMaster에서 처리(입장 경로 단일화).
    // =====================================================================

    /// <summary>싱글플레이 시작(오프라인 = Photon 서버 없이 1인 방). 이미 연결/입장 중이면 무시(멱등).</summary>
    public void StartSingleplayer()
    {
        if (PhotonNetwork.IsConnected || PhotonNetwork.InRoom)
        {
            return;
        }
        // 이 설정만으로 OnConnectedToMaster가 즉시 동기 호출되고, 거기서 방을 만든다(입장 경로 단일화).
        PhotonNetwork.OfflineMode = true;
    }

    /// <summary>멀티플레이 연결(실제 Photon 마스터 서버). 완료되면 OnConnectedToMaster에서 방 입장. 이미 연결/입장 중이면 무시(멱등).</summary>
    public void ConnectMultiplayer()
    {
        if (PhotonNetwork.IsConnected || PhotonNetwork.InRoom)
        {
            return;
        }
        PhotonNetwork.OfflineMode = false;
        PhotonNetwork.ConnectUsingSettings();
    }

    /// <summary>Photon 연결/오프라인 방을 종료(멀티 세션에서 비포톤 씬으로 복귀 시). 연결이 없으면 무시.</summary>
    public void Disconnect()
    {
        if (PhotonNetwork.OfflineMode)
        {
            PhotonNetwork.OfflineMode = false; // 오프라인 방 정리(이 대입이 방 퇴장을 유발)
            return;
        }
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
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
        LocalPlayerShip = PhotonNetwork.Instantiate(prefabName, pos, rot);
        return LocalPlayerShip;
    }

    /// <summary>
    /// 내 함선을 네트워크 전체에서 제거. 전투씬을 벗어나는 모든 경로(스테이션/로비/게임오버/리스타트)에서 호출.
    /// 함선은 DDOL이라 로컬 씬 로드로는 안 죽으므로, 이탈 시 명시적으로 제거해야 다음 씬(스테이션 등)에 남지 않는다.
    /// PhotonNetwork.Destroy라 남들 화면에서도 함께 사라진다("이 플레이어가 전투씬을 떠남"을 올바르게 반영).
    /// </summary>
    public void DestroyLocalPlayerShip()
    {
        if (LocalPlayerShip == null)
        {
            return;
        }
        PhotonView pv = LocalPlayerShip.GetComponent<PhotonView>();
        // PhotonNetwork.Destroy는 '방 안의 네트워크 오브젝트'에만 통함 —
        // 방 밖(오프라인 단독 테스트 등)에서 부르면 실패하고 오브젝트가 그대로 살아남는다.
        // 함선은 PhotonView만 있으면 DDOL이라(Player.Start) 씬 로드로도 안 죽으므로,
        // 방이 아니면 로컬 파괴로 폴백해야 게임오버 씬까지 따라오지 않는다.
        if (pv != null && pv.IsMine && PhotonNetwork.InRoom)
        {
            PhotonNetwork.Destroy(LocalPlayerShip);
        }
        else
        {
            Destroy(LocalPlayerShip);
        }
        LocalPlayerShip = null;
    }
}
