/*
 * [GameOverUI]
 * 게임오버 화면 (MISSION FAILED). GameManager.onGameStateChanged 구독 —
 * GAME_STATE.GAME_OVER가 되면 패널 표시. (플레이어 사망 → GameManager.GameOver() → 이벤트 발행)
 *
 * [버튼]
 * - Restart          : 출격 직전 자동 저장을 복원해 격납고(BASE_LANDING)부터 다시 시작
 * - Return to Station: 자동 저장을 복원해 스테이션으로 복귀
 * - QuitGame         : 로비 씬으로 복귀 (복원 없음)
 *
 * [부착 / 연결]
 * 1. GameOverUI 루트(항상 켜둘 오브젝트)에 부착
 * 2. panel      : 게임오버 패널(Panel) — 평소엔 꺼두고 GAME_OVER 시 켜짐
 * 3. Restart 버튼 OnClick → OnRestart()
 * 4. Station 버튼 OnClick → OnReturnToStation()
 * 5. Quit 버튼   OnClick → OnQuitToLobby()
 * 6. lobbySceneName : 로비 씬 이름 입력
 *
 * ※ 테스트 씬에서 패널만 확인할 땐 panel을 켜두면 됨 (이벤트 없이도 버튼 동작 확인 가능)
 * ※ IsGameOver/curState 리셋은 GameManager.OnSceneLoaded가 처리 — 게임오버 씬을 벗어나는
 *    모든 경로(Restart / 로비 복귀)에서 자동으로 내려감. UI가 따로 되돌릴 필요 없음.
 */

using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panel;   // 게임오버 패널 (평소 꺼둠)

    [Header("씬 이름")]
    [SerializeField] private string lobbySceneName = "TestLobby"; // 로비 씬
    [Tooltip("체크 시 로딩 씬 경유해서 전환")]
    [SerializeField] private bool useLoading = false;

    // GameManager 참조. Start에서 1회만 잡고 OnEnable/OnDisable은 이 필드만 씀.
    private GameManager _gameManager;

    private void OnEnable()
    {
        // 캐시된 것만 씀. 최초 1회는 아직 null이라 넘어가고 바로 뒤의 Start가 구독을 마무리함.
        if (_gameManager != null)
            _gameManager.onGameStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (_gameManager != null)
            _gameManager.onGameStateChanged -= HandleStateChanged;
    }

    // 매니저 최초 취득은 Start에서만 — Awake/OnEnable에서 .Instance를 부르면 매니저 자신의 Awake보다
    // 먼저 instance를 선점해서, 매니저 Awake의 초기화가 통째로 스킵됨.
    private void Start()
    {
        _gameManager = GameManager.Instance;
        if (_gameManager != null)
            _gameManager.onGameStateChanged += HandleStateChanged;

        // 시작 시 패널 꺼두기 (테스트로 켜둔 상태였다면 유지하고 싶을 때 이 줄 주석)
        if (panel != null && _gameManager != null && !_gameManager.IsGameOver)
            panel.SetActive(false);
    }

    // 게임 상태 변화 감지 — GAME_OVER면 패널 표시
    private void HandleStateChanged(GAME_STATE state)
    {
        if (state == GAME_STATE.GAME_OVER)
            Show();
    }

    /// <summary>게임오버 패널 표시. (외부에서 직접 호출도 가능)</summary>
    public void Show()
    {
        if (panel != null) panel.SetActive(true);

        // 커서 보이게 (전투 중엔 잠겨있으므로)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>Restart 버튼 — 자동 저장을 복원해 격납고부터 다시 시작.</summary>
    public void OnRestart()
    {
        if (GameManager.Instance != null)
        {
            if (panel != null) panel.SetActive(false);
            GameManager.Instance.RestartStage();
        }
        else
        {
            // GameManager 없는 테스트 씬 폴백
            LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    /// <summary>Return to Station 버튼 — 출격 직전 자동 저장을 복원한 상태로 스테이션 복귀.</summary>
    public void OnReturnToStation()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[GameOverUI] GameManager 없음 — 스테이션 복귀 불가");
            return;
        }
        if (panel != null) panel.SetActive(false);
        GameManager.Instance.ReturnToStation();
    }

    /// <summary>QuitGame 버튼 — 로비로 복귀.</summary>
    public void OnQuitToLobby()
    {
        if (string.IsNullOrEmpty(lobbySceneName))
        {
            Debug.LogWarning("[GameOverUI] lobbySceneName이 비어있습니다.");
            return;
        }
        LoadScene(lobbySceneName);
    }

    private void LoadScene(string sceneName)
    {
        // 씬 전환은 GameManager 경유 — 전환 직전 정리(풀/사운드/이펙트 회수)가 실행되어야
        // DontDestroyOnLoad 매니저가 파괴된 유닛 참조를 들고 가는 문제가 안 생김.
        if (GameManager.Instance == null)
        {
            // GameManager 없는 테스트 씬 대비 폴백(직접 로드)
            SceneManager.LoadScene(sceneName);
            return;
        }

        if (useLoading)
        {
            GameManager.Instance.LoadSceneWithLoading(sceneName);
        }
        else
        {
            GameManager.Instance.LoadScene(sceneName);
        }
    }
}
