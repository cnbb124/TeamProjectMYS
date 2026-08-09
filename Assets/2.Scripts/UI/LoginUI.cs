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

    [Header("━━━━━━ 오프라인 모드 (테스트용) ━━━━━━")]
    [Tooltip("켜면 서버가 꺼진 것으로 치고 로컬 저장만 쓴다. 로그인 화면 체크박스와 연동됨.")]
    [SerializeField] private bool offlineMode = false;

    [Tooltip("직접 만든 체크박스를 쓰려면 여기에 연결. 비우면 로그인 버튼 옆에 자동 생성.")]
    [SerializeField] private Toggle offlineToggle;

    [Tooltip("체크박스를 자동으로 만들지 여부. 정식 UI가 생기면 끄면 된다.")]
    [SerializeField] private bool createOfflineToggle = true;

    [Tooltip("로그인 버튼 기준 위치. 상태 문구와 겹치면 Y를 더 내린다.")]
    [SerializeField] private Vector2 offlineTogglePadding = new Vector2(0f, -70f);

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

        SetupOfflineToggle();
    }

    // =====================================================================
    // [2026-08-04 추가] 오프라인 모드 체크박스
    //
    // 서버가 살아 있는지 자동으로 판정하는 대신, 사람이 직접 켜는 방식이다.
    // 테스트할 때 서버를 실제로 끄지 않아도 되고, 판정 로직이 없어 오작동도 없다.
    //
    // 체크박스는 '로그인 버튼과 같은 부모' 아래에 만든다.
    // LoginUI 자신에게 붙이면 VR에서 RenderTexture 캔버스 밖에 생겨 안 보인다.
    // 로그인 버튼 옆이면 같은 캔버스 안이라 헤드셋에서도 그대로 보인다.
    // =====================================================================
    private void SetupOfflineToggle()
    {
        if (offlineToggle == null && createOfflineToggle && loginButton != null)
        {
            offlineToggle = CreateOfflineToggle(loginButton.transform.parent);
        }

        if (offlineToggle == null)
        {
            return;
        }

        offlineToggle.isOn = offlineMode;
        offlineToggle.onValueChanged.AddListener(value =>
        {
            offlineMode = value;
            ShowStatus(value
                ? "오프라인 모드 — 로컬 저장만 사용합니다"
                : "");
        });
    }

    private Toggle CreateOfflineToggle(Transform parent)
    {
        if (parent == null)
        {
            return null;
        }

        GameObject root = new GameObject("OfflineModeToggle", typeof(RectTransform));
        root.transform.SetParent(parent, false);

        // Toggle이 붙은 오브젝트에 그래픽이 없으면 레이캐스트에 안 잡힌다.
        // 그러면 작은 네모(28px)만 눌리고 글자를 눌러도 반응이 없다.
        // 투명 이미지를 깔아 줄 전체를 클릭 범위로 만든다.
        Image rootHit = root.AddComponent<Image>();
        rootHit.color = new Color(1f, 1f, 1f, 0f);
        rootHit.raycastTarget = true;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        RectTransform buttonRect = loginButton.GetComponent<RectTransform>();
        rootRect.anchorMin = buttonRect.anchorMin;
        rootRect.anchorMax = buttonRect.anchorMax;
        rootRect.pivot = buttonRect.pivot;
        // 기존 안내 문구가 줄바꿈되지 않도록 충분한 폭을 확보한다.
        rootRect.sizeDelta = new Vector2(
            Mathf.Max(360f, buttonRect.sizeDelta.x), 36f);
        // 상태 문구가 버튼 바로 아래에 뜨므로 그보다 더 내려놓는다.
        rootRect.anchoredPosition =
            buttonRect.anchoredPosition + offlineTogglePadding;

        // 표시 순서: 로그인 버튼 -> 오프라인 토글 -> 선택 상태 문구.
        // 기존 StatusText 위치가 토글과 겹치므로 토글 바로 아래로 내린다.
        if (statusText != null &&
            statusText.transform.parent == root.transform.parent &&
            statusText.transform is RectTransform statusRect)
        {
            const float statusGap = 10f;
            float statusY = rootRect.anchoredPosition.y
                - rootRect.sizeDelta.y * 0.5f
                - statusRect.sizeDelta.y * 0.5f
                - statusGap;
            statusRect.anchoredPosition = new Vector2(
                statusRect.anchoredPosition.x,
                statusY);
        }

        GameObject box = new GameObject("Box", typeof(RectTransform));
        box.transform.SetParent(root.transform, false);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0f, 0.5f);
        boxRect.anchorMax = new Vector2(0f, 0.5f);
        boxRect.pivot = new Vector2(0f, 0.5f);
        boxRect.sizeDelta = new Vector2(28f, 28f);
        boxRect.anchoredPosition = new Vector2(6f, 0f);
        Image boxImage = box.AddComponent<Image>();
        boxImage.color = new Color(0.10f, 0.13f, 0.18f, 0.95f);

        GameObject check = new GameObject("Check", typeof(RectTransform));
        check.transform.SetParent(box.transform, false);
        RectTransform checkRect = check.GetComponent<RectTransform>();
        checkRect.anchorMin = Vector2.zero;
        checkRect.anchorMax = Vector2.one;
        checkRect.offsetMin = new Vector2(5f, 5f);
        checkRect.offsetMax = new Vector2(-5f, -5f);
        Image checkImage = check.AddComponent<Image>();
        checkImage.color = new Color(0.20f, 0.75f, 0.95f, 1f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(root.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(1f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.offsetMin = new Vector2(38f, -16f);
        labelRect.offsetMax = new Vector2(-4f, 16f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "오프라인 모드 (서버 없이 진행)";
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Left;
        label.color = Color.white;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        if (statusText != null)
        {
            label.font = statusText.font;
        }

        Toggle toggle = root.AddComponent<Toggle>();
        toggle.targetGraphic = boxImage;
        toggle.graphic = checkImage;
        toggle.isOn = offlineMode;
        return toggle;
    }

    // =====================================================================
    // 로그인 버튼
    // =====================================================================
    public void OnClickLogin()
    {
        if (_busy) return;

        // [2026-08-04] 오프라인 모드면 서버를 아예 거치지 않는다.
        // 아이디/비밀번호도 확인하지 않는다 — 확인해 줄 서버가 없기 때문이다.
        if (offlineMode)
        {
            StartOffline();
            return;
        }

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

    /// <summary>
    /// [2026-08-04 추가] 서버 없이 다음 씬으로 들어간다.
    /// 로그인 성공과 같은 경로를 타므로 씬 이동·이벤트는 그대로 동작한다.
    /// 저장/불러오기는 OfflineSession.IsActive를 보고 로컬만 쓴다.
    /// </summary>
    private void StartOffline()
    {
        OfflineSession.Enter();
        ShowStatus("오프라인 모드로 시작합니다 (로컬 저장)");

        onLoginSuccess?.Invoke();
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
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
