using System.Collections;
using System.Collections.Generic;
using System.IO;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// ▶ 전체 팀 공통 참조
//   IsPaused   : 일시정지 여부. Update/FixedUpdate 첫 줄에서 ShouldPause로 체크.
//   IsGameOver : 게임오버 여부
//   curState   : 현재 게임 진행 상태 (GAME_STATE enum — PLAYING/PAUSED/GAME_OVER/STAGE_CLEAR)
//                씬 도착(OnSceneLoaded)에서 자동 확정됨. 진입 경로마다 따로 세팅하지 말 것.
//   curSceneType : 현재 씬 (SCENE_TYPE enum). 장소는 상태와 별개 축이라 따로 들고 감.
//                  IsBattleScene / IsStationScene 프로퍼티로 조회 권장
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

public class GameManager : MonoBehaviourPunCallbacks
{
    // =====================================================================
    // 싱글톤
    // =====================================================================
    private static GameManager instance = null;
    // Awake에서만 세팅됨. Awake 전엔 null이므로 최초 접근은 Start부터 할 것.
    public static GameManager Instance => instance;

    //public static GameManager Instance { get; private set; }

	// =====================================================================
	// 씬-BGM 매핑 
	//  ※ SCENE_TYPE 이름 = 실제 씬 파일 이름이어야 조회됨(scene.name 기준)
	// =====================================================================
	[System.Serializable]
	public struct SceneSettings
	{
		public SCENE_TYPE scene;

		[Tooltip("씬의 성격. 아래 체크박스는 이 값에서 자동으로 정해짐(FlagsFor).")]
		public SCENE_CATEGORY category;

		public SOUND_TYPE bgm;

		[Tooltip("카테고리 기본값 대신 아래 체크박스를 그대로 쓸지. 분류로 안 맞는 예외 씬에만 켤 것.")]
		public bool overrideFlags;

		[Tooltip("이 씬으로 '들어올 때' 쓰던 함선을 그대로 데려올지. 끄면 이 씬을 로드하는 시점에 함선이 파괴됨.\n" +
				 "판정은 출발 씬이 아니라 목적지 씬 기준임.")]
		public bool keepsPlayerShip;
		[Tooltip("이 씬에서 저장을 허용할지.")]
		public bool canSave;
		[Tooltip("전투 스테이지인지. 전투 전용 처리 분기용.")]
		public bool isBattleScene;
		[Tooltip("정거장 계열(상점/대화 등 비전투 거점)인지.")]
		public bool isStationScene;
		[Tooltip("함선을 남겨두되 조종은 막을지.")]
		public bool shipControlDisabled;
		[Tooltip("함선을 안 보이게 할지. 존재는 유지하고 렌더러·콜라이더만 끔.")]
		public bool shipHidden;
		[Tooltip("남(다른 플레이어)의 함선만 안 보이게 할지. 내 함선은 그대로 보임.\n" +
				 "격납고처럼 같은 씬에 여럿이 있어도 각자 자기 기체만 봐야 하는 씬에 씀.")]
		public bool otherShipsHidden;
	}

	[Header("<size=22>━━━━━━ 씬 설정표 ━━━━━━</size>")]
	[Header("씬 추가 시 여기에 SCENE_TYPE + BGM + 속성 체크박스로 한 줄 추가\n" +
		"SCENE_TYPE 이름 = 실제 씬 파일 이름이어야 조회됨(scene.name 기준)\n" +
		"씬 종류만 고르면 속성은 자동으로 정해짐. 표에 없는 씬은 전부 off(Awake 경고 확인)")]
	[SerializeField] private List<SceneSettings> _sceneSettings = new List<SceneSettings>();

	// 런타임 조회용. Awake에서 _sceneSettings로 구성 (key = scene.ToString())
	// 대소문자 무시 비교자 — 씬 파일명 대소문자가 enum 표기와 달라도
	// 조회가 되게 함. SceneManager.LoadScene도 대소문자를 안 가리므로 여기만 엄격하면 BGM이 조용히 누락됨.
	private Dictionary<string, SceneSettings> _sceneSettingsMap =
		new Dictionary<string, SceneSettings>(System.StringComparer.OrdinalIgnoreCase);

	[Header("보스 BGM 전환")]
	[Tooltip("보스 등장 시 전환할 BGM. 보스 처치 시 현재 씬 BGM으로 복귀.")]
	[SerializeField] private SOUND_TYPE _bossBGM = SOUND_TYPE.BGM_BOSS;
	[Tooltip("보스 BGM 전환(페이드아웃→새BGM→페이드인) 총 시간(초).")]
	[SerializeField] private float _bossBGMFadeDuration = 1.5f;
	// 현재 씬의 BGM(보스 처치 후 복귀용). PlaySceneBGM에서 저장.
	private SOUND_TYPE _currentSceneBGM;
	private bool _hasCurrentSceneBGM = false;



	public GAME_STATE curState;

	// 현재 어느 씬인지. curState(진행 흐름)와는 다른 축임 — "정거장에서 일시정지" 같은 조합을
	// 상태 하나로 표현하려 들면 값이 곱해지므로 장소는 여기서 따로 들고 감.
	// OnSceneLoaded에서 씬 이름을 SCENE_TYPE으로 파싱해 채움. 표에 없는 작업씬은 UNKNOWN.
	[Header("현재 씬 (자동 갱신, 입력X)")]
	public SCENE_TYPE curSceneType = SCENE_TYPE.UNKNOWN;

	/// <summary>전투 스테이지 씬인지 (전투 전용 처리 분기용)</summary>
	public bool IsBattleScene => SettingsOf(curSceneType).isBattleScene;

	/// <summary>정거장 계열 씬인지 (상점 등 비전투 거점)</summary>
	public bool IsStationScene => SettingsOf(curSceneType).isStationScene;

	/// <summary>함선이 있어도 조종하면 안 되는 씬인지 (격납고·맵선택 등)</summary>
	// Unit.ShouldPause / InputManager가 이걸 보고 조종·전투·커서잠금을 막음.
	public bool ShipControlDisabled => SettingsOf(curSceneType).shipControlDisabled;

	/// <summary>함선을 숨겨야 하는 씬인지 (맵선택·대기실 등). PlayerSceneVisibility가 봄.</summary>
	public bool ShipHidden => SettingsOf(curSceneType).shipHidden;

