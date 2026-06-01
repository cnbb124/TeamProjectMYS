using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// =====================================================================
// GameManager
//
// ??• :
//   1. ê²Œì„ ?íƒœ(FSM) ê´€ë¦?
//   2. ???„í™˜ (?•ë¦¬ ??ë¡œë“œ)
//   3. ê³¨ë“œ / ?¬ì¹´?´íŠ¸ / ë³´ìŠ¤ ?¤í° ì¡°ê±´ ê´€ë¦?
//   4. ?€??/ ë¶ˆëŸ¬?¤ê¸° (STATION ?¬ì—?œë§Œ ?€??ê°€??
//   5. Player ?ˆí¼?°ìŠ¤ ìºì‹± (??ë¡œë“œ ???ë™ ?ìƒ‰)
//
// ???ë¦„:
//   STATION ??LOADING_SEQUENCE ??MAP_SELECT ??STAGE1
//   ?„íˆ¬ ì¢…ë£Œ ??STATION ë³µê?
//
// ?€???°ì´?? SaveData.cs ì°¸ê³ 
// =====================================================================
public class GameManager : MonoBehaviour
{
    // =====================================================================
    // ?±ê???
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
                    Debug.LogError("[GameManager] ?¬ì— GameManager ?†ìŒ! ?˜ì´?´ë¼?¤ì— ì¶”ê? ?„ìš”");
            }
            return instance;
        }
    }

    // =====================================================================
    // ??BGM ë§¤í•‘ (??ì¶”ê? ???¬ê¸°????ì¤„ë§Œ ì¶”ê?)
    // =====================================================================
    private static readonly Dictionary<string, SOUND_TYPE> _sceneBGMMap = new Dictionary<string, SOUND_TYPE>
    {
        { "MAIN",             SOUND_TYPE.BGM_MAIN      },
        { "STATION",          SOUND_TYPE.BGM_STATION   },
        { "STAGE1",           SOUND_TYPE.BGM_STAGE1    },
        { "GAME_OVER",        SOUND_TYPE.BGM_GAMEOVER  },
        { "1F",               SOUND_TYPE.BGM_1F        },
        { "B2",               SOUND_TYPE.BGM_B2        },
        // LOADING_SEQUENCE, MAP_SELECT ?±ë„ ì¶”ê??´ì•¼??
    };

    // =====================================================================
    // ê²Œì„ ?íƒœ
    // =====================================================================
    public GAME_STATE curState;

    // ?íƒœ ë³€????UI?ì„œ êµ¬ë… (?¨ë„ ?„í™˜ ??
    public System.Action<GAME_STATE> OnGameStateChanged;

    // Time.timeScale ?€???Œë˜ê·¸ë¡œ ?œì–´
    // Player, Enemy ??ê²Œì„ ë¡œì§?ì„œ ??ê°’ì„ ì²´í¬???¤ìŠ¤ë¡?ë©ˆì¶¤
    // UI / ?Œì•… / ?°ì¶œ?€ ?í–¥ ?†ìŒ
    public bool IsPaused   { get; private set; }
    public bool IsGameOver { get; private set; }

    // =====================================================================
    // Player ?ˆí¼?°ìŠ¤ (??ë¡œë“œ ???ë™ ìºì‹±)
    // =====================================================================
    public Player playerRef;

    // =====================================================================
    // ?¬í™”
    // =====================================================================
    public int gold;

    // =====================================================================
    // ë³´ìŠ¤ ?¤í° ì¡°ê±´
    // =====================================================================
    [Header("?â”?â”?â” ë³´ìŠ¤ ?¤í° ì¡°ê±´ ?â”?â”?â”")]
    [Tooltip("???˜ë§Œ???ì„ ì²˜ì¹˜?˜ë©´ ë³´ìŠ¤ ?¤í° (0?´ë©´ ?¬ì¹´?´íŠ¸ ì¡°ê±´ ë¯¸ì‚¬??")]
    public int killCountToSpawnBoss = 20;

    [Tooltip("???˜ë§Œ???¤ë¸Œ?íŠ¸ë¥??Œê´´?˜ë©´ ë³´ìŠ¤ ?¤í° (0?´ë©´ ?Œê´´ ì¡°ê±´ ë¯¸ì‚¬??")]
    public int destroyCountToSpawnBoss = 0;

    [HideInInspector] public int  killCount;
    [HideInInspector] public int  destroyedObjectCount;
    [HideInInspector] public bool bossSpawned;

    // ë³´ìŠ¤ ?¤í° ì¡°ê±´ ?¬ì„± ??ë°œí–‰ (SpawnManager ?±ì´ êµ¬ë…)
    public System.Action OnBossSpawn;

    // =====================================================================
    // ?€??ê²½ë¡œ
    // =====================================================================
    private string SavePath(int slot) =>
        Path.Combine(Application.persistentDataPath, $"save{slot}.json");

    // =====================================================================
    // ?€??ê°€???¬ë? (STATION ?¬ì—?œë§Œ true)
    // =====================================================================
    public bool CanSave =>
        SceneManager.GetActiveScene().name == SCENE_TYPE.STATION.ToString();

    // =====================================================================
    // ì´ˆê¸°??
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
            Debug.LogWarning("[GameManager] ì¤‘ë³µ ê°ì?. ?Œê´´ ??ê¸°ì¡´ ? ì?");
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ??ë¡œë“œ ?„ë£Œ ???ë™ ?¸ì¶œ
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Player ?ˆí¼?°ìŠ¤ ê°±ì‹ 
        playerRef = FindObjectOfType<Player>();

        // BGM ?¬ìƒ
        PlaySceneBGM(scene.name);

        // ?„íˆ¬ ??ì§„ì… ???¬ì¹´?´íŠ¸ ì´ˆê¸°??
        if (scene.name == SCENE_TYPE.STAGE1.ToString())
        {
            ResetBattleData();
        }
    }

    // =====================================================================
    // ???„í™˜
    // ë°˜ë“œ????ë©”ì„œ?œë? ?µí•´ ?¬ì„ ?„í™˜??ê²?(ì§ì ‘ SceneManager ?¸ì¶œ ê¸ˆì?)
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
        // ?„í™˜ ???•ë¦¬
        Time.timeScale = 1f;
        PoolManager.Instance.DisableAllProjectiles();
        SoundManager.Instance.StopSFXAll();

        // ?„ìš” ???˜ì´?œì•„???°ì¶œ ì¶”ê?
        // yield return StartCoroutine(FadeOut());

        yield return null;
        SceneManager.LoadScene(sceneName);
    }

    private void PlaySceneBGM(string sceneName)
    {
        if (_sceneBGMMap.TryGetValue(sceneName, out SOUND_TYPE bgm))
            SoundManager.Instance.PlayBGM(bgm);
        // ë§¤í•‘ ?†ëŠ” ??ë¡œë”©, ë§µì„ ?????€ BGM ? ì? or ì¤‘ì? ? íƒ
        // SoundManager.Instance.StopBGM(); // ì¤‘ì? ?í•  ??ì£¼ì„ ?´ì œ

        // switch ë°©ì‹ ë©”ëª¨ (Dictionary ë°©ì‹?¼ë¡œ êµì²´?? ?„ìš” ???„ë˜ ë³µì›)
        //switch (sceneName)
        //{
        //    case "MAIN":      SoundManager.Instance.PlayBGM(SOUND_TYPE.BGM_MAIN);     break;
        //    case "STAGE1":    SoundManager.Instance.PlayBGM(SOUND_TYPE.BGM_STAGE1);   break;
        //    case "STATION":   SoundManager.Instance.PlayBGM(SOUND_TYPE.BGM_STATION);  break;
        //    case "GAME_OVER": SoundManager.Instance.PlayBGM(SOUND_TYPE.BGM_GAMEOVER); break;
        //    default:          /* BGM ? ì? ?ëŠ” StopBGM() */                           break;
        //}
    }

    // =====================================================================
    // UI?ì„œ ?¸ì¶œ?˜ëŠ” ê³µê°œ ë©”ì„œ??
    // =====================================================================

    /// <summary>??ê²Œì„ ?œì‘. ?°ì´??ì´ˆê¸°????ë¡œë”© ?œí€€?¤ë¡œ ?´ë™.</summary>
    public void NewGame()
    {
        ClearData();
        ChangeState(GAME_STATE.PLAYING);
        LoadScene(SCENE_TYPE.LOADING_SEQUENCE);
    }

    /// <summary>?€?¥ëœ ê²Œì„ ë¶ˆëŸ¬?¤ê¸°. ?¸ì´ë¸??¬ë¡¯ ë²ˆí˜¸ë¡??¸ì¶œ.</summary>
    public void LoadGame(int saveSlotNum)
    {
        LoadData(saveSlotNum);
        ChangeState(GAME_STATE.PLAYING);
        LoadScene(SCENE_TYPE.LOADING_SEQUENCE);
    }

    //=================================
    // ?€??ë¡œë“œ ì²˜ë¦¬ ?ë¦„ ë©”ëª¨
    // [?€???ë¦„]
    //   GameManager.SaveGame()
    //     ??Player?ì„œ ?„ì¬ HP/?¤ë“œ ?½ì–´??
    //     ??PlayerLoadout?ì„œ ?¥ë¹„/?„ì•½ ?½ì–´??
    //     ??Inventory?ì„œ ?„ì´??ëª©ë¡ ?½ì–´?€??
    //     ???˜ë‚˜??SaveDataë¡??¨í‚¹ ??JSON ?Œì¼ ?€??
    //
    // [ë¡œë“œ ?ë¦„]
    // GameManager.LoadGame()
    //     ??JSON ?Œì¼ ?½ì–´ SaveDataë¡??Œì‹±
    //     ??Player??HP/?¤ë“œ ??ë³µì›
    //     ??PlayerLoadout???¥ë¹„/?„ì•½ ë³µì›
    //     ??Inventory???„ì´??ëª©ë¡ ë³µì›
    //=================================

    /// <summary>?„ì¬ ê²Œì„ ?€?? STATION ?¬ì—?œë§Œ ê°€??</summary>
    public void SaveGame(int saveSlotNum)
    {
        if (!CanSave)
        {
            Debug.LogWarning("[GameManager] ?€?¥ì? ë§ˆì„(STATION)?ì„œë§?ê°€?¥í•©?ˆë‹¤.");
            return;
        }
        SaveData(saveSlotNum);
    }

    /// <summary>
    /// ?¼ì‹œ?•ì?. PLAYING ?íƒœ?ì„œë§??™ì‘.
    /// Time.timeScale??ê±´ë“œë¦¬ì? ?Šìœ¼ë¯€ë¡?UI / ?Œì•… / ?°ì¶œ?€ ê·¸ë?ë¡??™ì‘.
    /// Player, Enemy ??ê²Œì„ ë¡œì§?€ IsPausedë¥?ì²´í¬?´ì„œ ?¤ìŠ¤ë¡?ë©ˆì¶°????
    /// </summary>
    public void PauseGame()
    {
        if (curState != GAME_STATE.PLAYING) return;
        IsPaused = true;
        ChangeState(GAME_STATE.PAUSED);
    }

    /// <summary>?¼ì‹œ?•ì? ?´ì œ.</summary>
    public void ResumeGame()
    {
        if (curState != GAME_STATE.PAUSED) return;
        IsPaused = false;
        ChangeState(GAME_STATE.PLAYING);
    }

    // =====================================================================
    // ê²Œì„ ?´ë??ì„œ ?¸ì¶œ?˜ëŠ” ë©”ì„œ??
    // =====================================================================

    /// <summary>
    /// ?Œë ˆ?´ì–´ ?¬ë§ ??Player.Die()?ì„œ ?¸ì¶œ.
    /// Time.timeScale ê±´ë“œë¦¬ì? ?ŠìŒ - ì£½ìŒ ?°ì¶œ(??°œ ?????¬ìƒ?˜ì–´???˜ë?ë¡?
    /// UI / ?Œì•…?€ OnGameStateChanged ?´ë²¤?¸ë¡œ ì²˜ë¦¬.
    /// </summary>
    public void GameOver()
    {
        if (curState == GAME_STATE.GAME_OVER)
        {
            return;
        }
        IsGameOver = true;
        ChangeState(GAME_STATE.GAME_OVER);
        PoolManager.Instance.DisableAllProjectiles();//?„ì¬ ?¬ì‚¬ì²?ëª¨ë‘ ë¹„í™œ?±í™”
        SoundManager.Instance.StopSFXAll();//ëª¨ë“  ?˜ê³ ?ˆë˜ ?¨ê³¼?Œì¤‘ì§€
        //ê¸°í? ?„ìš”??ui?°ì¶œ?´ë‚˜ ?¬ìš´?? ?´í™?¸ì—°ì¶œì? ì¶”ê?ë¡??‘ì„±?„ìš”
    }

    /// <summary>?¤í…Œ?´ì? ?´ë¦¬??ì¡°ê±´ ?¬ì„± ???¸ì¶œ.</summary>
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
    /// ê³¨ë“œ ?ë“. ??ì²˜ì¹˜ ??ë³´ìƒ ì§€ê¸????¸ì¶œ.
    /// </summary>
    public void AddGold(int amount)
    {
        gold += Mathf.Max(0, amount);
        Debug.Log($"[GameManager] ê³¨ë“œ ?ë“: +{amount} / ë³´ìœ : {gold}");
    }


	
	/// <summary>
	/// ??ì²˜ì¹˜ ??Enemy.Die()?ì„œ ?¸ì¶œ.
	/// ?¬ì¹´?´íŠ¸ ?„ì  ??ë³´ìŠ¤ ?¤í° ì¡°ê±´ ì²´í¬.
	/// </summary>
	public void OnEnemyKilled()
    {
        killCount++;
        CheckBossSpawnCondition();
    }

    /// <summary>
    /// ?Œê´´ ê°€???¤ë¸Œ?íŠ¸ ?Œê´´ ???´ë‹¹ ?¤ë¸Œ?íŠ¸?ì„œ ?¸ì¶œ.
    /// ?Œê´´ ì¹´ìš´???„ì  ??ë³´ìŠ¤ ?¤í° ì¡°ê±´ ì²´í¬.
    /// </summary>
    public void OnObjectDestroyed()
    {
        destroyedObjectCount++;
        CheckBossSpawnCondition();
    }

    // =====================================================================
    // ?´ë? ë©”ì„œ??
    // =====================================================================
    
    private void ChangeState(GAME_STATE state)
    {
        curState = state;
        OnGameStateChanged?.Invoke(curState);
    }

    /// <summary>ë³´ìŠ¤ ?¤í° ì¡°ê±´ ì²´í¬. ì¡°ê±´ ?¬ì„± ??OnBossSpawn ?´ë²¤??ë°œí–‰.</summary>
    private void CheckBossSpawnCondition()
    {
        if (bossSpawned) return;

        bool killCondition    = killCountToSpawnBoss    > 0 && killCount             >= killCountToSpawnBoss;
        bool destroyCondition = destroyCountToSpawnBoss > 0 && destroyedObjectCount  >= destroyCountToSpawnBoss;

        if (killCondition || destroyCondition)
        {
            bossSpawned = true;
            Debug.Log($"[GameManager] ë³´ìŠ¤ ?¤í° ì¡°ê±´ ?¬ì„± (?? {killCount}, ?Œê´´: {destroyedObjectCount})");
            OnBossSpawn?.Invoke();
        }
    }

    /// <summary>?„íˆ¬ ??ì§„ì… ???„íˆ¬ ê´€??ì¹´ìš´??ì´ˆê¸°??</summary>
    private void ResetBattleData()
    {
        killCount            = 0;
        destroyedObjectCount = 0;
        bossSpawned          = false;
    }

    // =====================================================================
    // ?¸ì´ë¸?/ ë¡œë“œ
    // =====================================================================

    /// <summary>Player ?±ì—???„ì¬ ?íƒœë¥??˜ì§‘??JSON ?Œì¼ë¡??€??</summary>
    private void SaveData(int saveSlot)
    {
        SaveData data = CollectSaveData();
        string json  = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(SavePath(saveSlot), json);
        Debug.Log($"[GameManager] ?€???„ë£Œ: {SavePath(saveSlot)}");
    }

    /// <summary>JSON ?Œì¼?ì„œ ?°ì´?°ë? ?½ì–´ Player ?±ì— ë¶„ë°°.</summary>
    private void LoadData(int saveSlot)
    {
        string path = SavePath(saveSlot);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[GameManager] ?¸ì´ë¸??Œì¼ ?†ìŒ: {path}");
            return;
        }
        string json = File.ReadAllText(path);
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        ApplySaveData(data);
        Debug.Log($"[GameManager] ë¡œë“œ ?„ë£Œ: {path}");
    }

    /// <summary>Player / Loadout ?±ì—???€?¥í•  ?°ì´???˜ì§‘.</summary>
    private SaveData CollectSaveData()
    {
        SaveData data = new SaveData();
        data.gold = gold;

        if (playerRef != null)
        {
            data.level          = playerRef.level;
            data.exp            = playerRef.exp;
            data.expToNextLevel = playerRef.expToNextLevel;
            data.curHp          = playerRef.curHpRemaining;
            data.curShield      = playerRef.curShieldRemaining;
            data.curArmor       = playerRef.curArmorRemaining;
            data.curBoost       = playerRef.curBoostRemaining;

            // ë¯¸ì‚¬???„ì•½ (MissileAmmoInfoê°€ [Serializable]?´ë?ë¡?ì§ì ‘ ë³µì‚¬)
            data.missileAmmoList = new List<MissileAmmoInfo>(playerRef.weaponSystem.missileAmmoList);

            // TODO: PlayerLoadout êµ¬í˜„ ??ì°©ìš© ?¥ë¹„ / ?Œëª¨??/ ?¸ë²¤? ë¦¬ ì¶”ê?
        }
        return data;
    }

    /// <summary>ë¶ˆëŸ¬??SaveDataë¥?Player / Loadout ?±ì— ?ìš©.</summary>
    private void ApplySaveData(SaveData data)
    {
        gold = data.gold;

        if (playerRef != null)
        {
            playerRef.level             = data.level;
            playerRef.exp               = data.exp;
            playerRef.expToNextLevel    = data.expToNextLevel;
            playerRef.curHpRemaining    = data.curHp;
            playerRef.curShieldRemaining = data.curShield;
            playerRef.curArmorRemaining  = data.curArmor;
            playerRef.curBoostRemaining  = data.curBoost;

            // ë¯¸ì‚¬???„ì•½
            playerRef.weaponSystem.missileAmmoList = new List<MissileAmmoInfo>(data.missileAmmoList);

            // TODO: PlayerLoadout êµ¬í˜„ ??ì°©ìš© ?¥ë¹„ / ?Œëª¨??/ ?¸ë²¤? ë¦¬ ?ìš©
        }
    }

    /// <summary>??ê²Œì„ ?œì‘ ???°ì´???„ì²´ ì´ˆê¸°??</summary>
    private void ClearData()
    {
        gold = 0;
        ResetBattleData();
    }
}
