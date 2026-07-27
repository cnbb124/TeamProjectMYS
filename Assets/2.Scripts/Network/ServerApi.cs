using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// ▶ 서버 저장/로드 — 세이브 슬롯(0~9) 지원. 슬롯 안 넘기면 0번 슬롯 사용(임시 테스트용 기본값).
//   Instance.SaveCo(saveData, onSuccess, onError, slot) : 서버에 저장
//   Instance.LoadCo(onSuccess, onError, slot)           : 서버에서 로드
//   Instance.ListSavesCo(onSuccess, onError)            : 슬롯 목록(로드 화면용 — 슬롯/레벨/골드/시각)
//   IsLoggedIn / UserId                                 : 로그인 여부 / 내 고유번호
//
// ▶ 로그인 UI 만들 때
//   Instance.RegisterCo(id, pw, ...) : 회원가입
//   Instance.LoginCo(id, pw, ...)    : 로그인
// ================================================================

// =====================================================================
// ServerApi
//
// 역할:
//   C# API 서버(Server/Api, ASP.NET)와 HTTP(UnityWebRequest)로 통신.
//   창구: /register(가입) /login(로그인) /save/{id}/{slot}(저장) /load/{id}/{slot}(로드)
//         /saves/{id}(슬롯 목록, 로드 화면용)
//
// 데이터 흐름:
//   [유니티] --JSON--> [API 서버] --SQL--> [MySQL DB]
//   SaveData를 JsonUtility.ToJson으로 쪽지화해 보내면 서버가 DB에 나눠 담고,
//   로드 시 서버가 SaveData와 똑같은 모양의 JSON으로 돌려줌 → FromJson으로 복원.
//
// 사용법:
//   1. 씬에 ServerApi 프리팹 배치 (Assets/3.Prefabs/Network/ServerApi.prefab)
//   2. 서버 주소는 건드릴 필요 없음 — 게임 시작 시 Tailscale 주소와 LAN 주소를
//      순서대로 확인해서 되는 쪽을 자동으로 씀 (집=Tailscale, 학원=둘 중 되는 쪽)
//   3. 로그인 UI가 아직 없으면 autoLoginOnStart 체크하면
//      시작 시 자동으로 로그인 (계정 없으면 자동 가입 후 로그인)
//
// 서버 켜는 법: Server/공부노트.md 7번 참고 (dotnet run)
// =====================================================================
public class ServerApi : MonoBehaviour
{
    // =====================================================================
    // 싱글톤
    // =====================================================================
    public static ServerApi Instance { get; private set; }

    // =====================================================================
    // 설정
    // =====================================================================
    [Header("━━━━━━ 서버 주소 (자동 선택) ━━━━━━")]
    [Tooltip("Tailscale 주소. 집 등 외부에서도 접속되지만 접속하는 PC에 Tailscale 설치·로그인이 되어 있어야 함")]
    [SerializeField] private string serverUrlTailscale = "http://home-20251010.tail46e863.ts.net:5080";

    [Tooltip("학원 내부망(LAN) 주소. Tailscale 없이도 되지만 미니PC와 같은 공유기에 있을 때만 됨")]
    [SerializeField] private string serverUrlLan = "http://192.168.0.8:5080";

    [Tooltip("연결 확인에 기다리는 시간(초). 이 시간 안에 응답 없으면 다른 주소로 넘어감")]
    [SerializeField] private int probeTimeoutSec = 3;

    /// <summary>실제로 쓰는 주소. 첫 요청 전에 두 주소를 확인해서 되는 쪽으로 정해짐.</summary>
    private string serverUrl;

    /// <summary>주소 자동 선택이 끝났는지. 끝나기 전에 요청이 오면 먼저 선택부터 함.</summary>
    private bool _urlResolved;

    [Header("━━━━━━ 자동 로그인 (임시) ━━━━━━")]
    [Tooltip("로그인 UI가 생기기 전까지의 임시 기능. 켜두면 게임 시작 시 아래 계정으로 자동 로그인 (계정 없으면 자동 가입)")]
    [SerializeField] private bool autoLoginOnStart = false;
    [SerializeField] private string autoUsername = "test";
    [SerializeField] private string autoPassword = "1234";

    // =====================================================================
    // 로그인 상태
    // =====================================================================
    /// <summary>로그인 후 서버가 발급한 내 고유번호. 0이면 아직 로그인 안 됨.</summary>
    public long UserId { get; private set; }
    public bool IsLoggedIn => UserId > 0;