	/// <summary>남의 함선만 숨기는 씬인지 (격납고 등). 내 함선은 영향 없음.</summary>
	public bool OtherShipsHidden => SettingsOf(curSceneType).otherShipsHidden;

	/// <summary>유닛·스킬이 스스로 멈춰야 하는 상태. Unit.ShouldPause와 SkillSystem이 같이 씀.</summary>
	public bool IsUnitFrozen => IsPaused || IsGameOver || ShipControlDisabled;

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
    [Header("디버그 확인용")]
    [SerializeField]
    private int _bossTargetsTotal;
    [SerializeField]
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

    [Header("스테이지 클리어")]
    [Tooltip("클리어 판정 후 스테이션으로 넘어가기까지의 시간(초). 드랍 아이템을 주울 여유.")]
    [SerializeField] private float _stageClearSequenceTime = 8f;

    [Tooltip("클리어 시 날아가던 투사체가 더 날아갈 거리. 이만큼 간 뒤 각자 끝남(미사일은 폭발).")]
    [SerializeField] private float _stageClearProjectileRange = 30f;

    /// <summary>스테이지 클리어 시 발행. 인자는 씬이 넘어가기까지 남은 시간(초) — 클리어 UI가 구독할 것.</summary>
    public event System.Action<float> onStageClear;
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

    [Header("━━━━━━ 새 게임 시작 데이터 ━━━━━━")]
    [Tooltip("GameStartData.asset 연결. 미연결이면 새 게임이 골드 0 / 소지품 없음으로 시작함.")]
    [SerializeField] private GameStartData _gameStartData;

    // =====================================================================
    // 저장 경로 / 재시작 상태
    // =====================================================================
    // 로컬 세이브 경로. 계정별로 파일을 가름 —
    // 안 가르면 이 PC에 남은 남의 세이브가 새 계정 슬롯 목록에 그대로 뜨고, 누르면 그게 불러와짐.
    // 비로그인은 게스트 칸을 따로 씀. 옛 이름(save{slot}.json)은 이제 어느 경로로도 안 읽힘.
    private string SavePath(int slot)
    {
        long userId = ServerApi.Instance != null ? ServerApi.Instance.UserId : 0;
        string fileName = userId > 0 ? $"save_u{userId}_{slot}.json" : $"save_guest_{slot}.json";
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    /// <summary>출격 직전 자동 저장 슬롯. 사망/재시작이 되돌아갈 지점.</summary>
    // 서버가 받는 범위가 0~9라 그 안에서 잡음. 유저 슬롯은 0~4(LoadGameUI.slotCount)라 안 겹침.
    public const int AutoSaveSlot = 9;

    // 마지막으로 저장/로드한 슬롯. 사망 재시작 시 이 슬롯을 복원 대상으로 사용.
    private int _lastSaveSlot = 0;
    // 다음 씬 로드 완료 시 세이브를 복원할지 여부 (사망 재시작 전용).
    private bool _restoreOnNextLoad = false;

    // =====================================================================
    // 저장 가능 여부 — 씬 설정표의 canSave
    // curSceneType이 아니라 실제 활성 씬 이름으로 조회함. 저장은 씬 전환 도중에도 불릴 수 있어
    // 아직 갱신 전인 curSceneType을 믿으면 엉뚱한 씬 기준으로 판정됨.
    // =====================================================================
    public bool CanSave => SettingsOf(SceneManager.GetActiveScene().name).canSave;

    /// <summary>스테이지를 깨고 격납고로 들어왔는지. 수리는 이때만 허용됨.</summary>
    public bool StageClearedBeforeHangar { get; private set; }

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
            // 처음 시작한 씬은 sceneLoaded가 안 오므로(구독 시점이 이미 로드 후) 여기서 직접 채움
            curSceneType = ParseSceneType(SceneManager.GetActiveScene().name);
			BuildSceneSettingsMap();

			// 새 게임/불러오기 어느 경로로 들어와도 밑값이 있어야 하므로 여기서 한 번 캐시함
			if (_gameStartData != null)
			{
				PlayerProfile.CacheBaseStats(_gameStartData.basePlayerPrefab);
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

    // playerRef를 IsMine(로컬) 플레이어로만 갱신함.
    // 멀티에선 내 함선/남 함선이 DDOL로 씬을 넘어와 공존하므로 FindObjectOfType로 아무거나 잡으면
    // HUD·카메라가 남 함선을 따라가는 버그가 남. 로컬을 못 찾으면(스폰 전) 덮어쓰지 않고 Player.Start의 자기등록에 맡김.
    private void RefreshPlayerRefToLocal()
    {
        Player[] scenePlayers = FindObjectsOfType<Player>();
        foreach (Player candidate in scenePlayers)
        {
            if (candidate != null && candidate.IsMine)
            {
                playerRef = candidate;
                return;
            }
        }
    }

    // 씬 로드 완료 시 자동 호출
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 언로드 구간 종료 — 이제부터 오는 BossSpawnTarget OnDisable은 실제 파괴로 취급함.
        _isSceneUnloading = false;

        // 현재 씬 기록. LoadSceneRoutine이 아니라 여기서 하는 이유는 에디터에서 씬을 직접 재생하거나
        // 다른 경로로 씬이 바뀌어도 빠짐없이 잡히기 때문임.
        curSceneType = ParseSceneType(scene.name);

        // 진행 상태도 도착 씬 기준으로 확정함 — 진입 경로(싱글 시작/불러오기, 멀티 대기실, 재시작)가
        // 여러 갈래여도 씬 도착은 전부 이 지점을 지나므로 한 군데서 정하는 게 맞음.
        // 게임오버 씬은 GameOver()가 정한 값을 그대로 유지해야 하므로 제외함
        // (여기서 초기화하면 GameOverUI.Start의 IsGameOver 검사가 패널을 다시 꺼버림).
        // 로딩 씬도 제외 — 잠깐 스쳐가는 곳이라 여기서 상태를 지우면 목적지(RESULT)에 도착했을 때
        // STAGE_CLEAR/GAME_OVER가 이미 사라져 결과 UI가 안 뜸.
        if (curSceneType != SCENE_TYPE.RESULT && curSceneType != SCENE_TYPE.LOADING_SEQUENCE)
        {
            // 게임오버 씬을 벗어나는 순간 정지 플래그도 같이 내림 — 안 내리면 IsGameplayFrozen이 계속 참이라
            // 다음 플레이에서 유닛/퀵슬롯이 전부 멈춘 상태로 시작함.
            IsGameOver = false;

            if (IsGameplayScene(curSceneType))
            {
                ChangeState(GAME_STATE.PLAYING);
            }
            else
            {
                ChangeState(GAME_STATE.NONE);
            }
        }

        // Player 레퍼런스 갱신 — 멀티에선 원격 함선(DDOL로 씬 넘어와 공존)을 잡지 않도록 IsMine인 로컬만 채운다.
        RefreshPlayerRefToLocal();

        // BGM 재생
        PlaySceneBGM(scene.name);

        // 씬 진입 시 전투 카운터 초기화.
        // 킬카운트/파괴수/보스플래그는 스테이지 단위 값이라 씬을 넘어가면 항상 리셋한다.
        // (특정 스테이지 씬 이름에 의존하지 않음 — 어느 씬에서 리셋돼도 부작용 없음)
        ResetBattleData();

        // 예약된 세이브 복원(재시작/스테이션 복귀/불러오기).
        // 단, 이 시점(sceneLoaded)은 새 Player의 Start()(기본 로드아웃 장착)보다 먼저 실행되므로,
        // 여기서 바로 복원하면 직후 Start()의 기본 로드아웃에 덮어써진다.
        // → 한 프레임 기다렸다가(모든 Start 완료 후) 복원한다.
        //
        // 로딩 씬은 건너뜀 — 거긴 잠깐 스쳐가는 곳이라 여기서 예약을 써버리면
        // 정작 목적지 씬에서는 복원이 안 됨.
        if (_restoreOnNextLoad && curSceneType != SCENE_TYPE.LOADING_SEQUENCE)
        {
            StartCoroutine(RestoreAfterLoad(_lastSaveSlot));
        }

        // StopSFXAll이 회수한 엔진 루프 재등록
        if (playerRef != null)
        {
            playerRef.RegisterEngineSound();
        }

        StartCoroutine(FadeIn());
    }

    // 씬 내 모든 오브젝트의 Start()가 끝난 뒤 세이브를 복원 (기본 로드아웃 덮어쓰기 방지).
    private IEnumerator RestoreAfterLoad(int slot)
    {
        yield return null; // Start() 단계 통과 대기

        // 함선은 PlayerSpawner가 스폰하는데 연결이 늦으면 몇 프레임 뒤에 나옴.
        // 없는 상태로 복원하면 로드아웃/HP가 통째로 누락되므로 잠깐 기다려줌.
        // 스포너가 없는 씬(스테이션 등)은 기다려도 안 나오므로 바로 넘어감.
        RefreshPlayerRefToLocal();
        if (playerRef == null && FindObjectOfType<PlayerSpawner>() != null)
        {
            const float waitLimit = 3f;
            float waited = 0f;
            while (playerRef == null && waited < waitLimit)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
                RefreshPlayerRefToLocal();
            }
        }

        yield return LoadDataRoutine(slot, null); // 서버/로컬 로드(비동기) 완료까지 대기
        _restoreOnNextLoad = false;
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

    public void LoadSceneWithLoading(string sceneName)
    {
        LoadingManager.NextScene = sceneName;
        LoadScene(SCENE_TYPE.LOADING_SEQUENCE);
    }

	public void LoadSceneWithLoading(SCENE_TYPE sceneType)
	{
		LoadingManager.NextScene = sceneType.ToString();
		LoadScene(SCENE_TYPE.LOADING_SEQUENCE);
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
		PoolManager.Instance.DisableAllEnemies();
		// 드랍된 아이템/골드도 같이 정리 — 이것도 풀 오브젝트라 DDOL에 남아 다음 씬까지 떠다님
		PoolManager.Instance.DisableAllItems();

		yield return StartCoroutine(FadeOut());

        // 완전한 검은 화면을 한 프레임 렌더한 뒤(로드 히치 동안 검은 화면 유지),
        yield return null;

        // 언로드 직전에 사운드/이펙트 정리 — 페이드 동안 적/터렛이 재생·재대여한 스피커/이펙트까지
        // 이 시점에 회수해야 DontDestroyOnLoad 매니저가 파괴된 참조를 들고 가는 문제(재시작 직후 경고)를 막는다.
        SoundManager.Instance.StopSFXAll();
        VFXManager.Instance.ReturnAll();

      
        PlayerProfile.CaptureFrom(playerRef);

        if (!KeepsPlayerShip(ResolveDestination(sceneName)))
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.HasLocalPlayerShip)
            {
                NetworkManager.Instance.DestroyLocalPlayerShip();
            }
            else if (playerRef != null)
            {
                Destroy(playerRef.gameObject);
            }
            playerRef = null;
        }

