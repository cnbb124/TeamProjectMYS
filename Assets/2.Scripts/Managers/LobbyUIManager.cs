using UnityEngine;

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance { get; private set; }

    // 멀티 대기실 테스트용 씬 (WaitingRoomUI가 배치된 씬)
    private const string WaitingRoomSceneName = "TestMultiplayerScene_Wooseok";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        SoundManager.Instance.PlayBGM(SOUND_TYPE.BGM_LOBBY);
    }

    // ── 로비 버튼 (스샷 기준: NEW GAME / LOAD GAME / SETTINGS / QUIT GAME) ──

    public void OnClickNewGame()
    {
        HangarExitButton.launchSceneName = "MAP_SELECT"; // 격납고쪽에 다음 갈곳 저장
        LoadingManager.NextScene = "BASE_LANDING"; // 격납고 이동
        GameManager.Instance.LoadScene(SCENE_TYPE.LOADING_SEQUENCE);
    }

    public void OnClickLoadGame()
    {
        // TODO: 세이브 슬롯 목록 UI 완성되면 연결 (서버 /saves 연동 예정)
        Debug.Log("[LobbyUIManager] LOAD GAME — 세이브 슬롯 UI 미구현");
    }

    public void OnClickSettings()
    {
        GameManager.Instance.LoadScene("SettingsScene");
    }

    public void OnClickQuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── MULTI (테스트용 버튼) → 멀티 대기실(WaitingRoomUI) 씬으로 ──
    // MULTI 버튼 OnClick에 이 메서드 연결.
    public void OnClickMulti()
    {
        // 기존(서버 리스트 경유) — 나중에 매치메이킹 붙으면 복구
        // GameManager.Instance.LoadScene("ServerListUI");

        HangarExitButton.launchSceneName = "MULTIPLAYER"; // 격납고 나가면 갈 곳
        LoadingManager.NextScene = "BASE_LANDING";
        GameManager.Instance.LoadScene(SCENE_TYPE.LOADING_SEQUENCE);
    }

    // ── 아래는 안 쓰는 구버전 핸들러 — 참고용 주석 처리 ──
    // public void OnClickPlay()       { GameManager.Instance.LoadScene("ServerListUI"); }
    // public void OnClickCharacter()  { GameManager.Instance.LoadScene("CharacterScene"); }
    // public void OnClickShop()       { GameManager.Instance.LoadScene("ShopScene"); }
    // public void OnClickQuickMatch() { GameManager.Instance.LoadScene("ServerListUI"); }
}
