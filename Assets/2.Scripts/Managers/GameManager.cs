using System.Collections;
using System.Collections.Generic;
using System.IO;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// ▶ 전체 팀 공통 참조
//   IsPaused   : 일시정지 여부. Update/FixedUpdate 첫 줄에서 ShouldPause로 체크.
//   IsGameOver : 게임오버 여부
//   curState   : 현재 게임 상태 (GAME_STATE enum)
//   playerRef  : Player 레퍼런스. 씬 로드 후 자동 갱신.
//
// ▶ 적 / 스폰 참조용
//   OnEnemyKilled(killer, exp, gold) : 적 사망 시 Enemy.Die()에서 호출 (킬카운트 + 킬러에게 보상 지급)
//   OnBossKilled()      : 보스 사망 시 보스 오브젝트에서 호출
//   (파괴 목표는 오브젝트에 BossSpawnTarget 컴포넌트를 붙이면 자동 등록/파괴통지됨 — 킬 AND 목표파괴 시 보스 스폰)
//   onBossSpawn         : 보스 스폰 조건 달성 시 발행 이벤트
//   onBossKilled        : 보스 처치 시 발행 이벤트
//   onObjectiveChanged  : 목표 진행도 변경 시 발행 (Quest UI 구독용)
//   예시) GameManager.Instance.onBossSpawn += 내스폰함수;
//
// ▶ UI 참조용
//   PauseGame() / ResumeGame()  : 일시정지 / 해제
//   onGameStateChanged          : 상태 변화 이벤트. 패널 전환 등에 구독.
//   예시) GameManager.Instance.onGameStateChanged += OnStateChange;
//
// ▶ 씬 전환 참조용
//   LoadScene(SCENE_TYPE)       : enum으로 씬 전환 (권장)
//   LoadScene(string sceneName) : 씬 이름으로 전환
//   SaveGame(int) / LoadGame(int) : 저장/로드 (STATION 씬에서만 저장 가능)
// ================================================================

// =====================================================================
// GameManager
//
// 역할:
//   1. 게임 상태(FSM) 관리
//   2. 씬 전환 (정리 후 로드)
//   3. 킬카운트 / 보스 스폰 조건 관리 (골드는 InventoryManager)
//   4. 저장 / 불러오기 (STATION 씬에서만 저장 가능)
//   5. Player 레퍼런스 캐싱 (씬 로드 후 자동 탐색)
//
// 씬 흐름:
//   STATION -> LOADING_SEQUENCE -> MAP_SELECT -> STAGE1
//   전투 종료 -> STATION 복귀
//
// 저장 데이터: SaveData.cs 참고
// =====================================================================
public delegate void GameStateHandler(GAME_STATE state);
public delegate void BossSpawnHandler();