        // 게임오버로 빠질 때는 방에서도 나간다.
        //
        // 적은 룸 종속 오브젝트라 AI가 방장에서만 돌고, 방장이 전투씬을 떠나면서 적을 비활성화하면
        // 남은 사람 화면에서 적이 그대로 멈춰버린다. 방장이 방에 남아 있는 한 Photon은 마스터를
        // 넘기지 않으므로 SpawnManager.OnMasterClientSwitched(웨이브 인계)도 실행되지 않는다.
        // 방을 나가야 마스터가 다음 사람에게 넘어가고 진행이 이어진다.
        //
        // 다른 씬 전환(스테이션↔스테이지 등)에서는 방을 유지해야 하므로 게임오버일 때만 나감.
        // 함선 정리를 마친 뒤에 나가야 PhotonNetwork.Destroy가 정상 처리됨.
        if (PhotonNetwork.InRoom && sceneName == SCENE_TYPE.RESULT.ToString())
        {
            PhotonNetwork.LeaveRoom();
        }

        // 씬 언로드 중 BossSpawnTarget들이 줄줄이 OnDisable을 맞는데, 그건 '파괴'가 아니라 정리 과정임 —
        // 목표 달성으로 세어버리면 씬 나가는 중에 보스 조건이 터짐. 로드 완료(OnSceneLoaded)에서 해제함.
        _isSceneUnloading = true;
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
        SOUND_TYPE bgm = SettingsOf(sceneName).bgm;
        if (bgm != SOUND_TYPE.SFX_NONE)
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

    /// <summary>새 게임 시작. 데이터 초기화 + 시작 데이터 지급 후 지정 씬으로 이동(로딩 씬 경유).</summary>
    public void NewGame(SCENE_TYPE destination)
    {
        LoadingManager.NextScene = destination.ToString();
        NewGame();
    }

    /// <summary>새 게임 시작. 목적지는 LoadingManager.NextScene에 미리 넣어둘 것.</summary>
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

