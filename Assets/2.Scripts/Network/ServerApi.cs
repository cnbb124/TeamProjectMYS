using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// ▶ 서버 저장/로드 (GameManager가 사용)
//   Instance.SaveCo(saveData, onSuccess, onError) : 서버에 저장
//   Instance.LoadCo(onSuccess, onError)           : 서버에서 로드
//   IsLoggedIn / UserId                           : 로그인 여부 / 내 고유번호
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
//   창구 4개: /register(가입) /login(로그인) /save/{id}(저장) /load/{id}(로드)
//
// 데이터 흐름:
//   [유니티] --JSON--> [API 서버] --SQL--> [MySQL DB]
//   SaveData를 JsonUtility.ToJson으로 쪽지화해 보내면 서버가 DB에 나눠 담고,
//   로드 시 서버가 SaveData와 똑같은 모양의 JSON으로 돌려줌 → FromJson으로 복원.
//
// 사용법:
//   1. 씬에 빈 오브젝트 만들고 이 스크립트 부착 (GameManager 옆 권장)
//   2. serverUrl 확인 (내 PC 테스트면 localhost, 미니PC면 그 IP)
//   3. 로그인 UI가 아직 없으므로 autoLoginOnStart 체크하면
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
    [Header("━━━━━━ 서버 주소 ━━━━━━")]
    [Tooltip("API 서버 주소. 내 PC 테스트 = localhost, 미니PC 배포 후 = 그 PC의 IP (예: http://192.168.0.50:5080)")]
    [SerializeField] private string serverUrl = "http://192.168.0.8:5080";

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
        if (autoLoginOnStart)
        {
            StartCoroutine(AutoLoginCo());
        }
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
    // =====================================================================
    public IEnumerator SaveCo(SaveData data,
                              Action onSuccess = null, Action<string> onError = null)
    {
        if (!IsLoggedIn)
        {
            onError?.Invoke("로그인이 안 되어 있음 (UserId 없음)");
            yield break;
        }

        string json = JsonUtility.ToJson(data);   // 파일에 쓰던 그 JSON을 서버로 보낼 뿐
        using UnityWebRequest req = MakeJsonPost($"/save/{UserId}", json);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(ReadError(req));
            yield break;
        }

        Debug.Log($"[ServerApi] 서버 저장 완료 (userId {UserId})");
        onSuccess?.Invoke();
    }

    // =====================================================================
    // 창구 4: 로드 — 서버 응답(JSON)이 SaveData와 같은 모양이라 바로 복원됨
    // =====================================================================
    public IEnumerator LoadCo(Action<SaveData> onSuccess, Action<string> onError = null)
    {
        if (!IsLoggedIn)
        {
            onError?.Invoke("로그인이 안 되어 있음 (UserId 없음)");
            yield break;
        }

        using UnityWebRequest req = UnityWebRequest.Get($"{serverUrl}/load/{UserId}");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(ReadError(req));
            yield break;
        }

        SaveData data = JsonUtility.FromJson<SaveData>(req.downloadHandler.text);
        Debug.Log($"[ServerApi] 서버 로드 완료 (userId {UserId})");
        onSuccess?.Invoke(data);
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
}
