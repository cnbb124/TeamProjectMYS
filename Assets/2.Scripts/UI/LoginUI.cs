/*
 * [LoginUI]
 * 로그인 씬(TestLoginScene_Wooseok)의 UI를 ServerApi(가입/로그인)에 연결.
 *
 * [사용법]
 * 1. 로그인 씬의 아무 오브젝트(예: LoginUI)에 이 스크립트 부착
 * 2. 인스펙터에서 ID_Field / PW_Field / 로그인 Button 연결
 *    (안 연결해도 됨 — 씬에서 같은 이름(ID_Field, PW_Field, Button)을 자동으로 찾음)
 * 3. 씬에 ServerApi 프리팹(Assets/3.Prefabs/Network/ServerApi.prefab) 배치
 *    (또는 메뉴 Tools > MYS > 로그인 연결 셋업 한 번 클릭이면 2~3 자동)
 *
 * [동작]
 * - 버튼 클릭 or PW칸에서 Enter → 로그인 시도
 * - 계정이 없으면(로그인 실패) 자동으로 가입 후 다시 로그인 (autoRegisterIfNoAccount)
 *   · 가입까지 실패(아이디 중복)하면 = 아이디는 있는데 비번이 틀린 것 → 에러 표시
 * - 성공 시: statusText 표시 → nextSceneName 있으면 그 씬으로 이동, onLoginSuccess 이벤트 발동
 */

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using TMPro;

public class LoginUI : MonoBehaviour
{
    [Header("━━━━━━ UI 참조 (비우면 이름으로 자동 탐색) ━━━━━━")]
    [SerializeField] private TMP_InputField idField;      // ID_Field
    [SerializeField] private TMP_InputField pwField;      // PW_Field
    [SerializeField] private Button         loginButton;  // Button
    [SerializeField] private TMP_Text       statusText;   // 상태/에러 표시 (선택)

    [Header("━━━━━━ 동작 설정 ━━━━━━")]
    [Tooltip("로그인 실패(계정 없음) 시 자동으로 가입 후 다시 로그인")]
    [SerializeField] private bool autoRegisterIfNoAccount = true;

    [Tooltip("로그인 성공 시 이동할 씬 이름. 비우면 이동 안 함")]
    [SerializeField] private string nextSceneName = "";

    [Header("━━━━━━ 이벤트 ━━━━━━")]
    [Tooltip("로그인 성공 시 추가로 실행할 것 (씬 전환 외 연출 등)")]
    public UnityEvent onLoginSuccess;

    private bool _busy;   // 요청 중 중복 클릭 방지

    // =====================================================================
    // 초기화 — 참조 자동 탐색 + 버튼/Enter 연결
    // =====================================================================
    private void Start()
    {
        // 인스펙터에서 안 물려줬으면 씬에서 이름으로 찾음 (우석 씬 구조 기준)
        if (idField == null)     idField     = FindByName<TMP_InputField>("ID_Field");
        if (pwField == null)     pwField     = FindByName<TMP_InputField>("PW_Field");
        if (loginButton == null) loginButton = FindByName<Button>("Button");
        if (statusText == null)  statusText  = FindByName<TMP_Text>("StatusText");

        if (loginButton != null) loginButton.onClick.AddListener(OnClickLogin);
        if (pwField != null)     pwField.onSubmit.AddListener(_ => OnClickLogin()); // PW칸 Enter = 로그인

        if (idField == null || pwField == null || loginButton == null)
            Debug.LogWarning("[LoginUI] ID_Field/PW_Field/Button 중 못 찾은 게 있음 — 인스펙터에서 직접 연결 필요");
    }

    // =====================================================================
    // 로그인 버튼
    // =====================================================================
    public void OnClickLogin()
    {
        if (_busy) return;

        if (ServerApi.Instance == null)
        {
            ShowStatus("서버 연결 준비 안 됨 — 씬에 ServerApi 프리팹을 놓아주세요", true);
            return;
        }

        string id = idField != null ? idField.text.Trim() : "";
        string pw = pwField != null ? pwField.text : "";

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
        {
            ShowStatus("아이디/비밀번호를 입력하세요", true);
            return;
        }

        StartCoroutine(LoginFlowCo(id, pw));
    }

    // =====================================================================
    // 로그인 흐름: 로그인 → (실패 시) 가입 → 다시 로그인
    // =====================================================================
    private IEnumerator LoginFlowCo(string id, string pw)
    {
        _busy = true;
        if (loginButton != null) loginButton.interactable = false;
        ShowStatus("로그인 중...");

        // 1차: 로그인 시도
        bool ok = false;
        yield return ServerApi.Instance.LoginCo(id, pw, _ => ok = true, null);

        // 실패 → 계정이 없어서일 수 있음 → 가입 시도
        if (!ok && autoRegisterIfNoAccount)
        {
            ShowStatus("계정 확인 중...");
            bool registered = false;
            string registerError = null;
            yield return ServerApi.Instance.RegisterCo(id, pw,
                _ => registered = true, err => registerError = err);

            if (registered)
            {
                // 새 계정 생성됨 → 그 계정으로 로그인
                yield return ServerApi.Instance.LoginCo(id, pw, _ => ok = true, null);
            }
            else
            {
                // 가입도 실패 = 아이디가 이미 있음 → 비밀번호가 틀렸던 것
                ShowStatus(registerError != null && registerError.Contains("이미 사용")
                    ? "비밀번호가 틀렸습니다"
                    : "로그인 실패: " + registerError, true);
            }
        }
        else if (!ok)
        {
            ShowStatus("아이디 또는 비밀번호가 틀렸습니다", true);
        }

        if (ok)
        {
            ShowStatus($"로그인 성공! (userId {ServerApi.Instance.UserId})");
            onLoginSuccess?.Invoke();
            if (!string.IsNullOrEmpty(nextSceneName))
                SceneManager.LoadScene(nextSceneName);
        }

        if (loginButton != null) loginButton.interactable = true;
        _busy = false;
    }

    // =====================================================================
    // 도우미
    // =====================================================================
    private void ShowStatus(string msg, bool isError = false)
    {
        if (statusText != null)
        {
            statusText.text  = msg;
            statusText.color = isError ? new Color(1f, 0.4f, 0.4f) : Color.white;
        }
        if (isError) Debug.LogWarning("[LoginUI] " + msg);
        else         Debug.Log("[LoginUI] " + msg);
    }

    /// <summary>씬 전체에서 이름으로 컴포넌트 찾기 (비활성 오브젝트 포함).</summary>
    private static T FindByName<T>(string name) where T : Component
    {
        foreach (T c in FindObjectsOfType<T>(true))
            if (c.gameObject.name == name) return c;
        return null;
    }
}