    /// <summary>세이브를 불러온 뒤 지정한 씬으로 이동(로딩 씬 경유).</summary>
    // LoadGame은 목적지를 안 정해서 LoadingManager.NextScene에 남아 있던 이전 값으로 가버림.
    // 목적지를 먼저 박아두고 같은 경로를 태움.
    public void LoadGameWithLoading(int saveSlotNum, SCENE_TYPE destination)
    {
        LoadingManager.NextScene = destination.ToString();
        LoadGame(saveSlotNum);
    }

    // 서버/로컬 로드가 비동기라, 로드 완료 후 상태 전환 + 씬 이동.
    private IEnumerator LoadGameRoutine(int saveSlotNum)
    {
        yield return LoadDataRoutine(saveSlotNum, null);
        ChangeState(GAME_STATE.PLAYING);
        // 여기서의 복원은 함선이 없는 씬(로비/메인)에서 도는 경우가 많아 기체 부분이 통째로 누락됨.
        // 목적지에 함선이 스폰된 뒤 한 번 더 씌우도록 예약함.
        QueueRestore(saveSlotNum);
        LoadScene(SCENE_TYPE.LOADING_SEQUENCE);
    }

    /// <summary>해당 슬롯에 세이브 파일이 있는지. LOAD 버튼 활성/비활성 판정용.</summary>
    public bool HasSave(int slot)
    {
        return File.Exists(SavePath(slot));
    }

