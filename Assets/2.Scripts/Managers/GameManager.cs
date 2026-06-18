using System.Collections;
using System.Collections.Generic;
using System.IO;
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
//   OnEnemyKilled()     : 적 사망 시 Enemy.Die()에서 호출
//   OnObjectDestroyed() : 파괴 오브젝트 파괴 시 호출
//   onBossSpawn         : 보스 스폰 조건 달성 시 발행 이벤트
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
                    Debug.LogError("[GameManager] 씬에 GameManager 없음! 하이어라키에 추가 필요");
            }
            return instance;
        }
    }

    // =====================================================================
    // 씬-BGM 매핑 (씬 추가 시 여기에 한 줄만 추가)
    // =====================================================================
    private static readonly Dictionary<string, SOUND_TYPE> _sceneBGMMap = new Dictionary<string, SOUND_TYPE>
    {
        { "MAIN",             SOUND_TYPE.BGM_MAIN      },
        { "STATION",          SOUND_TYPE.BGM_STATION   },
        { "STAGE1",           SOUND_TYPE.BGM_STAGE1    },
        { "GAME_OVER",        SOUND_TYPE.BGM_GAMEOVER  },
        { "1F",               SOUND_TYPE.BGM_1F        },
        { "B2",               SOUND_TYPE.BGM_B2        },
        // LOADING_SEQUENCE, MAP_SELECT 등도 추가해야함.
    };

    // =====================================================================
    // 게임 상태
    // =====================================================================
    public GAME_STATE curState;

    // 상태 변화 시 UI에서 구독 (패널 전환 등)
    public GameStateHandler onGameStateChanged;

    // Time.timeScale 대신 플래그로 제어
    // Player, Enemy 등 게임 로직에서 이 값을 체크해 스스로 멈춤
    // UI / 음악 / 연출은 영향 없음
    public bool IsPaused   { get; private set; }
    public bool IsGameOver { get; private set; }

    // =====================================================================
    // Player 레퍼런스 (씬 로드 후 자동 캐싱)
    // =====================================================================
    public Player playerRef;

    // =====================================================================
    // 보스 스폰 조건
    // =====================================================================
    [Header("━━━━━━ 보스 스폰 조건 ━━━━━━")]
    [Tooltip("이 수만큼 적을 처치하면 보스 스폰 (0이면 킬카운트 조건 미사용)")]
    public int killCountToSpawnBoss = 20;

    [Tooltip("이 수만큼 오브젝트를 파괴하면 보스 스폰 (0이면 파괴 조건 미사용)")]
    public int destroyCountToSpawnBoss = 0;

    [HideInInspector] public int  killCount;
    [HideInInspector] public int  destroyedObjectCount;
    [HideInInspector] public bool bossSpawned;

    // 보스 스폰 조건 달성 시 발행 (SpawnManager 등이 구독)
    public BossSpawnHandler onBossSpawn;

    // =====================================================================
    // 저장 경로
    // =====================================================================
    private string SavePath(int slot) =>
        Path.Combine(Application.persistentDataPath, $"save{slot}.json");

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

        // 전투 씬 진입 시 킬카운트 초기화
        if (scene.name == SCENE_TYPE.STAGE1.ToString())
        {
            ResetBattleData();
        }
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
        // 전환 전 정리
        Time.timeScale = 1f;
        PoolManager.Instance.DisableAllProjectiles();
        SoundManager.Instance.StopSFXAll();
        VFXManager.Instance.ReturnAll();

        // 필요 시 페이드아웃 연출 추가
        // yield return StartCoroutine(FadeOut());

        yield return null;
        SceneManager.LoadScene(sceneName);
    }

    private void PlaySceneBGM(string sceneName)
    {
        if (_sceneBGMMap.TryGetValue(sceneName, out SOUND_TYPE bgm))
            SoundManager.Instance.PlayBGM(bgm);
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
        LoadData(saveSlotNum);
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

    /// <summary>
    /// 일시정지. PLAYING 상태에서만 동작.
    /// Time.timeScale을 건드리지 않으므로 UI / 음악 / 연출은 그대로 동작.
    /// Player, Enemy 등 게임 로직은 IsPaused를 체크해서 스스로 멈춰야 함.
    /// </summary>
    public void PauseGame()
    {
        if (curState != GAME_STATE.PLAYING) return;
        IsPaused = true;
        ChangeState(GAME_STATE.PAUSED);
    }

    /// <summary>일시정지 해제.</summary>
    public void ResumeGame()
    {
        if (curState != GAME_STATE.PAUSED) return;
        IsPaused = false;
        ChangeState(GAME_STATE.PLAYING);
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

    /// <summary>스테이지 클리어 조건 달성 시 호출.</summary>
    public void GameClear()
    {
        if (curState == GAME_STATE.CLEAR)
        {
            return;
        }
        ChangeState(GAME_STATE.CLEAR);
        PoolManager.Instance.DisableAllProjectiles();
    }

	/// <summary>
	/// 적 처치 시 Enemy.Die()에서 호출.
	/// 킬카운트 누적 후 보스 스폰 조건 체크.
	/// </summary>
	public void OnEnemyKilled()
    {
        killCount++;
        CheckBossSpawnCondition();
    }

    /// <summary>
    /// 파괴 가능 오브젝트 파괴 시 해당 오브젝트에서 호출.
    /// 파괴 카운트 누적 후 보스 스폰 조건 체크.
    /// </summary>
    public void OnObjectDestroyed()
    {
        destroyedObjectCount++;
        CheckBossSpawnCondition();
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

        bool killCondition    = killCountToSpawnBoss    > 0 && killCount             >= killCountToSpawnBoss;
        bool destroyCondition = destroyCountToSpawnBoss > 0 && destroyedObjectCount  >= destroyCountToSpawnBoss;

        if (killCondition || destroyCondition)
        {
            bossSpawned = true;
            Debug.Log($"[GameManager] 보스 스폰 조건 달성 (킬: {killCount}, 파괴: {destroyedObjectCount})");
            onBossSpawn?.Invoke();
        }
    }

    /// <summary>전투 씬 진입 시 전투 관련 카운터 초기화.</summary>
    private void ResetBattleData()
    {
        killCount            = 0;
        destroyedObjectCount = 0;
        bossSpawned          = false;
    }

    // =====================================================================
    // 세이브 / 로드
    // =====================================================================

    /// <summary>Player 등에서 현재 상태를 수집해 JSON 파일로 저장.</summary>
    private void SaveData(int saveSlot)
    {
        SaveData data = CollectSaveData();
        string json  = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(SavePath(saveSlot), json);
        Debug.Log($"[GameManager] 저장 완료: {SavePath(saveSlot)}");
    }

    /// <summary>JSON 파일에서 데이터를 읽어 Player 등에 분배.</summary>
    private void LoadData(int saveSlot)
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
        Debug.Log($"[GameManager] 로드 완료: {path}");
    }

    /// <summary>Player / Loadout 등에서 저장할 데이터 수집.</summary>
    private SaveData CollectSaveData()
    {
        SaveData data = new SaveData();
        data.gold = InventoryManager.Instance != null ? InventoryManager.Instance.gold : 0;

        if (playerRef != null)
        {
            data.level = playerRef.level;
            data.exp = playerRef.exp;
            data.expToNextLevel = playerRef.expToNextLevel;
            data.curHp = playerRef.curHpRemaining;
            data.curShield = playerRef.curShieldRemaining;
            data.curArmor = playerRef.curArmorRemaining;
            data.curBoost = playerRef.curBoostRemaining;

            // 미사일 슬롯 복사
            if (playerRef.weaponSystem.missileSlots != null)
            {
                data.missileSlots = new MissileSlot[playerRef.weaponSystem.missileSlots.Count];
                for (int i = 0; i < playerRef.weaponSystem.missileSlots.Count; i++)
                {
                    data.missileSlots[i] = new MissileSlot
                    {
                        type    = playerRef.weaponSystem.missileSlots[i].type,
                        curAmmo = playerRef.weaponSystem.missileSlots[i].curAmmo,
                        maxAmmo = playerRef.weaponSystem.missileSlots[i].maxAmmo
                    };
                }
            }

            // TODO: PlayerLoadout 구현 후 착용 장비 / 소모품 / 인벤토리 추가
        }
        return data;
    }

    /// <summary>불러온 SaveData를 Player / Loadout 등에 적용.</summary>
    private void ApplySaveData(SaveData data)
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.gold = data.gold;
        }

        if (playerRef != null)
        {
            playerRef.level             = data.level;
            playerRef.exp               = data.exp;
            playerRef.expToNextLevel    = data.expToNextLevel;
            playerRef.curHpRemaining    = data.curHp;
            playerRef.curShieldRemaining = data.curShield;
            playerRef.curArmorRemaining  = data.curArmor;
            playerRef.curBoostRemaining  = data.curBoost;

            // 미사일 슬롯 복원
            if (data.missileSlots != null)
            {
                playerRef.weaponSystem.missileSlots = new List<MissileSlot>();
                for (int i = 0; i < data.missileSlots.Length; i++)
                {
                    playerRef.weaponSystem.missileSlots.Add(new MissileSlot
                    {
                        type    = data.missileSlots[i].type,
                        curAmmo = data.missileSlots[i].curAmmo,
                        maxAmmo = data.missileSlots[i].maxAmmo
                    });
                }
            }

            // TODO: PlayerLoadout 구현 후 착용 장비 / 소모품 / 인벤토리 적용
        }
    }

    /// <summary>새 게임 시작 시 데이터 전체 초기화.</summary>
    private void ClearData()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.gold = 0;
        }
        ResetBattleData();
    }
}