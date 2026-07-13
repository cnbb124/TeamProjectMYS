/*
 * [PhotonChatManager]
 * ChatUI(채팅창) ↔ 포톤 채팅 서버를 잇는 다리.
 *
 * [데이터 흐름]
 *   내가 입력 → ChatUI.onSendRequested → 여기서 PublishMessage → 포톤 서버
 *   포톤 서버 → OnGetMessages 콜백 → ChatUI.AddMessage → 화면 표시
 *   (내가 보낸 것도 서버가 되돌려줘서 표시됨 — 그래서 연결 중엔 localEcho를 끔)
 *
 * [사용법]
 * 1. 채팅 UI가 있는 씬에서 이 스크립트를 오브젝트에 부착, chatUI 연결
 *    (또는 메뉴 Tools > MYS > 채팅 UI 생성 — UI부터 이것까지 통째로 만들어줌)
 * 2. ★ 포톤 대시보드(dashboard.photonengine.com)에서 "Chat" 타입 앱 생성 →
 *    그 App ID를 PhotonServerSettings의 App Id Chat 칸에 붙여넣기 (지금 비어있음!)
 * 3. 플레이 → 자동 연결 → 같은 채널에 접속한 사람들과 채팅
 *
 * [주의]
 * - App Id Chat이 비어있으면 연결 안 하고 채팅창에 안내만 띄움 (로컬 에코는 그대로 동작)
 * - 게임용 App Id(Realtime)와 채팅용 App Id(Chat)는 별개! 대시보드에서 따로 만들어야 함
 */

using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;               // PhotonServerSettings 읽기용
using Photon.Chat;              // ChatClient, IChatClientListener
using ExitGames.Client.Photon;  // DebugLevel

public class PhotonChatManager : MonoBehaviour, IChatClientListener
{
    [Header("━━━━━━ 연결 대상 ━━━━━━")]
    [SerializeField] private ChatUI chatUI;

    [Header("━━━━━━ 채팅 설정 ━━━━━━")]
    [Tooltip("채팅 채널 이름. 같은 채널에 들어온 사람끼리만 대화됨")]
    [SerializeField] private string channel = "lobby";

    [Tooltip("접속 지역 (asia = 한국에서 가장 빠름)")]
    [SerializeField] private string chatRegion = "asia";

    [Tooltip("채팅 닉네임. 비우면 로그인된 userId 기반(User3 등), 그것도 없으면 Guest+랜덤")]
    [SerializeField] private string nickname = "";

    private ChatClient _chatClient;
    private bool _subscribed;

    // =====================================================================
    // 시작 — ChatUI에 전송 이벤트 물리고 포톤 접속
    // =====================================================================
    private void Start()
    {
        if (chatUI == null) chatUI = FindObjectOfType<ChatUI>(true);
        if (chatUI == null)
        {
            Debug.LogError("[PhotonChat] ChatUI를 못 찾음 — 인스펙터에서 연결하세요");
            enabled = false;
            return;
        }

        chatUI.onSendRequested += OnSendRequested;   // 입력창 Enter → 나한테 옴
        Connect();
    }

    private void Connect()
    {
        string appIdChat = PhotonNetwork.PhotonServerSettings.AppSettings.AppIdChat;
        if (string.IsNullOrEmpty(appIdChat))
        {
            // 채팅용 App ID가 아직 없음 → 로컬 에코 모드로만 동작 (안내 표시)
            chatUI.AddSystemMessage("포톤 채팅 미연결 (App Id Chat 비어있음) — 지금은 혼자 보이는 로컬 모드");
            chatUI.AddSystemMessage("연결법: dashboard.photonengine.com에서 Chat 앱 생성 → PhotonServerSettings의 App Id Chat에 입력");
            return;
        }

        // 닉네임 정하기: 인스펙터 값 > 로그인된 유저번호 > 게스트+랜덤
        string nick = nickname;
        if (string.IsNullOrEmpty(nick))
            nick = (ServerApi.Instance != null && ServerApi.Instance.IsLoggedIn)
                ? $"User{ServerApi.Instance.UserId}"
                : $"Guest{Random.Range(1000, 9999)}";

        _chatClient = new ChatClient(this) { ChatRegion = chatRegion };
        _chatClient.Connect(appIdChat, "1.0", new Photon.Chat.AuthenticationValues(nick));
        chatUI.AddSystemMessage("채팅 서버 연결 중...");
    }

    // 포톤 채팅은 Service()를 계속 돌려줘야 송수신이 처리됨 (심장박동)
    private void Update() => _chatClient?.Service();

    private void OnDestroy()
    {
        if (chatUI != null) chatUI.onSendRequested -= OnSendRequested;
        _chatClient?.Disconnect();
    }

    // =====================================================================
    // 보내기: ChatUI 입력 → 포톤으로
    // =====================================================================
    private void OnSendRequested(string message)
    {
        if (_subscribed)
            _chatClient.PublishMessage(channel, message);
        // 미연결이면 아무것도 안 함 — ChatUI의 localEcho가 화면 표시를 담당
    }

    // =====================================================================
    // 포톤 콜백들 (IChatClientListener)
    // =====================================================================
    public void OnConnected()
    {
        _chatClient.Subscribe(new[] { channel });
    }

    public void OnSubscribed(string[] channels, bool[] results)
    {
        _subscribed = true;
        chatUI.LocalEcho = false;   // 이제 내 메시지는 서버가 되돌려줌 → 이중표시 방지
        chatUI.AddSystemMessage($"채팅 입장 완료 ({channel})");
    }

    public void OnGetMessages(string channelName, string[] senders, object[] messages)
    {
        for (int i = 0; i < senders.Length; i++)
            chatUI.AddMessage(senders[i], messages[i]?.ToString() ?? "");
    }

    public void OnDisconnected()
    {
        _subscribed = false;
        if (chatUI != null)
        {
            chatUI.LocalEcho = true;    // 연결 끊기면 다시 로컬 표시 모드
            chatUI.AddSystemMessage("채팅 서버 연결 끊김");
        }
    }

    public void OnUserSubscribed(string channelName, string user)
        => chatUI.AddSystemMessage($"{user} 입장");

    public void OnUserUnsubscribed(string channelName, string user)
        => chatUI.AddSystemMessage($"{user} 퇴장");

    public void DebugReturn(DebugLevel level, string message)
    {
        if (level == DebugLevel.ERROR)        Debug.LogError("[PhotonChat] " + message);
        else if (level == DebugLevel.WARNING) Debug.LogWarning("[PhotonChat] " + message);
    }

    // ── 이하 지금은 안 쓰는 콜백 (인터페이스 규격상 구현만) ──
    public void OnChatStateChange(ChatState state) { }
    public void OnPrivateMessage(string sender, object message, string channelName) { }
    public void OnUnsubscribed(string[] channels) { }
    public void OnStatusUpdate(string user, int status, bool gotMessage, object message) { }
    public void OnChannelPropertiesChanged(string channel, string senderUserId, Dictionary<object, object> properties) { }
    public void OnUserPropertiesChanged(string channel, string targetUserId, string senderUserId, Dictionary<object, object> properties) { }
    public void OnErrorInfo(string channel, string error, object data)
        => Debug.LogWarning($"[PhotonChat] 채널 오류 ({channel}): {error}");
    public void OnReceiveBroadcastMessage(string channel, byte[] message) { }
}