    /// <summary>
    /// 로컬 세이브 파일에서 목록 표시용 요약(레벨/골드/시각)만 읽음. 파일이 없거나 깨졌으면 null.
    /// 불러오기 창이 비로그인에서도 목록을 그릴 수 있게 하려는 것 — 서버 목록과 같은 모양으로 돌려줌.
    /// 저장 시각은 파일 수정 시각을 씀(세이브 본문에 시각 필드가 없음).
    /// </summary>
    public ServerApi.SaveSummary GetLocalSaveSummary(int slot)
    {
        string path = SavePath(slot);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (data == null)
            {
                return null;
            }
            return new ServerApi.SaveSummary
            {
                slot = slot,
                level = data.level,
                gold = data.gold,
                updatedAt = File.GetLastWriteTime(path).ToString("yyyy-MM-ddTHH:mm:ss")
            };
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GameManager] 로컬 세이브 읽기 실패(slot {slot}): {e.Message}");
            return null;
        }
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

    /// <summary>출격 직전 자동 저장. 격납고에서 나가는 순간 호출됨.</summary>
    public void AutoSaveBeforeLaunch()
    {
        // SaveGame이 아니라 SaveData 직행 — 격납고는 canSave가 아니라 SaveGame이면 거절됨.
        // 함선이 있는 곳에서 떠야 HP·파츠가 실제 값으로 담김.
        SaveData(AutoSaveSlot);
        StageClearedBeforeHangar = false;
    }

    /// <summary>자동 저장이 있으면 다음 씬 로드 후 복원하도록 예약.</summary>
    private void QueueAutoSaveRestore()
    {
        QueueRestore(AutoSaveSlot);
    }

    // 해당 슬롯 파일이 있으면 다음 씬 로드 후 복원 예약. 없으면 아무것도 안 함.
    private void QueueRestore(int slot)
    {
        _restoreOnNextLoad = File.Exists(SavePath(slot));
        if (_restoreOnNextLoad)
        {
            _lastSaveSlot = slot;
        }
    }

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
        // 게임오버에서만 일시정지 불가. 클리어 중엔 보상 줍는 시간이라 메뉴/UI가 정상 동작해야 함.
        if (curState == GAME_STATE.GAME_OVER) return false;

        // 멀티(온라인)에선 시간을 멈추지 않는다 — 남들은 계속 플레이 중이므로.
        // 메뉴는 뜨고(호출자가 표시), 여기선 사운드 감쇠만 한다. 게임 로직 프리즈는 안 함.
        if (IsMultiplayer)
        {
            SoundManager.Instance.SetAllVolumeAtGamePaused(true);
            return true;
        }

        _pauseRequests++;
        if (!IsPaused)
        {
            IsPaused = true;
            ChangeState(GAME_STATE.PAUSED);
            FreezeParticles(); // 폭발/트레일 등 파티클도 정지
            SoundManager.Instance.SetAllVolumeAtGamePaused(true); // 일시정지 중 전체 사운드(BGM+SFX+엔진) 감쇠
        }
        return true;
    }

    /// <summary>일시정지 해제 요청. 모든 요청이 해제되면 게임 재개.</summary>
    public void ResumeGame()
    {
        // 멀티에선 프리즈를 안 걸었으므로 사운드만 원복.
        if (IsMultiplayer)
        {
            SoundManager.Instance.SetAllVolumeAtGamePaused(false);
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
            SoundManager.Instance.SetAllVolumeAtGamePaused(false); // 전체 사운드(BGM+SFX+엔진) 볼륨 원복
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
        StageClearedBeforeHangar = false;
        ChangeState(GAME_STATE.GAME_OVER);
        PoolManager.Instance.DisableAllProjectiles();//현재 투사체 모두 비활성화
        SoundManager.Instance.StopSFXAll();//모든 나고있던 효과음 중지
        LoadScene(SCENE_TYPE.RESULT);
        //기타 필요한 ui연출이나 사운드, 이펙트연출은 추가로 작성필요
    }

    /// <summary>
    /// 사망 후 현재 스테이지를 처음부터 재시작
    /// 게임오버를 풀고 격납고로 돌아감. 출격 직전 자동저장이 있으면 그 시점으로 복원됨.
    /// </summary>
    public void RestartStage()
    {
        IsGameOver = false;
        QueueAutoSaveRestore();
        ChangeState(GAME_STATE.PLAYING);
        // 스테이지를 리로드하지 않고 격납고부터 다시 시작함 — 출격 준비를 고칠 기회를 줘야 함.
        // 나갈 목적지(HangarExitButton.launchSceneName)는 static이라 출격 때 고른 값이 그대로 남아 있음.
        LoadSceneWithLoading(SCENE_TYPE.BASE_HANGAR);
    }

    /// <summary>스테이션 복귀. 출격 직전 자동 저장 시점으로 되돌림.</summary>
    public void ReturnToStation()
    {
        IsGameOver = false;
        QueueAutoSaveRestore();
        ChangeState(GAME_STATE.PLAYING);
        LoadSceneWithLoading(SCENE_TYPE.BASE_STATION);
    }

    /// <summary>
    /// 스테이지 클리어. 보스 처치 또는 마지막 웨이브 소진으로 진입함.
    /// 바로 씬을 넘기지 않고 _stageClearSequenceTime 동안 보상을 주울 시간을 준 뒤 스테이션으로 감.
    /// 클리어 UI는 onStageClear를 구독해서 띄우면 됨.
    /// </summary>
    public void StageClear()
    {
        if (curState == GAME_STATE.STAGE_CLEAR)
        {
            return;
        }
        ChangeState(GAME_STATE.STAGE_CLEAR);
        StageClearedBeforeHangar = true;

        // 킬카운트는 보스 스폰 조건용이라 클리어 후엔 쓸 데가 없음. 비우고 표시도 갱신.
        killCount = 0;
        onObjectiveChanged?.Invoke();
        PublishBattleProgress();

        // 날아가던 투사체는 지우지 않고 사거리만 잘라 각자 끝나게 함(미사일은 폭발).
        PoolManager.Instance.CutProjectileRanges(_stageClearProjectileRange);

        // 새 웨이브 소환 중단 + 남은 적을 정상 사망 처리(연출·드랍 그대로).
        SpawnManager spawner = FindObjectOfType<SpawnManager>();
        if (spawner != null)
        {
            spawner.StopWaves();
        }
        UnitManager.Instance?.KillAllEnemies();

        onStageClear?.Invoke(_stageClearSequenceTime);
        StartCoroutine(StageClearRoutine());
    }

    // 보상을 주울 시간을 준 뒤 결과 화면으로. 대기는 일시정지 안전.
    private IEnumerator StageClearRoutine()
    {
        yield return WaitGameplaySeconds(_stageClearSequenceTime);

        // 저장은 여기서 — 결과 씬은 함선을 안 데려가므로 도착 후엔 체력·파츠를 담을 수 없음.
        SaveData(AutoSaveSlot);

        //LoadSceneWithLoading(SCENE_TYPE.RESULT);
        LoadScene(SCENE_TYPE.RESULT);
    }

	/// <summary>
	/// 적 처치 시 Enemy.Die()에서 호출.
	/// 킬카운트 누적 후 보스 스폰 조건 체크.
	/// </summary>
	public void OnEnemyKilled(GameObject killer, int exp, int gold, bool countsKill = true)
    {
        // 보상은 항상 지급. 킬카운트(보스 스폰 조건)는 세는 대상만.
        GiveRewardToPlayer(killer, exp, gold);

        // 클리어 정리로 죽는 적은 세지 않음 — 여기서 세면 보스 스폰 조건이 다시 충족될 수 있음.
        if (!countsKill || curState == GAME_STATE.STAGE_CLEAR)
        {
            return;
        }
        killCount++;
        PublishBattleProgress();
        onObjectiveChanged?.Invoke();
        CheckBossSpawnCondition();
    }

    /// <summary>
    /// 킬 보상을 '죽인 사람'에게 지급. killer의 Player를 찾아 경험치/골드 지급.
    /// killer가 플레이어가 아니면(환경 사망 등) 지급 없음.
    /// 멀티: 이 함수는 적 소유자(방장) 클라에서 실행됨 — killer가 원격 플레이어면 여기서 직접 지급하지 않고
    /// 그 소유 클라로만 RPC 전달(여기서 원격 사본에 지급하면 방장 화면의 사본만 바뀌고 본인은 못 받음).
    /// 경험치/골드 전부 로컬 스탯이라(InventoryManager도 클라마다 로컬) killer 클라에서만 지급하면 자연스럽게 플레이어별 귀속됨.
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

        PhotonView killerView = player.GetComponent<PhotonView>();
        if (PhotonNetwork.InRoom && killerView != null && !killerView.IsMine)
        {
            killerView.RPC(nameof(Player.RpcReceiveKillReward), killerView.Owner, exp, gold);
            return;
        }
        player.ReceiveKillReward(exp, gold);
    }

    /// <summary>
    /// 적 처치 드랍 아이템을 '죽인 사람'에게 지급. Enemy.DropItem()에서 사망 연출이 끝난 뒤 호출됨.
    /// 월드에 픽업을 스폰하지 않고 획득자 클라로만 보냄 — 네트워크 오브젝트가 생기지 않고,
    /// 획득자가 이미 정해져 있어 "누가 먼저 먹었나" 중재도 필요 없음.
    /// 연출(아이템이 날아오는 자석 효과)과 실제 지급은 받은 쪽 Player가 처리함.
    /// 멀티 분기는 GiveRewardToPlayer와 동일 — killer가 원격이면 그 소유 클라로만 RPC.
    /// </summary>
    /// <param name="killer">마지막으로 때린 오브젝트(Unit의 _lastAttacker)</param>
    /// <param name="dropTypes">드랍할 픽업 프리팹의 풀 타입 목록. 개수만큼 한 번에 넘김(RPC 1회로 끝내려고).</param>
    /// <param name="center">연출이 출발할 중심 위치(적이 죽은 자리)</param>
    /// <param name="spreadRadius">출발 지점을 흩뿌릴 반경. 실제 좌표는 받는 쪽이 계산함(연출이라 동기화 불필요).</param>
    public void GiveItemDropToKiller(GameObject killer, POOL_TYPE[] dropTypes, Vector3 center, float spreadRadius)
    {
        if (killer == null || dropTypes == null || dropTypes.Length == 0)
        {
            return;
        }
        Player player = killer.GetComponentInParent<Player>();
        if (player == null)
        {
            return;   // 플레이어가 죽인 게 아니면 드랍 없음
        }

        // POOL_TYPE은 enum이라 RPC로 못 보냄 → int 배열로 변환해서 전달
        int[] poolTypes = new int[dropTypes.Length];
        for (int i = 0; i < dropTypes.Length; i++)
        {
            poolTypes[i] = (int)dropTypes[i];
        }

        PhotonView killerView = player.GetComponent<PhotonView>();
        if (PhotonNetwork.InRoom && killerView != null && !killerView.IsMine)
        {
            killerView.RPC(nameof(Player.RpcReceiveItemDrops), killerView.Owner, poolTypes, center, spreadRadius);
            return;
        }
        player.ReceiveItemDrops(poolTypes, center, spreadRadius);
    }

    /// <summary>
    /// 보스 처치 시 보스 오브젝트에서 호출.
    /// 킬카운트 누적 + onBossKilled 이벤트 발행.
    /// </summary>
    public void OnBossKilled()
    {
        // 보스 처치 → 현재 씬 BGM으로 페이드 복귀(매핑 있을 때만)
        if (_hasCurrentSceneBGM)
        {
            SoundManager.Instance.ChangeBGMWithFade(_currentSceneBGM, _bossBGMFadeDuration);
        }
        onBossKilled?.Invoke();

        // 보스 처치 = 스테이지 클리어. 각 클라가 RpcBossKilled로 여기 도달하므로 전원이 로컬에서 클리어됨.
        StageClear();
    }

    // 목표 카운터(Total/Destroyed)를 로컬에서 세도 되는지. 룸 안에서는 방장만 셈 —
    // 게스트는 룸 프로퍼티로 받은 값이 진실이라 로컬에서 같이 세면 두 값이 어긋남.
    // 실체 집합(_bossTargets)은 게스트도 유지함: 방장이 되면 그 시점부터 자기 파괴 통지를 세야 하므로.
    private bool HasProgressAuthority
    {
        get
        {
            return !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
        }
    }

    /// <summary>BossSpawnTarget이 활성 시 자기 등록. 보스 스폰 '파괴 목표' 수에 포함.</summary>
    public void RegisterBossTarget(GameObject target)
    {
        if (target == null) return;
        if (!_bossTargets.Add(target))
        {
            return;
        }
        if (!HasProgressAuthority)
        {
            return;
        }
        _bossTargetsTotal++;
        onObjectiveChanged?.Invoke();
        PublishBattleProgress();
    }

    /// <summary>BossSpawnTarget이 파괴(비활성) 시 통지. 남은 목표에서 제거 후 조건 체크.</summary>
    /// 씬 언로드/종료로 인한 비활성은 '파괴'가 아니므로 무시함(안 그러면 씬 나가는 중에 보스 조건이 터짐).
    public void NotifyBossTargetDestroyed(GameObject target)
    {
        if (target == null) return;
        if (_isQuitting || _isSceneUnloading) return;
        if (!_bossTargets.Remove(target))
        {
            return;
        }
        if (!HasProgressAuthority)
        {
            return;
        }
        _bossTargetsDestroyed++;
        onObjectiveChanged?.Invoke();
        PublishBattleProgress();
        CheckBossSpawnCondition();
    }

    // 방장 승계. 진행도 권위가 이쪽으로 넘어오므로 로컬 값을 즉시 룸에 다시 올려 확정함 —
    // 안 올리면 다음 킬/파괴가 일어날 때까지 룸에는 떠난 방장의 마지막 값이 남음.
    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        // 추적 대상이 로컬에 없는데 목표가 남아 있으면 그 목표는 영원히 달성되지 않음 —
        // 조용히 넘기면 보스가 안 나오는 원인을 찾기 어려우므로 드러내 둠.
        int remaining = _bossTargetsTotal - _bossTargetsDestroyed;
        if (remaining > 0 && _bossTargets.Count < remaining)
        {
            Debug.LogWarning($"[GameManager] 방장 승계 — 남은 목표 {remaining}개 중 로컬 추적 가능한 것이 " +
                             $"{_bossTargets.Count}개뿐임. 이 클라이언트에 목표 오브젝트가 생성되지 않았으면 " +
                             $"보스 조건이 충족되지 않음.");
        }

        PublishBattleProgress();
    }

    // 씬 언로드/앱 종료 중인지. 이때 오는 OnDisable은 '파괴'가 아니라 정리 과정이므로 목표 달성으로 세면 안 됨.
    private bool _isQuitting;
    private bool _isSceneUnloading;

    private void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    // =====================================================================
    // 내부 메서드
    // =====================================================================
    
    // 게임플레이가 도는 씬인지. 메뉴/로딩/게임오버만 제외하고 나머지는 플레이 대상으로 봄 —
    // 제외 목록 방식이라 스테이지를 새로 추가해도 여기 손댈 필요가 없고, SCENE_TYPE에 없는
    // 작업씬(UNKNOWN)도 플레이 테스트용이므로 PLAYING으로 잡힘.
    private bool IsGameplayScene(SCENE_TYPE scene)
    {
        switch (scene)
        {
            case SCENE_TYPE.LOGIN:
            case SCENE_TYPE.MAIN:
            case SCENE_TYPE.MULTIPLAYER:
            case SCENE_TYPE.MAP_SELECT:
            case SCENE_TYPE.LOADING_SEQUENCE:
            case SCENE_TYPE.RESULT:
                return false;
            default:
                return true;
        }
    }

    // 로딩 씬을 거치는 전환은 실제 목적지가 LoadingManager.NextScene에 들어 있음.
    private static string ResolveDestination(string sceneName)
    {
        if (sceneName == SCENE_TYPE.LOADING_SEQUENCE.ToString())
        {
            return LoadingManager.NextScene;
        }
        return sceneName;
    }

    // =====================================================================
    // 씬 설정표 조회
    // 씬별 속성의 단일 출처. 여기엔 씬 이름이 하나도 안 들어감 —
    // 속성 규칙은 카테고리(SCENE_CATEGORY)에만 걸려 있어서 씬이 늘어도 코드를 안 고침.
    // =====================================================================
    private void BuildSceneSettingsMap()
    {
        _sceneSettingsMap.Clear();
        foreach (SceneSettings entry in _sceneSettings)
        {
            // 카테고리로 속성을 풀어서 넣어둠. 조회할 때마다 풀지 않고 여기서 한 번만 처리함.
            _sceneSettingsMap[entry.scene.ToString()] = Resolve(entry);
        }

        // 표에 빠진 SCENE_TYPE은 조용히 전부 off가 되므로 알려줌 — 저장이 막히거나
        // 함선이 사라지는 식으로 뒤늦게 드러나는 게 최악임.
        HashSet<string> buildScenes = CollectBuildSceneNames();

        List<string> missing = new List<string>();
        List<string> noSceneFile = new List<string>();
        foreach (SCENE_TYPE type in System.Enum.GetValues(typeof(SCENE_TYPE)))
        {
            if (type == SCENE_TYPE.UNKNOWN || type == SCENE_TYPE.LOADING_SEQUENCE
                || _sceneSettingsMap.ContainsKey(type.ToString()))
            {
                continue;
            }
            if (buildScenes.Contains(type.ToString()))
            {
                missing.Add(type.ToString());
            }
            else
            {
                noSceneFile.Add(type.ToString());
            }
        }

        if (missing.Count > 0)
        {
            Debug.LogWarning($"[GameManager] 씬 설정표에 없는 씬: {string.Join(", ", missing)}\n" +
                             "속성이 전부 off로 취급됨. Hub > 새 씬 만들기 > 씬 설정에서 채울 것.");
        }

        if (noSceneFile.Count > 0)
        {
            Debug.Log($"[GameManager] enum에만 있고 실제 씬은 없음: {string.Join(", ", noSceneFile)}\n" +
                      "씬을 만들거나 SCENE_TYPE에서 뺄 것. 설정표에는 넣지 않아도 됨.");
        }
    }

    // 빌드 세팅에 등록된 씬 이름. 런타임이라 AssetDatabase를 못 쓰므로 빌드 목록으로 대신 판정함.
    private static HashSet<string> CollectBuildSceneNames()
    {
        HashSet<string> names = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (!string.IsNullOrEmpty(path))
            {
                names.Add(Path.GetFileNameWithoutExtension(path));
            }
        }
        return names;
    }

    // 카테고리 → 속성. 씬 속성 규칙의 유일한 코드 출처.
    // overrideFlags가 켜진 행은 사람이 찍은 체크박스를 그대로 씀.
    public static SceneSettings Resolve(SceneSettings entry)
    {
        if (entry.overrideFlags)
        {
            return entry;
        }

        SceneSettings resolved = entry;
        resolved.keepsPlayerShip = false;
        resolved.canSave = false;
        resolved.isBattleScene = false;
        resolved.isStationScene = false;
        resolved.shipHidden = false;
        resolved.otherShipsHidden = false;
        // 조종은 전투씬에서만
        resolved.shipControlDisabled = entry.category != SCENE_CATEGORY.BATTLE;

        switch (entry.category)
        {
            case SCENE_CATEGORY.STATION:
                resolved.isStationScene = true;
                resolved.canSave = true;
                break;
            case SCENE_CATEGORY.BATTLE:
                resolved.isBattleScene = true;
                resolved.keepsPlayerShip = true;
                break;
            case SCENE_CATEGORY.HANGAR:
                resolved.keepsPlayerShip = true;
                // 멀티에서 같은 격납고에 여럿이 들어와도 각자 자기 기체만 보게 함.
                resolved.otherShipsHidden = true;
                break;
            case SCENE_CATEGORY.TRANSIT:
                resolved.keepsPlayerShip = true;
                resolved.shipHidden = true;
                break;
        }
        return resolved;
    }

    private SceneSettings SettingsOf(SCENE_TYPE type)
    {
        return SettingsOf(type.ToString());
    }

    private SceneSettings SettingsOf(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName) && _sceneSettingsMap.TryGetValue(sceneName, out SceneSettings settings))
        {
            return settings;
        }
        return new SceneSettings();   // 표에도 SCENE_TYPE에도 없는 작업씬 — 전부 false
    }

    // 그 씬으로 갈 때 내 함선을 남겨둘지. 출격 흐름(격납고→맵선택/대기실→스테이지) 안이면 남김.
    private bool KeepsPlayerShip(string sceneName)
    {
        return SettingsOf(sceneName).keepsPlayerShip;
    }

    // 씬 이름 → SCENE_TYPE. 이름이 정확히 일치할 때만 인정하고, 표에 없는 작업씬은 UNKNOWN을 돌려줌.
    // SCENE_TYPE 이름 = 실제 씬 파일 이름 규칙에 기대는 건 씬 설정표 조회와 동일함.
    private SCENE_TYPE ParseSceneType(string sceneName)
    {
        // 대소문자 무시 — SceneManager.LoadScene(string)이 대소문자를 안 가리므로 씬 파일명이
        // 대소문자가 enum 표기와 달라도 정상 로드됨. 여기서만 구분하면
        // 로드는 되는데 curSceneType이 UNKNOWN으로 잡히는 불일치가 생김.
        if (System.Enum.TryParse(sceneName, true, out SCENE_TYPE parsed) && System.Enum.IsDefined(typeof(SCENE_TYPE), parsed))
        {
            return parsed;
        }
        return SCENE_TYPE.UNKNOWN;
    }

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
        PublishBattleProgress();
    }

    // =====================================================================
    // 멀티 — 전투 진행도(killCount) 공유
    // =====================================================================
    // 적 사망 처리(Enemy.Die)는 소유자(방장)에서만 돌아서 killCount도 방장만 오름.
    // 그대로 두면 게스트 HUD가 0에 멈추고, 보스 조건/BGM도 게스트에선 영영 안 돌고,
    // 방장이 나가면 진행도가 통째로 날아감.
    // → 진행도는 특정 플레이어가 아니라 '방'에 속한 값이라 Room Custom Property에 올림.
    //   방장만 기록하고 나머지는 받아서 반영함. 방장이 바뀌어도 값이 남음.
    //
    // ⚠ 카운터를 로컬에서 올리는 것도 방장만 함(HasProgressAuthority). 게스트가 같이 세면
    //   '받은 값'과 '자기가 센 값'이 섞여 어긋나고, 그 상태로 방장이 되면 어긋난 값이 권위가 됨.
    //   방장 승계 시점에는 OnMasterClientSwitched가 로컬 값을 즉시 룸에 다시 올려 확정함.
    private const string KillCountPropertyKey = "StageKillCount";
    // 파괴 목표 진행도도 '방'에 속한 값 — 목표 등록/파괴는 방장 쪽에서만 일어나므로 게스트는 받아서 반영함.
    private const string BossTargetTotalPropertyKey = "StageBossTargetTotal";
    private const string BossTargetDonePropertyKey = "StageBossTargetDone";
    // 보스가 이미 나왔는지도 '방'에 속한 값 — 이게 없으면 방장이 바뀐 뒤 새 방장의 로컬 플래그가
    // 꺼져 있어서 조건이 다시 성립할 때 보스가 두 번 나올 수 있음.
    private const string BossSpawnedPropertyKey = "StageBossSpawned";

    // 방장만 기록(권위 일원화). 싱글/오프라인은 룸이 없어 그냥 무시됨.
    private void PublishBattleProgress()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
        {
            return;
        }
        PhotonHashtable progress = new PhotonHashtable
        {
            { KillCountPropertyKey, killCount },
            { BossTargetTotalPropertyKey, _bossTargetsTotal },
            { BossTargetDonePropertyKey, _bossTargetsDestroyed },
            { BossSpawnedPropertyKey, bossSpawned }
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(progress);
    }

    public override void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged)
    {
        if (propertiesThatChanged == null)
        {
            return;
        }
        ApplySyncedProgress(propertiesThatChanged);
    }

    // 방장이 올린 진행도(킬/파괴목표)를 게스트가 반영. 바뀐 항목만 골라 적용 후 보스 조건 재검사.
    private void ApplySyncedProgress(PhotonHashtable props)
    {
        // 방장은 자기가 올린 값의 권위자라 되돌려 받을 필요 없음(자기 값이 원본).
        if (PhotonNetwork.IsMasterClient)
        {
            return;
        }

        bool changed = false;

        if (props.TryGetValue(KillCountPropertyKey, out object killValue) && killValue is int syncedKillCount
            && killCount != syncedKillCount)
        {
            killCount = syncedKillCount;
            changed = true;
        }
        if (props.TryGetValue(BossTargetTotalPropertyKey, out object totalValue) && totalValue is int syncedTotal
            && _bossTargetsTotal != syncedTotal)
        {
            _bossTargetsTotal = syncedTotal;
            changed = true;
        }
        if (props.TryGetValue(BossTargetDonePropertyKey, out object doneValue) && doneValue is int syncedDone
            && _bossTargetsDestroyed != syncedDone)
        {
            _bossTargetsDestroyed = syncedDone;
            changed = true;
        }

        if (changed)
        {
            onObjectiveChanged?.Invoke();
            // 게스트도 보스 조건을 돌려야 보스 BGM/연출을 같이 받음. 실제 보스 스폰은 SpawnManager가 방장만 하도록 막아둠.
            CheckBossSpawnCondition();
        }

        // 보스 스폰 여부는 '미스폰 → 스폰' 한 방향으로만 받음.
        // 조건 검사보다 뒤에 둬야 게스트가 자기 조건으로 BGM/연출을 먼저 받고, 그 뒤에 중복 스폰만 막힘.
        if (props.TryGetValue(BossSpawnedPropertyKey, out object spawnedValue) && spawnedValue is bool syncedSpawned
            && syncedSpawned && !bossSpawned)
        {
            bossSpawned = true;
        }
    }

    // 방 입장 시점의 현재 진행도를 한 번 읽어옴(도중 합류 대비 — 프로퍼티 변경 콜백은 '변할 때'만 오므로).
    public override void OnJoinedRoom()
    {
        if (PhotonNetwork.CurrentRoom != null)
        {
            ApplySyncedProgress(PhotonNetwork.CurrentRoom.CustomProperties);
        }
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
        // 슬롯 안 넘기면 기본값 0으로만 감.
        if (ServerApi.Instance != null && ServerApi.Instance.IsLoggedIn)
        {
            ServerApi.Instance.StartCoroutine(ServerApi.Instance.SaveCo(data,
                () => Debug.Log($"[GameManager] 서버 저장 성공 (slot {saveSlot})"),
                err => Debug.LogWarning($"[GameManager] 서버 저장 실패(로컬은 저장됨): {err}"),
                saveSlot));
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
            // 슬롯 안 넘기면 기본값 0만 읽음.
            yield return ServerApi.Instance.LoadCo(
                data =>
                {
                    if (data != null)
                    {
                        ApplySaveData(data);
                        serverOk = true;
                        Debug.Log($"[GameManager] 서버 로드 완료 (slot {saveSlot})");
                    }
                },
                err => Debug.LogWarning($"[GameManager] 서버 로드 실패, 로컬 시도: {err}"),
                saveSlot);

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

        // 함선이 있으면 최신 상태를 프로필로 끌어온 뒤, 저장은 프로필에서만 함.
        // 함선이 없는 씬에서 저장해도 파츠·스킬·HP가 빠지지 않게 하려는 것.
        PlayerProfile.CaptureFrom(playerRef);
        PlayerProfile.WriteTo(data);

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

        // ※ 여기서 playerRef를 다시 훑어 담지 말 것.
        // 위 CaptureFrom + WriteTo가 레벨·체력·파츠·미사일·스킬·퀵슬롯을 전부 담는다.
        // 예전엔 아래에 함선에서 직접 담는 코드가 한 벌 더 있었는데, 그게 WriteTo 결과를
        // 통째로 덮어쓰면서 파츠 HP(curPartHp)만 안 넣어 전 파츠가 0으로 저장되고 있었다.
        // (0 = 파괴로 복원되어 엔진 스탯이 사라지고 최대 연료가 0이 되던 원인)
        return data;
    }

    /// <summary>불러온 SaveData를 Player / Loadout 등에 적용.</summary>
    private void ApplySaveData(SaveData data)
    {
        // 함선 종속 데이터는 프로필이 먼저 받고, 함선이 있으면 아래에서 씌움.
        PlayerProfile.InitFromSave(data, itemDatabase);

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

        // 함선이 이미 있으면(같은 씬에서 불러오기) 프로필을 바로 씌움.
        // 함선이 아직 없으면 스폰 직후 Player가 스스로 PlayerProfile.ApplyTo를 부름.
        if (playerRef != null)
        {
            PlayerProfile.ApplyTo(playerRef, itemDatabase);
        }
    }

    /// <summary>새 게임 시작 시 데이터 전체 초기화.</summary>
    private void ClearData()
    {
        PlayerProfile.InitFromStartData(_gameStartData);

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.gold = _gameStartData != null ? _gameStartData.startGold : 0;
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
        GrantStartItems();
        ResetBattleData();
    }

    // 비우기가 끝난 뒤에 지급해야 함 — 순서가 바뀌면 같이 지워짐.
    // 여기서 주는 건 함선과 무관한 것(골드/가방)뿐임.
    // 파츠는 UnitParts가, 스킬은 SkillSystem이 스폰 시 GameStartData를 직접 읽음 —
    // 새 게임은 함선이 없는 씬(로비)에서 시작해서 여기서 주면 통째로 누락됨.
    private void GrantStartItems()
    {
        if (_gameStartData == null || InventoryManager.Instance == null)
        {
            return;
        }

        foreach (GameStartData.StartItem entry in _gameStartData.startItems)
        {
            if (entry == null || entry.item == null || entry.count <= 0)
            {
                continue;
            }
            InventoryManager.Instance.AddItem(entry.item, entry.count);
        }
    }
}