public class GameManager : MonoBehaviour
{
    // =====================================================================
    // 싱글톤
    // =====================================================================
    private static GameManager instance = null;
    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GameManager>();
                if (instance == null)
                {
                    Debug.LogError("[GameManager] 씬에 GameManager 없음! 하이어라키에 추가 필요");
                }
                else
                {
                    DontDestroyOnLoad(instance.gameObject);
                }
            }
            return instance;
        }
    }

	// =====================================================================
	// 씬-BGM 매핑 
	//  ※ SCENE_TYPE 이름 = 실제 씬 파일 이름이어야 조회됨(scene.name 기준)
	// =====================================================================
	[System.Serializable]
	public struct SceneBGM
	{
		public SCENE_TYPE scene;
		public SOUND_TYPE bgm;
	}

	[Header("<size=22>━━━━━━ 씬-BGM 매핑 ━━━━━━</size>")]
	[Header("씬 추가 시 여기서 SCENE_TYPE + BGM 드롭다운으로 추가\n" +
		"SCENE_TYPE 이름 = 실제 씬 파일 이름이어야 조회됨(scene.name 기준)")]
	[SerializeField] private List<SceneBGM> _sceneBGMList = new List<SceneBGM>();

	// 런타임 조회용. Awake에서 _sceneBGMList로 구성 (key = scene.ToString())
	private Dictionary<string, SOUND_TYPE> _sceneBGMMap = new Dictionary<string, SOUND_TYPE>();

	[Header("보스 BGM 전환")]
	[Tooltip("보스 등장 시 전환할 BGM. 보스 처치 시 현재 씬 BGM으로 복귀.")]
	[SerializeField] private SOUND_TYPE _bossBGM = SOUND_TYPE.BGM_BOSS;
	[Tooltip("보스 BGM 전환(페이드아웃→새BGM→페이드인) 총 시간(초).")]
	[SerializeField] private float _bossBGMFadeDuration = 1.5f;
	// 현재 씬의 BGM(보스 처치 후 복귀용). PlaySceneBGM에서 저장.
	private SOUND_TYPE _currentSceneBGM;
	private bool _hasCurrentSceneBGM = false;



	public GAME_STATE curState;

    // 상태 변화 시 UI에서 구독 (패널 전환 등)
    public GameStateHandler onGameStateChanged;

    // Time.timeScale 대신 플래그로 제어
    // Player, Enemy 등 게임 로직에서 이 값을 체크해 스스로 멈춤
    // UI / 음악 / 연출은 영향 없음
    public bool IsPaused   { get; private set; }
    public bool IsGameOver { get; private set; }

    // 게임플레이가 멈춰야 하는 상태(일시정지 또는 게임오버). Unit이 아닌 투사체 등이 이동 정지 판정에 참조.
    // (Unit은 자체 ShouldPause 사용 — 같은 조건)
    public bool IsGameplayFrozen => IsPaused || IsGameOver;

    // 온라인 멀티(오프라인 모드 아님 + 룸 입장 상태)면 true. 일시정지 시 시간을 멈출지 판정에 사용.
    // 멀티에선 남들이 계속 플레이 중이라 시간을 멈추면 안 됨 → 메뉴/사운드만 처리하고 프리즈는 안 함.
    private bool IsMultiplayer => PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode;

	// 게임플레이 코루틴용 "일시정지 인지" 대기.
	// WaitForSeconds는 timeScale 기준이라 이 프로젝트의 플래그 방식 일시정지(IsGameplayFrozen)를 무시함.
	// 스폰/실드회복 등 게임플레이 코루틴의 WaitForSeconds를 이걸로 대체하면 프리즈 동안 시간이 안 흐름.
	// (instance가 없으면(테스트 씬 등) 프리즈 개념이 없으므로 일반 시간처럼 흐름)
	public static IEnumerator WaitGameplaySeconds(float seconds)
	{
		float elapsed = 0f;
		while (elapsed < seconds)
		{
			if (instance == null || !instance.IsGameplayFrozen)
			{
				elapsed += Time.deltaTime;
			}
			yield return null;
		}
	}

	// =====================================================================
	// Player 레퍼런스 (씬 로드 후 자동 캐싱)
	// =====================================================================
	[Header("━━━━플레이어 연결상태(자동)━━━━")]
	[Tooltip("씬 로드 후 자동캐싱")]
    public Player playerRef;

    // =====================================================================
    // 보스 스폰 조건
    // =====================================================================
    [Header("━━━━━━ 보스 스폰 조건 ━━━━━━")]
    [Tooltip("이 수만큼 적을 처치하면 킬 조건 충족 (0이면 킬 조건 미사용)")]
    public int killCountToSpawnBoss = 20;

    // 특정 '파괴 목표'(중간보스/기지 등)는 그 오브젝트에 BossSpawnTarget 컴포넌트를 붙이면 자동 등록됨.
    // 목표가 하나도 없으면 파괴 목표 조건 미사용. 킬 조건 + 파괴 목표 조건을 둘 다 충족해야 보스 스폰(AND, 순서 무관).
    [HideInInspector] public int  killCount;
    [HideInInspector] public bool bossSpawned;

    // 파괴 목표(특정 오브젝트) 추적. BossSpawnTarget이 활성 시 등록, 파괴(비활성) 시 통지.
    private readonly HashSet<GameObject> _bossTargets = new HashSet<GameObject>();
    private int _bossTargetsTotal;
    private int _bossTargetsDestroyed;

    // Quest UI 참조용 진행도
    public int KillProgress => killCount;
    public int KillGoal => killCountToSpawnBoss;
    public int BossTargetsDestroyed => _bossTargetsDestroyed;
    public int BossTargetsTotal => _bossTargetsTotal;

    // 보스 스폰 조건 달성 시 발행 (SpawnManager 등이 구독)
    public BossSpawnHandler onBossSpawn;
    // 보스 처치 시 발행
    public event System.Action onBossKilled;
    // 목표 진행도(킬/파괴 목표) 변경 시 발행. Quest UI 등이 구독해 갱신.
    public event System.Action onObjectiveChanged;

    // =====================================================================
    // 페이드 연출
    // =====================================================================
    [Header("━━━━━━ 페이드 설정 ━━━━━━")]
    [Tooltip("페이드 연출용 CanvasGroups. GameManager 자식 Canvas/Image 부착. null이면 페이드 스킵.")]
    [SerializeField] private UnityEngine.UI.Image _fadeImage;
    [Tooltip("페이드 인/아웃 시간 (초)")]
    public float fadeDuration = 0.5f;

    // =====================================================================
    // 아이템 데이터베이스
    // =====================================================================
    [Header("━━━━━━ 아이템 데이터베이스 ━━━━━━")]
    [Tooltip("ItemDatabase.asset 연결 필수. Awake에서 Init() 호출.")]
    public ItemDatabase itemDatabase;

    [Header("━━━━━━ 스킬 데이터베이스 ━━━━━━")]
    [Tooltip("SkillDatabase.asset 연결 필수. Awake에서 Init() 호출.")]
    public SkillDatabase skillDatabase;

    // =====================================================================
    // 저장 경로 / 재시작 상태
    // =====================================================================
    private string SavePath(int slot) =>
        Path.Combine(Application.persistentDataPath, $"save{slot}.json");

    // 마지막으로 저장/로드한 슬롯. 사망 재시작 시 이 슬롯을 복원 대상으로 사용.
    private int _lastSaveSlot = 0;
    // 다음 씬 로드 완료 시 세이브를 복원할지 여부 (사망 재시작 전용).
    private bool _restoreOnNextLoad = false;

    // =====================================================================
    // 저장 가능 여부 (STATION 씬에서만 true)
    // =====================================================================
    public bool CanSave =>
        SceneManager.GetActiveScene().name == SCENE_TYPE.STATION.ToString();

    // =====================================================================
    // 초기화
    // =====================================================================
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
			// 씬-BGM 매핑을 인스펙터 리스트에서 구성
			_sceneBGMMap.Clear();
			foreach (SceneBGM entry in _sceneBGMList)
			{
				_sceneBGMMap[entry.scene.ToString()] = entry.bgm;
			}

			if (itemDatabase != null)
            {
                itemDatabase.Init();
            }
            else
            {
                Debug.LogWarning("[GameManager] itemDatabase 미연결. 저장/로드 시 파츠 복원 불가.");
            }

            if (skillDatabase != null)
            {
                skillDatabase.Init();
            }
            else
            {
                Debug.LogWarning("[GameManager] skillDatabase 미연결. 저장/로드 시 스킬 복원 불가.");
            }
        }
        else if (instance != this)
        {
            Debug.LogWarning("[GameManager] 중복 감지. 파괴 후 기존 유지");
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 씬 로드 완료 시 자동 호출
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Player 레퍼런스 갱신
        playerRef = FindObjectOfType<Player>();

        // BGM 재생
        PlaySceneBGM(scene.name);

        // 씬 진입 시 전투 카운터 초기화.
        // 킬카운트/파괴수/보스플래그는 스테이지 단위 값이라 씬을 넘어가면 항상 리셋한다.
        // (특정 스테이지 씬 이름에 의존하지 않음 — 어느 씬에서 리셋돼도 부작용 없음)
        ResetBattleData();

        // 사망 후 재시작(A안): 마지막 세이브를 복원.
        // 단, 이 시점(sceneLoaded)은 새 Player의 Start()(기본 로드아웃 장착)보다 먼저 실행되므로,
        // 여기서 바로 복원하면 직후 Start()의 기본 로드아웃에 덮어써진다.
        // → 한 프레임 기다렸다가(모든 Start 완료 후) 복원한다.
        if (_restoreOnNextLoad)
        {
            _restoreOnNextLoad = false;
            StartCoroutine(RestoreAfterLoad(_lastSaveSlot));
        }

        StartCoroutine(FadeIn());
    }

    // 씬 내 모든 오브젝트의 Start()가 끝난 뒤 세이브를 복원 (기본 로드아웃 덮어쓰기 방지).
    private IEnumerator RestoreAfterLoad(int slot)
    {
        yield return null; // Start() 단계 통과 대기
        playerRef = FindObjectOfType<Player>();
        yield return LoadDataRoutine(slot, null); // 서버/로컬 로드(비동기) 완료까지 대기
    }

    // =====================================================================
    // 씬 전환
    // 반드시 이 메서드를 통해 씬을 전환할 것 (직접 SceneManager 호출 금지)
    // =====================================================================
    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    public void LoadScene(SCENE_TYPE sceneType)
    {
        StartCoroutine(LoadSceneRoutine(sceneType.ToString()));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        // 전환 전 초기화
        // 메뉴(일시정지/인벤토리)가 열린 채 씬이 전환돼도 다음 씬은 정상 진행되도록 초기화
        _pauseRequests = 0;
        IsPaused = false;
        Time.timeScale = 1f;
        // 남아있는 투사체는 미리 정지(페이드 동안 날아다니거나 데미지 주지 않게).
        // BGM은 여기서 끊지 않는다 — FadeOut이 화면과 함께 BGM 볼륨을 페이드다운한다.
        PoolManager.Instance.DisableAllProjectiles();

        yield return StartCoroutine(FadeOut());

        // 완전한 검은 화면을 한 프레임 렌더한 뒤(로드 히치 동안 검은 화면 유지),
        yield return null;

        // 언로드 직전에 사운드/이펙트 정리 — 페이드 동안 적/터렛이 재생·재대여한 스피커/이펙트까지
        // 이 시점에 회수해야 DontDestroyOnLoad 매니저가 파괴된 참조를 들고 가는 문제(재시작 직후 경고)를 막는다.
        SoundManager.Instance.StopSFXAll();
        VFXManager.Instance.ReturnAll();

        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator FadeOut()
    {
        if (_fadeImage == null)
        {
            yield break;
        }
        Color fadeColor = _fadeImage.color;
        fadeColor.a = 0f;
        _fadeImage.color = fadeColor;
        _fadeImage.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float fadeProgress = Mathf.Clamp01(elapsed / fadeDuration);
            fadeColor.a = fadeProgress;                                  // 화면: 투명 → 검정
            _fadeImage.color = fadeColor;
            SoundManager.Instance.SetBGMFadeFactor(1f - fadeProgress);   // BGM: 설정 볼륨 → 0 (같이 줄어듦)
            yield return null;
        }
        fadeColor.a = 1f;
        _fadeImage.color = fadeColor;
        SoundManager.Instance.SetBGMFadeFactor(0f);
    }

    private IEnumerator FadeIn()
    {
        // [임시 진단] 페이드인 안 되는 원인 확인 — 호출 여부/이미지 null/활성/알파 로그
        //Debug.Log($"[GameManager] FadeIn 호출: _fadeImage null={_fadeImage == null}, " +
        //    $"active={(_fadeImage != null && _fadeImage.gameObject.activeInHierarchy)}, " +
        //    $"alpha={(_fadeImage != null ? _fadeImage.color.a : -1f)}");

        if (_fadeImage == null)
        {
            yield break;
        }
        Color fadeColor = _fadeImage.color;
        fadeColor.a = 1f;
        _fadeImage.color = fadeColor;
        _fadeImage.gameObject.SetActive(true);
        // 새 씬 BGM은 PlaySceneBGM이 이미 설정 볼륨으로 재생 중 — 페이드인 시작 시점에 0으로 낮춰두고 올린다.
        SoundManager.Instance.SetBGMFadeFactor(0f);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float fadeProgress = Mathf.Clamp01(elapsed / fadeDuration);
            fadeColor.a = 1f - fadeProgress;                          // 화면: 검정 → 투명
            _fadeImage.color = fadeColor;
            SoundManager.Instance.SetBGMFadeFactor(fadeProgress);     // BGM: 0 → 설정 볼륨 (같이 커짐)
            yield return null;
        }
        fadeColor.a = 0f;
        _fadeImage.color = fadeColor;
        _fadeImage.gameObject.SetActive(false);
        SoundManager.Instance.SetBGMFadeFactor(1f);
    }

    private void PlaySceneBGM(string sceneName)
    {
        if (_sceneBGMMap.TryGetValue(sceneName, out SOUND_TYPE bgm))
        {
            // 보스 처치 후 이 씬 BGM으로 복귀하기 위해 기억
            _currentSceneBGM = bgm;
            _hasCurrentSceneBGM = true;
            SoundManager.Instance.PlayBGM(bgm);
        }
        else
        {
            _hasCurrentSceneBGM = false;
        }
        // 매핑 없는 씬(로딩, 맵선택 등)은 BGM 유지 or 중지 선택
        // SoundManager.Instance.StopBGM(); // 중지 원할 시 주석 해제

        // switch 방식 메모 (Dictionary 방식으로 교체됨, 필요 시 아래 복원)
        //switch (sceneName)
        //{
        //    case "MAIN":      SoundManager.Instance.PlayBGM(SOUND_TYPE.BGM_MAIN);     break;
        //    case "STAGE1":    SoundManager.Instance.PlayBGM(SOUND_TYPE.BGM_STAGE1);   break;
        //    case "STATION":   SoundManager.Instance.PlayBGM(SOUND_TYPE.BGM_STATION);  break;
        //    case "GAME_OVER": SoundManager.Instance.PlayBGM(SOUND_TYPE.BGM_GAMEOVER); break;
        //    default:          /* BGM 유지 또는 StopBGM() */                           break;
        //}
    }

    // =====================================================================
    // UI에서 호출하는 공개 메서드
    // =====================================================================

    /// <summary>새 게임 시작. 데이터 초기화 후 로딩 시퀀스로 이동.</summary>
    public void NewGame()
    {
        ClearData();
        ChangeState(GAME_STATE.PLAYING);
        LoadScene(SCENE_TYPE.LOADING_SEQUENCE);
    }

    /// <summary>저장된 게임 불러오기. 세이브 슬롯 번호로 호출.</summary>
    public void LoadGame(int saveSlotNum)
    {
        StartCoroutine(LoadGameRoutine(saveSlotNum));
    }

    // 서버/로컬 로드가 비동기라, 로드 완료 후 상태 전환 + 씬 이동.
    private IEnumerator LoadGameRoutine(int saveSlotNum)
    {
        yield return LoadDataRoutine(saveSlotNum, null);
        ChangeState(GAME_STATE.PLAYING);
        LoadScene(SCENE_TYPE.LOADING_SEQUENCE);
    }

    //=================================
    // 저장/로드 처리 흐름 메모
    // [저장 흐름]
    //   GameManager.SaveGame()
    //     <- Player에서 현재 HP/실드 읽어옴
    //     <- PlayerLoadout에서 장비/탄약 읽어옴
    //     <- Inventory에서 아이템 목록 읽어와서
    //     <- 하나의 SaveData로 패킹 후 JSON 파일 저장
    //
    // [로드 흐름]
    // GameManager.LoadGame()
    //     <- JSON 파일 읽어 SaveData로 파싱
    //     <- Player에 HP/실드 등 복원
    //     <- PlayerLoadout에 장비/탄약 복원
    //     <- Inventory에 아이템 목록 복원
    //=================================

    /// <summary>현재 게임 저장. STATION 씬에서만 가능.</summary>
    public void SaveGame(int saveSlotNum)
    {
        if (!CanSave)
        {
            Debug.LogWarning("[GameManager] 저장은 마을(STATION)에서만 가능합니다.");
            return;
        }
        SaveData(saveSlotNum);
    }

    // 일시정지를 요청한 UI 개수. 인벤토리 + ESC 메뉴처럼 여러 창이 동시에 열려도
    // 마지막 창이 닫힐 때까지 일시정지가 유지되도록 참조 카운트로 관리.
    private int _pauseRequests = 0;

    /// <summary>
    /// 일시정지 요청. 게임오버/스테이지클리어가 아니면 동작(테스트 씬 직접 실행 포함).
    /// Time.timeScale을 건드리지 않으므로 UI / 음악 / 연출은 그대로 동작.
    /// Player, Enemy 등 게임 로직은 IsPaused를 체크해서 스스로 멈춤.
    /// 여러 UI가 각각 호출할 수 있으며, 호출한 만큼 ResumeGame으로 해제해야 재개됨.
    /// </summary>
    /// <returns>메뉴를 띄워도 되는 상태면 true(게임오버/클리어면 false).</returns>
    public bool PauseGame()
    {
        // 게임오버/클리어 상태에선 일시정지 불가. 그 외(PLAYING, 메뉴, 테스트 씬 등)는 허용.
        if (curState == GAME_STATE.GAME_OVER || curState == GAME_STATE.STAGE_CLEAR) return false;

        // 멀티(온라인)에선 시간을 멈추지 않는다 — 남들은 계속 플레이 중이므로.
        // 메뉴는 뜨고(호출자가 표시), 여기선 사운드 감쇠만 한다. 게임 로직 프리즈는 안 함.
        if (IsMultiplayer)
        {
            SoundManager.Instance.SetBGMAtGamePaused(true);
            return true;
        }

        _pauseRequests++;
        if (!IsPaused)
        {
            IsPaused = true;
            ChangeState(GAME_STATE.PAUSED);
            FreezeParticles(); // 폭발/트레일 등 파티클도 정지
            SoundManager.Instance.SetBGMAtGamePaused(true); // 일시정지 중 BGM 볼륨 감쇠(pauseBGMVolumeScale)
        }
        return true;
    }

    /// <summary>일시정지 해제 요청. 모든 요청이 해제되면 게임 재개.</summary>
    public void ResumeGame()
    {
        // 멀티에선 프리즈를 안 걸었으므로 사운드만 원복.
        if (IsMultiplayer)
        {
            SoundManager.Instance.SetBGMAtGamePaused(false);
            return;
        }

        if (!IsPaused) return;
        _pauseRequests--;
        if (_pauseRequests <= 0)
        {
            _pauseRequests = 0;
            IsPaused = false;
            ChangeState(GAME_STATE.PLAYING);
            UnfreezeParticles(); // 정지했던 파티클 재개
            SoundManager.Instance.SetBGMAtGamePaused(false); // BGM 볼륨 원복
        }
    }

    // =====================================================================
    // 파티클 정지/재개 (일시정지 전용)
    // Time.timeScale을 안 쓰므로 파티클은 자동으로 안 멈춤 → 일시정지 시 재생 중인 것만
    // 골라 Pause, 재개 시 그것만 다시 Play. (게임오버 땐 폭발 연출이 재생돼야 하므로 미적용)
    // =====================================================================
    private readonly List<ParticleSystem> _pausedParticles = new List<ParticleSystem>();

    private void FreezeParticles()
    {
        _pausedParticles.Clear();
        ParticleSystem[] all = FindObjectsOfType<ParticleSystem>();
        foreach (ParticleSystem ps in all)
        {
            if (ps.isPlaying)
            {
                ps.Pause();
                _pausedParticles.Add(ps);
            }
        }
    }

    private void UnfreezeParticles()
    {
        foreach (ParticleSystem ps in _pausedParticles)
        {
            if (ps != null)
            {
                ps.Play();
            }
        }
        _pausedParticles.Clear();
    }

    // =====================================================================
    // 게임 내부에서 호출하는 메서드
    // =====================================================================

    /// <summary>
    /// 플레이어 사망 시 Player.Die()에서 호출.
    /// Time.timeScale 건드리지 않음 - 죽음 연출(폭발 등)이 재생되어야 하므로.
    /// UI / 음악은 onGameStateChanged 이벤트로 처리.
    /// </summary>
    public void GameOver()
    {
        if (curState == GAME_STATE.GAME_OVER)
        {
            return;
        }
        IsGameOver = true;
        ChangeState(GAME_STATE.GAME_OVER);
        PoolManager.Instance.DisableAllProjectiles();//현재 투사체 모두 비활성화
        SoundManager.Instance.StopSFXAll();//모든 나고있던 효과음 중지
        //기타 필요한 ui연출이나 사운드, 이펙트연출은 추가로 작성필요
    }

    /// <summary>
    /// 사망 후 현재 스테이지를 처음부터 재시작
    /// 게임오버 상태를 풀고 현재 씬을 리로드. 마지막 세이브(STATION 저장 시점)가
    /// 있으면 씬 로드 완료 후 영구 진행도를 복원하고, 없으면 프리팹 기본값으로 시작.
    /// </summary>
    public void RestartStage()
    {
        IsGameOver = false;
        _restoreOnNextLoad = File.Exists(SavePath(_lastSaveSlot));
        ChangeState(GAME_STATE.PLAYING);
        LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>스테이지 클리어 조건 달성 시 호출. (게임 전체 클리어와는 별개 — 그건 별도 로직 필요, 아직 미구현)</summary>
    public void StageClear()
    {
        if (curState == GAME_STATE.STAGE_CLEAR)
        {
            return;
        }
        ChangeState(GAME_STATE.STAGE_CLEAR);
        PoolManager.Instance.DisableAllProjectiles();
    }

	/// <summary>
	/// 적 처치 시 Enemy.Die()에서 호출.
	/// 킬카운트 누적 후 보스 스폰 조건 체크.
	/// </summary>
	public void OnEnemyKilled(GameObject killer, int exp, int gold)
    {
        killCount++;
        onObjectiveChanged?.Invoke();
        GiveRewardToPlayer(killer, exp, gold);  
        CheckBossSpawnCondition();
    }

    /// <summary>
    /// 킬 보상을 '죽인 사람'에게 지급. killer의 Player를 찾아 경험치/골드 지급.
    /// killer가 플레이어가 아니면(환경 사망 등) 지급 없음. 멀티 땐 killer 기준으로 각자에게 귀속됨
    /// (단, 골드 인벤토리는 아직 전역 싱글톤이라 멀티에선 플레이어별로 분리 필요).
    /// </summary>
    private void GiveRewardToPlayer(GameObject killer, int exp, int gold)
    {
        if (killer == null)
        {
            return;
        }
        Player player = killer.GetComponentInParent<Player>();
        if (player == null)
        {
            return;   // 플레이어가 죽인 게 아니면 보상 없음
        }
        player.GainExp(exp);
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.gold += gold;
        }
    }

    /// <summary>
    /// 보스 처치 시 보스 오브젝트에서 호출.
    /// 킬카운트 누적 + onBossKilled 이벤트 발행.
    /// </summary>
    public void OnBossKilled()
    {
        killCount++;
        // 보스 처치 → 현재 씬 BGM으로 페이드 복귀(매핑 있을 때만)
        if (_hasCurrentSceneBGM)
        {
            SoundManager.Instance.ChangeBGMWithFade(_currentSceneBGM, _bossBGMFadeDuration);
        }
        onBossKilled?.Invoke();
    }

    /// <summary>BossSpawnTarget이 활성 시 자기 등록. 보스 스폰 '파괴 목표' 수에 포함.</summary>
    public void RegisterBossTarget(GameObject target)
    {
        if (target == null) return;
        if (_bossTargets.Add(target))
        {
            _bossTargetsTotal++;
            onObjectiveChanged?.Invoke();
        }
    }

    /// <summary>BossSpawnTarget이 파괴(비활성) 시 통지. 남은 목표에서 제거 후 조건 체크.</summary>
    public void NotifyBossTargetDestroyed(GameObject target)
    {
        if (target == null) return;
        if (_bossTargets.Remove(target))
        {
            _bossTargetsDestroyed++;
            onObjectiveChanged?.Invoke();
            CheckBossSpawnCondition();
        }
    }

    // =====================================================================
    // 내부 메서드
    // =====================================================================
    
    private void ChangeState(GAME_STATE state)
    {
        curState = state;
        onGameStateChanged?.Invoke(curState);
    }

    /// <summary>보스 스폰 조건 체크. 조건 달성 시 onBossSpawn 이벤트 발행.</summary>
    private void CheckBossSpawnCondition()
    {
        if (bossSpawned) return;

        // 각 조건: 미설정(임계값 0 / 목표 없음)이면 '충족'으로 간주 → 킬만/목표만 단독 사용도 가능.
        bool killDone    = killCountToSpawnBoss <= 0 || killCount             >= killCountToSpawnBoss;
        bool targetsDone = _bossTargetsTotal    <= 0 || _bossTargetsDestroyed >= _bossTargetsTotal;
        // 조건이 하나도 없으면(둘 다 미설정) 스폰 안 함.
        bool hasAnyCondition = killCountToSpawnBoss > 0 || _bossTargetsTotal > 0;

        // 순서 무관 — 킬/파괴 어느 쪽이 나중에 채워지든, 채워지는 순간 둘 다 충족되면 스폰(AND).
        if (hasAnyCondition && killDone && targetsDone)
        {
            bossSpawned = true;
            Debug.Log($"[GameManager] 보스 스폰 조건 달성 (킬 {killCount}/{killCountToSpawnBoss}, 목표파괴 {_bossTargetsDestroyed}/{_bossTargetsTotal})");
            // 보스 등장 → 보스 BGM으로 페이드 전환
            SoundManager.Instance.ChangeBGMWithFade(_bossBGM, _bossBGMFadeDuration);
            onBossSpawn?.Invoke();
        }
    }

    /// <summary>전투 씬 진입 시 전투 관련 카운터 초기화.</summary>
    private void ResetBattleData()
    {
        killCount = 0;
        bossSpawned = false;
        _bossTargets.Clear();
        _bossTargetsTotal = 0;
        _bossTargetsDestroyed = 0;
        onObjectiveChanged?.Invoke();
    }

    // =====================================================================
    // 세이브 / 로드
    // =====================================================================

    /// <summary>Player 등에서 현재 상태를 수집해 저장. 로컬 파일(항상) + 로그인 시 서버에도.</summary>
    private void SaveData(int saveSlot)
    {
        _lastSaveSlot = saveSlot;
        SaveData data = CollectSaveData();

        // 로컬 저장 — 항상 수행(오프라인/서버다운 시 폴백 겸함).
        string json  = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(SavePath(saveSlot), json);
        Debug.Log($"[GameManager] 로컬 저장 완료: {SavePath(saveSlot)}");

        // 로그인 상태면 서버에도 저장(비동기, 실패해도 로컬은 남음).
        if (ServerApi.Instance != null && ServerApi.Instance.IsLoggedIn)
        {
            ServerApi.Instance.StartCoroutine(ServerApi.Instance.SaveCo(data,
                () => Debug.Log("[GameManager] 서버 저장 성공"),
                err => Debug.LogWarning($"[GameManager] 서버 저장 실패(로컬은 저장됨): {err}")));
        }
    }

    /// <summary>
    /// 데이터 로드 → Player 등에 적용. 로그인 상태면 서버에서 먼저 시도하고, 실패/미로그인 시 로컬 파일.
    /// 서버 통신이 비동기라 코루틴. 완료 후 onDone 콜백(있으면) 호출.
    /// </summary>
    private IEnumerator LoadDataRoutine(int saveSlot, System.Action onDone)
    {
        _lastSaveSlot = saveSlot;

        if (ServerApi.Instance != null && ServerApi.Instance.IsLoggedIn)
        {
            bool serverOk = false;
            yield return ServerApi.Instance.LoadCo(
                data =>
                {
                    if (data != null)
                    {
                        ApplySaveData(data);
                        serverOk = true;
                        Debug.Log("[GameManager] 서버 로드 완료");
                    }
                },
                err => Debug.LogWarning($"[GameManager] 서버 로드 실패, 로컬 시도: {err}"));

            if (!serverOk)
            {
                LoadLocal(saveSlot);   // 서버 실패 → 로컬 폴백
            }
        }
        else
        {
            LoadLocal(saveSlot);
        }

        onDone?.Invoke();
    }

    /// <summary>로컬 JSON 파일에서 데이터를 읽어 Player 등에 분배.</summary>
    private void LoadLocal(int saveSlot)
    {
        string path = SavePath(saveSlot);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[GameManager] 세이브 파일 없음: {path}");
            return;
        }
        string json = File.ReadAllText(path);
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        ApplySaveData(data);
        Debug.Log($"[GameManager] 로컬 로드 완료: {path}");
    }

    /// <summary>Player / Loadout 등에서 저장할 데이터 수집.</summary>
    private SaveData CollectSaveData()
    {
        SaveData data = new SaveData();
        data.gold = InventoryManager.Instance != null ? InventoryManager.Instance.gold : 0;

        // 보유 아이템(가방) 저장 (ItemStack.data(SO) → id int)
        if (InventoryManager.Instance != null && InventoryManager.Instance.items != null)
        {
            List<ItemStack> items = InventoryManager.Instance.items;
            data.ownedItems = new SavedItemStack[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                data.ownedItems[i] = new SavedItemStack
                {
                    itemId = items[i].data != null ? items[i].data.id : 0,
                    count  = items[i].count
                };
            }
        }

        // 호감도 저장 (Dictionary → 배열)
        if (AffectionManager.Instance != null)
        {
            Dictionary<NPC_ID, int> affections = AffectionManager.Instance.GetAllAffections();
            data.affections = new SavedAffection[affections.Count];
            int affectionIndex = 0;
            foreach (KeyValuePair<NPC_ID, int> pair in affections)
            {
                data.affections[affectionIndex] = new SavedAffection
                {
                    npc   = pair.Key,
                    value = pair.Value
                };
                affectionIndex++;
            }
        }

        if (playerRef != null)
        {
            data.level = playerRef.level;
            data.exp = playerRef.exp;
            data.expToNextLevel = playerRef.expToNextLevel;
            data.curHp = playerRef.curHpRemaining;
            data.curShield = playerRef.curShieldRemaining;
            data.curArmor = playerRef.curArmorRemaining;
            data.curBoost = playerRef.curBoostRemaining;
            data.curFuel = playerRef.curFuelRemaining;

            // 파츠 슬롯 저장 (SO 참조 → id int)
            UnitParts unitParts = playerRef.GetComponent<UnitParts>();
            if (unitParts != null && unitParts.partSlots != null)
            {
                data.partSlots = new SavedPartSlot[unitParts.partSlots.Count];
                for (int i = 0; i < unitParts.partSlots.Count; i++)
                {
                    PartSlotEntry slot = unitParts.partSlots[i];
                    data.partSlots[i] = new SavedPartSlot
                    {
                        slotType = slot.slotType,
                        partId   = slot.equippedPart != null ? (int)slot.equippedPart.id : 0
                    };
                }
            }

            // 미사일 슬롯 저장 (SO 참조 → id int)
            if (playerRef.weaponSystem.missileSlots != null)
            {
                data.missileSlots = new SavedMissileSlot[playerRef.weaponSystem.missileSlots.Count];
                for (int i = 0; i < playerRef.weaponSystem.missileSlots.Count; i++)
                {
                    MissileSlot src = playerRef.weaponSystem.missileSlots[i];
                    data.missileSlots[i] = new SavedMissileSlot
                    {
                        type          = src.type,
                        missileDataId = src.missileData != null ? (int)src.missileData.id : 0,
                        curAmmo       = src.curAmmo,
                        maxAmmo       = src.maxAmmo
                    };
                }
            }

            // 보유 스킬 저장 (SkillSystem이 직접 SavedSkill[] 생성)
            if (playerRef.skillSystem != null)
            {
                data.skills = playerRef.skillSystem.CollectSaveData();
            }

            // 소모품 퀵슬롯 저장 (ConsumableData(SO) → id int, 빈칸은 0)
            QuickSlot quickSlot = playerRef.GetComponent<QuickSlot>();
            if (quickSlot != null && quickSlot.slots != null)
            {
                data.quickSlotItemIds = new ITEM_ID[quickSlot.slots.Length];
                for (int i = 0; i < quickSlot.slots.Length; i++)
                {
                    data.quickSlotItemIds[i] = quickSlot.slots[i] != null ? quickSlot.slots[i].id : 0;
                }
            }
        }
        return data;
    }

    /// <summary>불러온 SaveData를 Player / Loadout 등에 적용.</summary>
    private void ApplySaveData(SaveData data)
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.gold = data.gold;

            // 보유 아이템(가방) 복원 (id int → SO 참조). 기존 목록 비우고 재구성.
            if (data.ownedItems != null && itemDatabase != null)
            {
                InventoryManager.Instance.items.Clear();
                for (int i = 0; i < data.ownedItems.Length; i++)
                {
                    SavedItemStack saved = data.ownedItems[i];
                    if (saved.itemId == 0) continue;
                    ItemData itemData = itemDatabase.Get((ITEM_ID)saved.itemId);
                    if (itemData == null)
                    {
                        Debug.LogWarning($"[GameManager] 아이템 복원 실패: id={saved.itemId}");
                        continue;
                    }
                    InventoryManager.Instance.items.Add(new ItemStack { data = itemData, count = saved.count });
                }
            }
        }

        if (AffectionManager.Instance != null)
        {
            AffectionManager.Instance.LoadAffections(data.affections);
        }

        if (playerRef != null)
        {
            playerRef.level             = data.level;
            playerRef.exp               = data.exp;
            playerRef.expToNextLevel    = data.expToNextLevel;

            // 파츠 슬롯 복원 (id int → SO 참조).
            // 좌우 런처처럼 같은 타입 슬롯이 여러 개여도 저장 순서대로 각 슬롯에 배정되도록
            // 파츠 목록을 모아 ReloadLoadout으로 일괄 재구성한다.
            // (타입 기준 Equip은 첫 슬롯만 잡아 L/R이 충돌하므로 사용하지 않음)
            if (data.partSlots != null && itemDatabase != null)
            {
                UnitParts unitParts = playerRef.GetComponent<UnitParts>();
                if (unitParts != null)
                {
                    List<PartData> savedParts = new List<PartData>();
                    for (int i = 0; i < data.partSlots.Length; i++)
                    {
                        SavedPartSlot saved = data.partSlots[i];
                        if (saved.partId == 0) continue;
                        PartData partData = itemDatabase.Get<PartData>((ITEM_ID)saved.partId);
                        if (partData == null)
                        {
                            Debug.LogWarning($"[GameManager] 파츠 복원 실패: id={saved.partId}");
                            continue;
                        }
                        savedParts.Add(partData);
                    }
                    unitParts.ReloadLoadout(savedParts);
                }
            }

            // 현재 HP/실드/아머/부스트 복원.
            // ReloadLoadout이 RefillToMax로 max를 채우므로, 파츠 복원 뒤에 세팅해야 저장값이 유지된다.
            playerRef.curHpRemaining     = data.curHp;
            playerRef.curShieldRemaining = data.curShield;
            playerRef.curArmorRemaining  = data.curArmor;
            playerRef.curBoostRemaining  = data.curBoost;
            playerRef.curFuelRemaining   = data.curFuel;

            // 미사일 슬롯 복원 (id int → SO 참조)
            if (data.missileSlots != null)
            {
                playerRef.weaponSystem.missileSlots = new List<MissileSlot>();
                for (int i = 0; i < data.missileSlots.Length; i++)
                {
                    SavedMissileSlot saved = data.missileSlots[i];
                    MissileData missileData = itemDatabase != null && saved.missileDataId != 0
                        ? itemDatabase.Get<MissileData>((ITEM_ID)saved.missileDataId)
                        : null;
                    playerRef.weaponSystem.missileSlots.Add(new MissileSlot
                    {
                        type        = saved.type,
                        missileData = missileData,
                        curAmmo     = saved.curAmmo,
                        maxAmmo     = saved.maxAmmo
                    });
                }
            }

            // 보유 스킬 복원 (skillId int → SkillData 참조, skillDatabase 통해 역참조)
            if (playerRef.skillSystem != null)
            {
                playerRef.skillSystem.LoadSaveData(data.skills);
            }

            // 소모품 퀵슬롯 복원 (id int → ConsumableData 참조, 0이면 빈칸)
            QuickSlot quickSlot = playerRef.GetComponent<QuickSlot>();
            if (quickSlot != null && data.quickSlotItemIds != null && itemDatabase != null)
            {
                for (int i = 0; i < data.quickSlotItemIds.Length; i++)
                {
                    ITEM_ID id = data.quickSlotItemIds[i];
                    ConsumableData consumable = id != 0
                        ? itemDatabase.Get<ConsumableData>(id)
                        : null;
                    quickSlot.AssignSlot(i, consumable);
                }
            }
        }
    }

    /// <summary>새 게임 시작 시 데이터 전체 초기화.</summary>
    private void ClearData()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.gold = 0;
            if (InventoryManager.Instance.items != null)
            {
                InventoryManager.Instance.items.Clear();
            }
        }
        if (AffectionManager.Instance != null)
        {
            AffectionManager.Instance.LoadAffections(null);
        }
        if (playerRef != null)
        {
            if (playerRef.skillSystem != null)
            {
                playerRef.skillSystem.LoadSaveData(null);
            }
            // 퀵슬롯 소모품 비우기
            QuickSlot quickSlot = playerRef.GetComponent<QuickSlot>();
            if (quickSlot != null && quickSlot.slots != null)
            {
                for (int i = 0; i < quickSlot.slots.Length; i++)
                {
                    quickSlot.AssignSlot(i, null);
                }
            }
        }
        ResetBattleData();
    }
}