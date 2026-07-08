/*
 * [PauseMenuUI]
 * ESC 일시정지 메뉴. 전투 중 ESC를 누르면 게임을 일시정지하고 메뉴 패널을 표시.
 * 일시정지는 Time.timeScale이 아니라 GameManager.IsPaused 플래그 방식이라
 * BGM / UI 상호작용 / 연출은 그대로 살아있고 게임플레이(Player/Enemy)만 멈춘다.
 *
 * [버튼] 각 버튼 OnClick에 아래 public 메서드 연결
 * - Resume          : OnResume()          — 메뉴 닫고 게임 재개
 * - 미션 재시작      : OnRestart()         — GameManager.RestartStage()
 * - 마을로 복귀      : OnReturnToStation() — STATION 씬으로 이동
 * - 게임 종료        : OnQuit()            — 애플리케이션 종료
 * - 인벤토리         : OnInventory()       — 인벤토리 패널로 전환
 * - 옵션 / 전체지도  : OnOptions() / OnMap() — 해당 패널 표시(패널 미완성 시 훅만)
 *
 * [부착 / 연결]
 * 1. 항상 켜둘 UI 오브젝트에 부착
 * 2. panel      : 일시정지 메뉴 패널 (평소 꺼둠)
 * 3. inventory  : 씬의 InventoryPanelUI (인벤토리 버튼용, 없으면 버튼 비활성)
 * 4. optionsPanel / mapPanel : 옵션·전체지도 패널 (선택, 없으면 무시)
 */

using UnityEngine;

public class PauseMenuUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;   // 일시정지 메뉴 패널 (평소 꺼둠)

    [Header("연동 패널 (선택)")]
    [Tooltip("인벤토리 버튼용. 없으면 인벤토리 전환 불가.")]
    [SerializeField] private InventoryPanelUI inventory;
    [Tooltip("옵션 패널. 아직 없으면 비워둬도 됨.")]
    [SerializeField] private GameObject optionsPanel;
    [Tooltip("전체지도 패널. 아직 없으면 비워둬도 됨.")]
    [SerializeField] private GameObject mapPanel;
    [Tooltip("목표(퀘스트) 패널. QuestHUD가 붙은 패널 연결. 없으면 비워둬도 됨.")]
    [SerializeField] private GameObject questPanel;

    private void Start()
    {
        // 시작 시 메뉴/연동 패널 전부 꺼두기 (씬에서 켜둔 채 저장해도 안전)
        if (panel != null)        panel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (mapPanel != null)     mapPanel.SetActive(false);
        if (questPanel != null)   questPanel.SetActive(false);
    }

    private void Update()
    {
        if (InputManager.Instance != null && InputManager.Instance.pauseMenu)
        {
            // ESC = 한 단계씩 뒤로: 세팅 창 → 메뉴 → 게임
            if (IsOptionsShown()) CloseOptions();   // 세팅 창 열려있으면 세팅만 닫고 메뉴 복귀
            else if (IsShown())   CloseMenu();      // 메뉴 열려있으면 메뉴 닫고 재개
            else                  OpenMenu();       // 아무것도 없으면 메뉴 열기
        }
    }

    private bool IsShown()
    {
        return panel != null && panel.activeSelf;
    }

    // 세팅 창 표시 여부 (수동 연결 우선, 없으면 SettingMenuUI.Instance)
    private bool IsOptionsShown()
    {
        if (optionsPanel != null) return optionsPanel.activeSelf;
        if (SettingMenuUI.Instance != null) return SettingMenuUI.Instance.IsShown;
        return false;
    }

    // 메뉴 열기 — 전투 중(PLAYING)에만 열림. 일시정지 요청 후 실제로 걸렸을 때만 패널 표시.
    private void OpenMenu()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.PauseGame();
        if (!GameManager.Instance.IsPaused) return; // 전투 중이 아니면 무시(게임오버 등)

        if (panel != null) panel.SetActive(true);
    }

    // 메뉴 닫기 — 패널 숨기고 일시정지 해제.
    private void CloseMenu()
    {
        if (panel != null) panel.SetActive(false);
        if (GameManager.Instance != null) GameManager.Instance.ResumeGame();
    }

    /// <summary>Resume 버튼 — 메뉴 닫고 게임 재개.</summary>
    public void OnResume()
    {
        CloseMenu();
    }

    /// <summary>미션 재시작 버튼 — 현재 스테이지를 처음부터 (A안).</summary>
    public void OnRestart()
    {
        if (panel != null) panel.SetActive(false);
        // RestartStage()가 씬을 전환하며 일시정지 상태를 초기화하므로 별도 Resume 불필요.
        if (GameManager.Instance != null) GameManager.Instance.RestartStage();
    }

    /// <summary>마을로 복귀 버튼 — STATION 씬으로 이동.</summary>
    public void OnReturnToStation()
    {
        if (panel != null) panel.SetActive(false);
        // 씬 전환(LoadSceneRoutine)이 일시정지 상태를 초기화함.
        if (GameManager.Instance != null) GameManager.Instance.LoadScene(SCENE_TYPE.STATION);
    }

    /// <summary>게임 종료 버튼.</summary>
    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>인벤토리 버튼 — 일시정지 메뉴를 닫고 인벤토리로 전환 (인벤토리가 계속 일시정지 유지).</summary>
    public void OnInventory()
    {
        if (inventory == null)
        {
            Debug.LogWarning("[PauseMenuUI] inventory 미연결 — 인벤토리 전환 불가.");
            return;
        }
        // 메뉴의 일시정지 요청을 넘겨줌: 메뉴 닫고(해제) → 인벤토리 열기(다시 일시정지)
        if (panel != null) panel.SetActive(false);
        if (GameManager.Instance != null) GameManager.Instance.ResumeGame();
        inventory.Open();
    }

    /// <summary>옵션 버튼 — 일시정지 메뉴를 숨기고 옵션 패널 표시(일시정지는 유지).
    /// optionsPanel이 비어있으면 SettingMenuUI.Instance로 자동 연결 (씬마다 수동 연결 불필요).</summary>
    public void OnOptions()
    {
        if (panel != null) panel.SetActive(false); // 메뉴는 뒤로 (일시정지 상태는 그대로)

        if (optionsPanel != null)                     optionsPanel.SetActive(true);
        else if (SettingMenuUI.Instance != null)      SettingMenuUI.Instance.Show();
        else                                          Debug.LogWarning("[PauseMenuUI] 세팅 창 없음 — 씬에 SettingMenuUI를 배치할 것");
    }

    /// <summary>옵션 닫기 — 옵션 패널을 닫고 일시정지 메뉴로 복귀. 세팅 창의 닫기(X/Back) 버튼에 연결.</summary>
    public void CloseOptions()
    {
        if (optionsPanel != null)                     optionsPanel.SetActive(false);
        else if (SettingMenuUI.Instance != null)      SettingMenuUI.Instance.Hide();

        if (panel != null) panel.SetActive(true); // 일시정지 메뉴로 복귀
    }

    /// <summary>전체지도 버튼 — 지도 패널 표시(일시정지 유지). 패널 미연결 시 훅만.</summary>
    public void OnMap()
    {
        if (mapPanel != null) mapPanel.SetActive(true);
    }

    /// <summary>목표(퀘스트) 버튼 — 목표 패널 표시(일시정지 유지). QuestUI가 GameManager 상태를 읽어 표시.</summary>
    public void OnQuest()
    {
        if (questPanel != null) questPanel.SetActive(true);
    }
}