    /// <summary>지금 실제로 쓰고 있는 서버 주소 (자동 선택 결과). 아직 미정이면 빈 문자열.</summary>
    public string CurrentServerUrl => serverUrl ?? "";

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
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 게임 켜지자마자 미리 주소를 정해둠 (로그인 버튼 눌렀을 때 기다리는 시간을 줄이려고)
        StartCoroutine(ResolveThenAutoLoginCo());
    }

    private IEnumerator ResolveThenAutoLoginCo()
    {
        yield return ResolveServerUrlCo();

        if (autoLoginOnStart)
        {
            yield return AutoLoginCo();
        }
    }

    // =====================================================================
    // 서버 주소 자동 선택
    //
    // Tailscale 주소 → LAN 주소 순서로 /health 를 찔러보고, 먼저 응답하는 쪽을 씀.
    // - 집/외부: Tailscale만 되므로 Tailscale로 결정
    // - 학원에서 Tailscale 안 깐 PC: Tailscale 실패 → LAN으로 결정
    // 둘 다 실패하면 Tailscale 주소를 기본값으로 두고 진행 (에러 메시지는 실제 요청에서 나옴)
    // =====================================================================
    private IEnumerator ResolveServerUrlCo()
    {
        if (_urlResolved) yield break;

        foreach (string candidate in new[] { serverUrlTailscale, serverUrlLan })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;

            bool alive = false;
            yield return ProbeCo(candidate, ok => alive = ok);

            if (alive)
            {
                serverUrl    = candidate;
                _urlResolved = true;
                Debug.Log($"[ServerApi] 서버 주소 결정: {serverUrl}");
                yield break;
            }

            Debug.Log($"[ServerApi] {candidate} 응답 없음 — 다음 주소 시도");
        }

        // 둘 다 실패 — 일단 Tailscale 주소로 두고, 실제 요청에서 에러가 보이게 함
        serverUrl    = serverUrlTailscale;
        _urlResolved = true;
        Debug.LogWarning("[ServerApi] 두 주소 모두 응답 없음. 서버가 꺼져 있거나 네트워크 문제일 수 있음 " +
                         $"(Tailscale: {serverUrlTailscale} / LAN: {serverUrlLan})");
    }

    /// <summary>그 주소의 /health 가 응답하는지 짧게 확인.</summary>
    private IEnumerator ProbeCo(string baseUrl, Action<bool> onResult)
    {
        using UnityWebRequest req = UnityWebRequest.Get(baseUrl + "/health");
        req.timeout = probeTimeoutSec;
        yield return req.SendWebRequest();
        onResult(req.result == UnityWebRequest.Result.Success);
    }

    /// <summary>요청 보내기 전에 주소가 정해졌는지 확인 (아직이면 지금 정함).</summary>
    private IEnumerator EnsureUrlCo()
    {
        if (!_urlResolved) yield return ResolveServerUrlCo();
    }

    /// <summary>로그인 시도 → 계정이 없으면(401) 가입 후 다시 로그인.</summary>
    private IEnumerator AutoLoginCo()
    {
        bool loginOk = false;
        yield return LoginCo(autoUsername, autoPassword, _ => loginOk = true, null);
        if (loginOk) yield break;

        Debug.Log("[ServerApi] 계정이 없는 것 같아 자동 가입 시도...");
        bool registerOk = false;
        yield return RegisterCo(autoUsername, autoPassword, _ => registerOk = true,
            err => Debug.LogError("[ServerApi] 자동 가입 실패: " + err));
        if (!registerOk) yield break;

        yield return LoginCo(autoUsername, autoPassword, null,
            err => Debug.LogError("[ServerApi] 가입 후 로그인 실패: " + err));
    }

    // =====================================================================
    // 창구 1: 회원가입
    // =====================================================================
    public IEnumerator RegisterCo(string username, string password,
                                  Action<long> onSuccess = null, Action<string> onError = null)
    {
        yield return EnsureUrlCo();

        string json = JsonUtility.ToJson(new AuthRequest { username = username, password = password });
        using UnityWebRequest req = MakeJsonPost("/register", json);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(ReadError(req));
            yield break;
        }

        AuthResponse res = JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
        Debug.Log($"[ServerApi] 가입 성공: {res.username} (userId {res.userId})");
        onSuccess?.Invoke(res.userId);
    }

    // =====================================================================
    // 창구 2: 로그인 (성공하면 UserId 보관 → 이후 저장/로드에 사용)
    // =====================================================================
    public IEnumerator LoginCo(string username, string password,
                               Action<long> onSuccess = null, Action<string> onError = null)
    {
        yield return EnsureUrlCo();

        string json = JsonUtility.ToJson(new AuthRequest { username = username, password = password });
        using UnityWebRequest req = MakeJsonPost("/login", json);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(ReadError(req));
            yield break;
        }

        AuthResponse res = JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
        UserId = res.userId;
        Debug.Log($"[ServerApi] 로그인 성공: {res.username} (userId {res.userId})");
        onSuccess?.Invoke(res.userId);
    }

    // =====================================================================
    // 창구 3: 저장 — SaveData를 통째로 JSON으로 만들어 서버에 전송
    // slot: 세이브 슬롯 번호(0~9). 안 넘기면 0번 슬롯(임시 테스트 기본값).
    // =====================================================================
    public IEnumerator SaveCo(SaveData data,
                              Action onSuccess = null, Action<string> onError = null, int slot = 0)
    {
        if (!IsLoggedIn)
        {
            onError?.Invoke("로그인이 안 되어 있음 (UserId 없음)");
            yield break;
        }

        yield return EnsureUrlCo();

        string json = JsonUtility.ToJson(data);   // 파일에 쓰던 그 JSON을 서버로 보낼 뿐
        using UnityWebRequest req = MakeJsonPost($"/save/{UserId}/{slot}", json);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(ReadError(req));
            yield break;
        }

        Debug.Log($"[ServerApi] 서버 저장 완료 (userId {UserId}, slot {slot})");
        onSuccess?.Invoke();
    }

    // =====================================================================
    // 창구 4: 로드 — 서버 응답(JSON)이 SaveData와 같은 모양이라 바로 복원됨
    // slot: 세이브 슬롯 번호(0~9). 안 넘기면 0번 슬롯(임시 테스트 기본값).
    // =====================================================================
    public IEnumerator LoadCo(Action<SaveData> onSuccess, Action<string> onError = null, int slot = 0)
    {
        if (!IsLoggedIn)
        {
            onError?.Invoke("로그인이 안 되어 있음 (UserId 없음)");
            yield break;
        }

        yield return EnsureUrlCo();

        using UnityWebRequest req = UnityWebRequest.Get($"{serverUrl}/load/{UserId}/{slot}");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(ReadError(req));
            yield break;
        }

        SaveData data = JsonUtility.FromJson<SaveData>(req.downloadHandler.text);
        Debug.Log($"[ServerApi] 서버 로드 완료 (userId {UserId}, slot {slot})");
        onSuccess?.Invoke(data);
    }

    // =====================================================================
    // 창구 5: 슬롯 목록 — 로드 화면용. 그 유저가 실제로 저장해둔 슬롯들의
    // 요약(슬롯번호/레벨/골드/저장시각)만 배열로 받음. 저장 안 한 슬롯은 안 옴
    // → 유니티에서 0~9 중 빠진 번호를 "빈 슬롯(새 게임)"으로 표시하면 됨.
    // =====================================================================
    public IEnumerator ListSavesCo(Action<SaveSummary[]> onSuccess, Action<string> onError = null)
    {
        if (!IsLoggedIn)
        {
            onError?.Invoke("로그인이 안 되어 있음 (UserId 없음)");
            yield break;
        }

        yield return EnsureUrlCo();

        using UnityWebRequest req = UnityWebRequest.Get($"{serverUrl}/saves/{UserId}");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(ReadError(req));
            yield break;
        }

        // JsonUtility는 최상위가 배열인 JSON을 바로 못 받아서 { "items": [...] } 형태로 감싸서 파싱
        string wrapped = "{\"items\":" + req.downloadHandler.text + "}";
        SaveSummaryList wrapper = JsonUtility.FromJson<SaveSummaryList>(wrapped);
        SaveSummary[] saves = wrapper.items ?? Array.Empty<SaveSummary>();
        Debug.Log($"[ServerApi] 세이브 목록 {saves.Length}개 수신");
        onSuccess?.Invoke(saves);
    }

    /// <summary>슬롯 삭제 (로드 화면 "삭제" 버튼용).</summary>
    public IEnumerator DeleteSaveCo(int slot, Action onSuccess = null, Action<string> onError = null)
    {
        if (!IsLoggedIn)
        {
            onError?.Invoke("로그인이 안 되어 있음 (UserId 없음)");
            yield break;
        }

        yield return EnsureUrlCo();

        using UnityWebRequest req = UnityWebRequest.Delete($"{serverUrl}/save/{UserId}/{slot}");
        req.downloadHandler = new DownloadHandlerBuffer();
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(ReadError(req));
            yield break;
        }

        Debug.Log($"[ServerApi] 슬롯 {slot} 삭제 완료");
        onSuccess?.Invoke();
    }

    // =====================================================================
    // 내부 도우미
    // =====================================================================

    /// <summary>JSON 본문을 첨부한 POST 요청 생성.</summary>
    private UnityWebRequest MakeJsonPost(string path, string json)
    {
        var req = new UnityWebRequest(serverUrl + path, "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        return req;
    }

    /// <summary>실패 응답에서 사람이 읽을 에러 문구 추출.</summary>
    private string ReadError(UnityWebRequest req)
    {
        // 서버가 {"error":"..."} 를 보냈으면 그 문구를, 아니면 통신 에러 내용을
        string body = req.downloadHandler != null ? req.downloadHandler.text : "";
        if (!string.IsNullOrEmpty(body) && body.Contains("\"error\""))
        {
            ErrorResponse e = JsonUtility.FromJson<ErrorResponse>(body);
            if (!string.IsNullOrEmpty(e.error)) return e.error;
        }
        return $"{req.error} (HTTP {req.responseCode})";
    }

    // =====================================================================
    // 서버와 주고받는 JSON 그릇들 (필드명이 서버 쪽과 일치해야 함)
    // =====================================================================
    [Serializable] private class AuthRequest  { public string username; public string password; }
    [Serializable] private class AuthResponse { public long userId;     public string username; }
    [Serializable] private class ErrorResponse { public string error; }

    /// <summary>슬롯 목록(로드 화면)용 요약 한 줄. 서버 SaveSummaryDto와 필드명 일치.</summary>
    [Serializable]
    public class SaveSummary
    {
        public int slot;
        public int level;
        public int gold;
        public string updatedAt;
    }
    [Serializable] private class SaveSummaryList { public SaveSummary[] items; }
}
