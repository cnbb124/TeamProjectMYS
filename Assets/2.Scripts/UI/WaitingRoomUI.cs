using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PhotonPlayer = Photon.Realtime.Player;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

public class WaitingRoomUI : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const string ReadyPropertyKey = "WaitingRoomReady";
    private const byte StartGameEventCode = 71;

    [Header("Player Slots")]
    [SerializeField] private Transform playerSlotContainer;
    [SerializeField] private PlayerSlotUI playerSlotPrefab;

    [Header("Start Button")]
    [SerializeField] private Button startButton;
    [SerializeField] private int minimumPlayerCount = 1;
    [SerializeField] private bool hostMustBeReady = true;

    [Header("Scene Transition")]
    [SerializeField] private string gameplaySceneName = "STAGE1";
    [SerializeField] private bool useLoadingScene = true;
    [SerializeField] private string loadingSceneName = "LOADING_SEQUENCE";

    private readonly List<PlayerSlotUI> runtimeSlots = new List<PlayerSlotUI>();
    private bool lobbyInitialized;
    private bool isStartingGame;

    private void Awake()
    {
        PreparePlayerSlotContainer();

        if (startButton != null)
        {
            startButton.onClick.AddListener(HandleStartButtonClicked);
            startButton.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            InitializeLobby();
        }
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(HandleStartButtonClicked);
        }
    }

    public override void OnJoinedRoom()
    {
        InitializeLobby();
    }

    public override void OnLeftRoom()
    {
        lobbyInitialized = false;
        isStartingGame = false;
        ClearRuntimeSlots();

        if (startButton != null)
        {
            startButton.gameObject.SetActive(false);
        }
    }

    public override void OnPlayerEnteredRoom(PhotonPlayer newPlayer)
    {
        RebuildPlayerList();
    }

    public override void OnPlayerLeftRoom(PhotonPlayer otherPlayer)
    {
        RebuildPlayerList();
    }

    public override void OnPlayerPropertiesUpdate(
        PhotonPlayer targetPlayer,
        PhotonHashtable changedProps)
    {
        if (changedProps.ContainsKey(ReadyPropertyKey))
        {
            RebuildPlayerList();
        }
    }

    public override void OnMasterClientSwitched(PhotonPlayer newMasterClient)
    {
        RebuildPlayerList();
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != StartGameEventCode || !PhotonNetwork.InRoom)
        {
            return;
        }

        PhotonPlayer masterClient = PhotonNetwork.MasterClient;
        if (masterClient == null || photonEvent.Sender != masterClient.ActorNumber)
        {
            Debug.LogWarning("[WaitingRoomUI] 방장이 아닌 플레이어가 보낸 시작 요청을 무시했습니다.");
            return;
        }

        string requestedScene = photonEvent.CustomData as string;
        if (string.IsNullOrWhiteSpace(requestedScene))
        {
            requestedScene = gameplaySceneName;
        }

        isStartingGame = true;
        RefreshStartButton();
        LoadGameSceneLocally(requestedScene);
    }

    private void InitializeLobby()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        if (!lobbyInitialized)
        {
            lobbyInitialized = true;

            PhotonHashtable initialProperties = new PhotonHashtable
            {
                { ReadyPropertyKey, false }
            };

            PhotonNetwork.LocalPlayer.SetCustomProperties(initialProperties);
        }

        RebuildPlayerList();
    }

    private void PreparePlayerSlotContainer()
    {
        if (playerSlotContainer == null)
        {
            return;
        }

        // 프리팹에서 배치 확인용으로 둔 슬롯은 런타임에 제거한다.
        for (int i = playerSlotContainer.childCount - 1; i >= 0; i--)
        {
            GameObject previewSlot = playerSlotContainer.GetChild(i).gameObject;
            previewSlot.SetActive(false);
            Destroy(previewSlot);
        }
    }

    private void RebuildPlayerList()
    {
        ClearRuntimeSlots();

        if (!PhotonNetwork.InRoom ||
            playerSlotContainer == null ||
            playerSlotPrefab == null)
        {
            RefreshStartButton();
            return;
        }

        PhotonPlayer[] roomPlayers = PhotonNetwork.PlayerList;
        Array.Sort(
            roomPlayers,
            (left, right) => left.ActorNumber.CompareTo(right.ActorNumber));

        foreach (PhotonPlayer roomPlayer in roomPlayers)
        {
            PlayerSlotUI slot = Instantiate(playerSlotPrefab, playerSlotContainer);
            slot.gameObject.SetActive(true);

            slot.Configure(
                roomPlayer.ActorNumber,
                GetPlayerDisplayName(roomPlayer),
                IsPlayerReady(roomPlayer),
                roomPlayer.IsMasterClient,
                roomPlayer.IsLocal,
                HandleReadyButtonClicked);

            runtimeSlots.Add(slot);
        }

        RefreshStartButton();
    }

    private void ClearRuntimeSlots()
    {
        foreach (PlayerSlotUI slot in runtimeSlots)
        {
            if (slot != null)
            {
                slot.gameObject.SetActive(false);
                Destroy(slot.gameObject);
            }
        }

        runtimeSlots.Clear();
    }

    private void HandleReadyButtonClicked(int clickedActorNumber)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
        {
            return;
        }

        if (clickedActorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
        {
            Debug.LogWarning("[WaitingRoomUI] 다른 플레이어의 Ready 버튼 조작을 차단했습니다.");
            return;
        }

        bool nextReadyState = !IsPlayerReady(PhotonNetwork.LocalPlayer);
        PhotonHashtable changedProperties = new PhotonHashtable
        {
            { ReadyPropertyKey, nextReadyState }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(changedProperties);
    }

    private void HandleStartButtonClicked()
    {
        if (!CanStartGame() || isStartingGame)
        {
            return;
        }

        isStartingGame = true;
        RefreshStartButton();

        if (PhotonNetwork.OfflineMode)
        {
            LoadGameSceneLocally(gameplaySceneName);
            return;
        }

        RaiseEventOptions eventOptions = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.All
        };

        bool requestSent = PhotonNetwork.RaiseEvent(
            StartGameEventCode,
            gameplaySceneName,
            eventOptions,
            SendOptions.SendReliable);

        if (!requestSent)
        {
            isStartingGame = false;
            RefreshStartButton();
            Debug.LogError("[WaitingRoomUI] 게임 시작 이벤트 전송에 실패했습니다.");
        }
    }

    private void RefreshStartButton()
    {
        if (startButton == null)
        {
            return;
        }

        bool isHost = PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient;
        startButton.gameObject.SetActive(isHost);
        startButton.interactable = isHost && !isStartingGame && CanStartGame();
    }

    private bool CanStartGame()
    {
        if (!PhotonNetwork.InRoom ||
            !PhotonNetwork.IsMasterClient ||
            PhotonNetwork.CurrentRoom == null ||
            PhotonNetwork.CurrentRoom.PlayerCount < minimumPlayerCount)
        {
            return false;
        }

        foreach (PhotonPlayer roomPlayer in PhotonNetwork.PlayerList)
        {
            if (!hostMustBeReady && roomPlayer.IsMasterClient)
            {
                continue;
            }

            if (!IsPlayerReady(roomPlayer))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPlayerReady(PhotonPlayer player)
    {
        if (player == null ||
            !player.CustomProperties.TryGetValue(ReadyPropertyKey, out object value))
        {
            return false;
        }

        return value is bool isReady && isReady;
    }

    private static string GetPlayerDisplayName(PhotonPlayer player)
    {
        if (player == null)
        {
            return "Player";
        }

        return string.IsNullOrWhiteSpace(player.NickName)
            ? "Player " + player.ActorNumber
            : player.NickName;
    }

    private void LoadGameSceneLocally(string sceneName)
    {
        if (isStartingGame && !string.IsNullOrWhiteSpace(sceneName))
        {
            LoadingManager.NextScene = sceneName;

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.LoadScene(useLoadingScene ? loadingSceneName : sceneName);
            }
            else
            {
                SceneManager.LoadScene(useLoadingScene ? loadingSceneName : sceneName);
            }
        }
    }
}
