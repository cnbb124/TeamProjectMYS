/*
 * [GameOverUI]
 * 게임오버 화면 (MISSION FAILED). GameManager.onGameStateChanged 구독 —
 * GAME_STATE.GAME_OVER가 되면 패널 표시. (플레이어 사망 → GameManager.GameOver() → 이벤트 발행)
 *
 * [버튼]
 * - Restart  : 현재 전투 씬을 다시 로드 (재시작)
 * - QuitGame : 로비 씬으로 복귀
 *
 * [부착 / 연결]
 * 1. GameOverUI 루트(항상 켜둘 오브젝트)에 부착
 * 2. panel      : 게임오버 패널(Panel) — 평소엔 꺼두고 GAME_OVER 시 켜짐
 * 3. Restart 버튼 OnClick → OnRestart()
 * 4. Quit 버튼   OnClick → OnQuitToLobby()
 * 5. lobbySceneName : 로비 씬 이름 입력
 *
 * ※ 테스트 씬에서 패널만 확인할 땐 panel을 켜두면 됨 (이벤트 없이도 버튼 동작 확인 가능)
 * ※ 주의: GameManager.IsGameOver/curState 리셋은 씬 전환 시 GameManager 쪽 처리에 따름 —
 *   Restart 후에도 GAME_OVER 상태가 남아있으면 팀장과 리셋 지점 협의 필요.
 */

using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panel;   // 게임오버 패널 (평소 꺼둠)

    [Header("씬 이름")]
    [SerializeField] private string lobbySceneName = "TestLobby"; // 로비 씬
    [Tooltip("체크 시 로딩 씬(LoadingManager) 경유해서 전환")]
    [SerializeField] private bool useLoading = false;
    [SerializeField] private string loadingSceneName = "LoadingScene";

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStateChanged -= HandleStateChanged;
    }

    private void Start()
    {
        // 시작 시 패널 꺼두기 (테스트로 켜둔 상태였다면 유지하고 싶을 때 이 줄 주석)
        if (panel != null && GameManager.Instance != null && !GameManager.Instance.IsGameOver)
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

    /// <summary>Restart 버튼 — 현재 씬 재시작.</summary>
    public void OnRestart()
    {
        LoadScene(SceneManager.GetActiveScene().name);
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
        if (useLoading)
        {
            LoadingManager.NextScene = sceneName;
            SceneManager.LoadScene(loadingSceneName);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
