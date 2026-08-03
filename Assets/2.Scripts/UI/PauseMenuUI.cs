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
using UnityEngine.UI;

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
    [Tooltip("세이브 슬롯 패널 (LoadGameUI, Mode=Save). 없으면 비워둬도 됨.")]
    [SerializeField] private GameObject savePanel;
    [Tooltip("로드 슬롯 패널 (LoadGameUI, Mode=Load). 없으면 비워둬도 됨.")]
    [SerializeField] private GameObject loadPanel;

    [Tooltip("세이브 버튼. 연결하면 정거장(CanSave)에서만 활성, 그 외엔 비활성(회색). 로드 버튼은 항상 활성이라 연결 불필요.")]
    [SerializeField] private Button saveButton;

    // 입력 잠금/커서 해제 판정용(InputManager.IsGameplayInputLocked). 메뉴 계열 패널(메뉴/옵션/지도/퀘스트) 중
    // 하나라도 떠 있으면 커서를 풀고 게임 입력을 막음 — 멀티에선 IsPaused가 false라 '열림 상태' 자체가 유일한 잠금 근거임.
    // ※ 메뉴 panel만 보면 안 됨: OnOptions()가 panel을 끄고 옵션 패널을 켜므로 그 순간 잠금 근거가 사라져,
    //    (싱글은 IsPaused가 가려주지만) 멀티에선 옵션창을 띄운 채 플레이어가 조종되는 문제가 생김.
    private static PauseMenuUI _instance;
    public static bool IsOpen => _instance != null && _instance.IsAnyPanelShown();

    // 메뉴 계열 패널(메뉴/옵션/지도/퀘스트/세이브/로드) 중 하나라도 표시 중인지
    // 세이브·로드도 반드시 포함해야 함 — OnSave/OnLoadGame이 메뉴 패널을 끄고 자기 패널만 켜므로,
    // 여기서 빠지면 그 창을 띄운 동안 '아무 UI도 없음'으로 판정돼 멀티에서 함선이 조종되고 커서가 잠김.
    private bool IsAnyPanelShown()
    {
        if (IsShown() || IsOptionsShown())
        {
            return true;
        }
        if (mapPanel != null && mapPanel.activeSelf)
        {
            return true;
        }
        if (IsSaveShown() || IsLoadShown())
        {
            return true;
        }
        return questPanel != null && questPanel.activeSelf;
    }

    private bool IsSaveShown()
    {
        return savePanel != null && savePanel.activeSelf;
    }

    private bool IsLoadShown()
    {
        return loadPanel != null && loadPanel.activeSelf;
    }

    private void Start()
    {
        _instance = this;
        // 시작 시 메뉴/연동 패널 전부 꺼두기 (씬에서 켜둔 채 저장해도 안전)
        if (panel != null)        panel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (mapPanel != null)     mapPanel.SetActive(false);
        if (questPanel != null)   questPanel.SetActive(false);
        if (savePanel != null)    savePanel.SetActive(false);
        if (loadPanel != null)    loadPanel.SetActive(false);
    }

    private void Update()
    {
        if (InputManager.Instance != null && InputManager.Instance.pauseMenu)
        {
            // ESC = 한 단계씩 뒤로: 세팅/세이브/로드 창 → 메뉴 → 게임
            if (IsOptionsShown()) CloseOptions();   // 세팅 창 열려있으면 세팅만 닫고 메뉴 복귀
            else if (IsSaveShown()) CloseSave();    // 세이브 창 열려있으면 그것만 닫고 메뉴 복귀
            else if (IsLoadShown()) CloseLoad();    // 로드 창도 동일
            else if (IsShown())   CloseMenu();      // 메뉴 열려있으면 메뉴 닫고 재개
            else                  OpenMenu();       // 아무것도 없으면 메뉴 열기
        }
    }

    /// <summary>
    /// XR 왼손 Menu 버튼이 기존 ESC와 동일한 메뉴 흐름을 호출하는 진입점.
    /// </summary>
    public static void ToggleFromExternalInput()
    {
        if (_instance == null)
        {
            return;
        }

        if (_instance.IsOptionsShown())  _instance.CloseOptions();
        else if (_instance.IsSaveShown()) _instance.CloseSave();
        else if (_instance.IsLoadShown()) _instance.CloseLoad();
        else if (_instance.IsShown())     _instance.CloseMenu();
        else                              _instance.OpenMenu();
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
        // PauseGame() 반환값으로 판단 — 멀티에선 실제 프리즈(IsPaused)는 안 되지만 메뉴는 떠야 하므로
        // IsPaused가 아니라 "메뉴 허용" 반환값을 본다. false면 게임오버/클리어라 메뉴 안 띄움.
        if (!GameManager.Instance.PauseGame()) return;

        if (panel != null) panel.SetActive(true);

        // 세이브는 정거장(CanSave)에서만 — 그 외 씬에선 버튼 비활성(회색). 로드는 어디서든 가능이라 안 건드림.
        if (saveButton != null)
            saveButton.interactable = GameManager.Instance.CanSave;
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

    /// <summary>마을로 복귀 버튼 — 출격 직전 자동 저장을 복원한 상태로 스테이션 이동.</summary>
    public void OnReturnToStation()
    {
        if (panel != null) panel.SetActive(false);
        // 씬 전환(LoadSceneRoutine)이 일시정지 상태를 초기화함.
        if (GameManager.Instance != null) GameManager.Instance.ReturnToStation();
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

    /// <summary>세이브 버튼 — 저장 슬롯 패널 표시(일시정지 유지). 실제 저장 가능 여부는 패널(LoadGameUI)이 CanSave로 판단.</summary>
    public void OnSave()
    {
        if (savePanel == null)
        {
            Debug.LogWarning("[PauseMenuUI] savePanel 미연결 — 저장 패널 표시 불가.");
            return;
        }
        if (panel != null) panel.SetActive(false); // 메뉴 숨김 (일시정지 유지)
        savePanel.SetActive(true);
    }

    /// <summary>세이브 패널 닫기 — 저장 패널을 닫고 일시정지 메뉴로 복귀. 세이브 패널의 닫기 버튼에 연결.</summary>
    public void CloseSave()
    {
        if (savePanel != null) savePanel.SetActive(false);
        if (panel != null) panel.SetActive(true);
    }

    /// <summary>로드 버튼 — 로드 슬롯 패널 표시(일시정지 유지).</summary>
    public void OnLoadGame()
    {
        if (loadPanel == null)
        {
            Debug.LogWarning("[PauseMenuUI] loadPanel 미연결 — 로드 패널 표시 불가.");
            return;
        }
        if (panel != null) panel.SetActive(false);
        loadPanel.SetActive(true);
    }

    /// <summary>로드 패널 닫기 — 로드 패널을 닫고 일시정지 메뉴로 복귀. 로드 패널의 닫기 버튼에 연결.</summary>
    public void CloseLoad()
    {
        if (loadPanel != null) loadPanel.SetActive(false);
        if (panel != null) panel.SetActive(true);
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
