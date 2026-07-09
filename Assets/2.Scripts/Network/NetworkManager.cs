using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// ▶ 멀티플레이 연결/방 관리 (Photon PUN2)
//   Instance.IsReady          : 방에 입장 완료(스폰 가능) 상태인지
//   Instance.SpawnLocalPlayer(): 내 함선을 방에 스폰(보통 자동 호출됨)
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
//   1. 게임플레이 씬에 빈 오브젝트 만들고 이 스크립트 부착
//   2. playerPrefabName : Resources 폴더 안의 플레이어 프리팹 이름
//      (PhotonNetwork.Instantiate는 Resources 폴더 프리팹만 스폰 가능)
//   3. spawnPoints : 스폰 위치들(비어있으면 원점에 스폰)
//   4. offlineMode : 싱글플레이 테스트면 체크(기본값)
// =====================================================================
public class NetworkManager : MonoBehaviourPunCallbacks
{
    // =====================================================================
    // 싱글톤
    // =====================================================================
    public static NetworkManager Instance { get; private set; }

    // =====================================================================
    // 설정
    // =====================================================================
    [Header("━━━━━━ 모드 ━━━━━━")]
    [Tooltip("켜면 Photon 서버에 붙지 않고 '1인 방'으로 취급(싱글플레이). 같은 코드로 싱글/멀티 둘 다 돌아감.")]
    [SerializeField] private bool offlineMode = true;

    [Header("━━━━━━ 플레이어 스폰 ━━━━━━")]
    [Tooltip("Resources 폴더 안의 플레이어 프리팹 이름. PhotonNetwork.Instantiate는 Resources 프리팹만 스폰 가능.")]
    [SerializeField] private string playerPrefabName = "Player";
    [Tooltip("스폰 위치들. 여러 명이면 입장 순서대로 하나씩 배정. 비어있으면 원점에 스폰.")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("━━━━━━ 방 설정 ━━━━━━")]
    [Tooltip("한 방에 들어올 수 있는 최대 인원.")]
    [SerializeField] private byte maxPlayersPerRoom = 3;

    // =====================================================================
    // 상태
    // =====================================================================
    /// <summary>방 입장 완료(내 함선 스폰 가능) 여부.</summary>
    public bool IsReady { get; private set; }

    // =====================================================================
    // 초기화
    // =====================================================================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        Connect();
    }

    /// <summary>모드에 따라 오프라인(싱글) 또는 온라인(멀티)으로 연결 시작.</summary>
    private void Connect()
    {
        if (offlineMode)
        {
            // 오프라인 모드 = Photon 서버 없이 로컬에서 1인 방을 즉시 만든다.
            // CreateRoom 호출 즉시 OnJoinedRoom이 동기적으로 불린다.
            PhotonNetwork.OfflineMode = true;
            PhotonNetwork.CreateRoom("Offline");
            return;
        }

        // 온라인 모드 = 실제 Photon 마스터 서버에 연결.
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    // =====================================================================
    // Photon 콜백
    // =====================================================================
    public override void OnConnectedToMaster()
    {
        Debug.Log("[NetworkManager] 마스터 서버 연결 성공 → 방 입장 시도");
        // 아무 방이나 입장, 없으면 새로 만든다.
        PhotonNetwork.JoinRandomOrCreateRoom(
            roomOptions: new RoomOptions { MaxPlayers = maxPlayersPerRoom });
    }

    public override void OnJoinedRoom()
    {
        IsReady = true;
        Debug.Log($"[NetworkManager] 방 입장 완료 (인원 {PhotonNetwork.CurrentRoom.PlayerCount}/{maxPlayersPerRoom}, Master={PhotonNetwork.IsMasterClient})");
        SpawnLocalPlayer();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        IsReady = false;
        Debug.LogWarning("[NetworkManager] 연결 끊김: " + cause);
    }

    // =====================================================================
    // 플레이어 스폰
    // =====================================================================
    /// <summary>내 함선을 방에 스폰. 각 클라가 자기 것만 스폰하며 photonView.IsMine으로 소유권이 갈린다.</summary>
    public void SpawnLocalPlayer()
    {
        if (!IsReady)
        {
            Debug.LogWarning("[NetworkManager] 아직 방에 입장하지 않아 스폰 불가");
            return;
        }

        Transform spawn = GetSpawnPoint();
        Vector3 pos = spawn != null ? spawn.position : Vector3.zero;
        Quaternion rot = spawn != null ? spawn.rotation : Quaternion.identity;

        // Resources 폴더 프리팹을 방 전원에게 생성. 소유권은 이 클라(스폰 주체)에게 있다.
        PhotonNetwork.Instantiate(playerPrefabName, pos, rot);
    }

    /// <summary>입장 순서(내 순번)에 맞는 스폰 위치를 고른다. 부족하면 순환.</summary>
    private Transform GetSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return null;
        }
        // 내 순번 = 방에 나보다 먼저 들어온 사람 수(대략) → PlayerCount-1로 근사.
        int index = (PhotonNetwork.CurrentRoom.PlayerCount - 1) % spawnPoints.Length;
        return spawnPoints[index];
    }
}
