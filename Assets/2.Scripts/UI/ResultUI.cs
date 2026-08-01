/*
 * [ResultUI]
 * 결과 화면(RESULT 씬). 게임오버와 스테이지 클리어가 같은 씬을 쓰고, 상태에 따라 패널만 갈린다.
 *
 * [버튼] 두 결과 모두 동작이 같아 핸들러를 공유함
 * - Restart          : 격납고(BASE_LANDING)부터 다시 시작
 * - Return to Station: 스테이션으로 복귀
 * - QuitGame         : 로비 씬으로 복귀
 *
 * [부착 / 연결]
 * 1. 결과 UI 루트(항상 켜둘 오브젝트)에 부착
 * 2. gameOverPanel / clearPanel : 각 패널
 * 3. Restart 버튼 OnClick → OnRestart()
 * 4. Station 버튼 OnClick → OnReturnToStation()
 * 5. Quit 버튼   OnClick → OnQuitToLobby()
 * 6. lobbySceneName : 로비 씬 이름 입력
 *
 * ※ Restart/Station이 복원하는 자동저장 슬롯에는 출격 직전(격납고 Exit) 또는
 *   클리어 직후 상태가 기록돼 있음.
 */

using UnityEngine;
using UnityEngine.SceneManagement;

public class ResultUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("사망 시 표시할 패널")]
    [SerializeField] private GameObject gameOverPanel;
    [Tooltip("스테이지 클리어 시 표시할 패널")]
    [SerializeField] private GameObject clearPanel;

    [Header("씬 이름")]
    [SerializeField] private string lobbySceneName = SCENE_TYPE.MAIN.ToString();
    [Tooltip("체크 시 로딩 씬 경유해서 전환")]
    [SerializeField] private bool useLoading = false;

    // GameManager 참조. Start에서 1회만 잡고 OnEnable/OnDisable은 이 필드만 씀.
    private GameManager _gameManager;

    private void OnEnable()
    {
        if (_gameManager != null)
        {
            _gameManager.onGameStateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        if (_gameManager != null)
        {
            _gameManager.onGameStateChanged -= HandleStateChanged;
        }
    }

    // 매니저 최초 취득은 Start에서 — Awake/OnEnable 시점엔 아직 null일 수 있음.
    private void Start()
    {
        _gameManager = GameManager.Instance;
        if (_gameManager != null)
        {
            _gameManager.onGameStateChanged += HandleStateChanged;
        }

        // RESULT 씬은 GameManager.OnSceneLoaded의 상태 초기화에서 제외돼 있어
        // 도착 시점에도 GAME_OVER / STAGE_CLEAR 값이 살아 있음.
        ShowFor(_gameManager != null ? _gameManager.curState : GAME_STATE.NONE);
    }

    private void HandleStateChanged(GAME_STATE state)
    {
        ShowFor(state);
    }

    /// <summary>상태에 맞는 패널만 켬. 결과 상태가 아니면 둘 다 끔.</summary>
    public void ShowFor(GAME_STATE state)
    {
        bool isGameOver = state == GAME_STATE.GAME_OVER;
        bool isClear = state == GAME_STATE.STAGE_CLEAR;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(isGameOver);
        }
        if (clearPanel != null)
        {
            clearPanel.SetActive(isClear);
        }

        if (isGameOver || isClear)
        {
            // 전투 중엔 커서가 잠겨 있으므로 풀어줌
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>Restart 버튼 — 격납고부터 다시 시작.</summary>
    public void OnRestart()
    {
        if (_gameManager == null)
        {
            Debug.LogWarning("[ResultUI] GameManager 없음 — 재시작 불가");
            return;
        }
        HideAll();
        _gameManager.RestartStage();
    }

    /// <summary>Return to Station 버튼 — 스테이션으로 복귀.</summary>
    public void OnReturnToStation()
    {
        if (_gameManager == null)
        {
            Debug.LogWarning("[ResultUI] GameManager 없음 — 스테이션 복귀 불가");
            return;
        }
        HideAll();
        _gameManager.ReturnToStation();
    }

    /// <summary>Return to lobby 버튼 —  타이틀(로비)로 복귀.</summary>
    public void OnReturnToLobby()
    {
        if (string.IsNullOrEmpty(lobbySceneName))
        {
            Debug.LogWarning("[ResultUI] lobbySceneName이 비어있습니다.");
            return;
        }
        HideAll();
        LoadScene(lobbySceneName);
    }

    private void HideAll()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
        if (clearPanel != null)
        {
            clearPanel.SetActive(false);
        }
    }

    // 씬 전환은 GameManager 경유 — 전환 직전 정리(풀/사운드/이펙트 회수)가 실행되어야
    // DontDestroyOnLoad 매니저가 파괴된 참조를 들고 가는 문제가 안 생김.
    private void LoadScene(string sceneName)
    {
        if (_gameManager == null)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        if (useLoading)
        {
            _gameManager.LoadSceneWithLoading(sceneName);
        }
        else
        {
            _gameManager.LoadScene(sceneName);
        }
    }
}
