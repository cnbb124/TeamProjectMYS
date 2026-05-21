using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
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
					Debug.LogError("씬에 GameManager 누락! 하이어라키에 게임매니저 필요");
				}
			}
			return instance;
		}
	}

	private void Awake()
	{
		if (instance == null)
		{
			instance = this;
			DontDestroyOnLoad(gameObject);
		}
		else if (instance != this)
		{
			Debug.LogWarning("중복된 GameManager 발견. 파괴 후 실행");
			Destroy(gameObject);
		}

		//SceneManager.sceneLoaded += OnSceneLoaded;
	}

	

	//===== 현재 게임 상태=====
	public GAME_STATE curState;

	// =====UI팀 연동용 =======
	// 상태 변화 시 UI에서 패널 전환 등에 사용
	public System.Action<GAME_STATE> OnGameStateChanged;
	

	

	// ===== 씬 전환======
	/// <summary>씬 이름으로 전환. 전환 전 정리 처리 포함.</summary>
	/// bgm은 코루틴내로 이동시킬수도있음 수정예정
	public void LoadScene(string sceneName)
	{
		StartCoroutine(LoadSceneRoutine(sceneName));
		PlaySceneBGM(sceneName);
	}

	/// <summary>씬 타입 enum으로 전환.</summary>
	public void LoadScene(SCENE_TYPE sceneType)
	{
		StartCoroutine(LoadSceneRoutine(sceneType));
		PlaySceneBGM(sceneType.ToString());
	}

	/// <summary>
	/// 로드된후 해당 씬의 bgm을 틀을 함수(사운드매니저 호출)
	/// 필요시 안에 추가, enum추가
	/// </summary>
	/// <param name="sceneName"></param>
	private void PlaySceneBGM(string sceneName)
	{
		switch (sceneName)
		{
			case "MAIN":
				//SoundManager.Instance.PlayBGM(SOUND_TYPE.)
				break;
			case "STAGE1":
				//SoundManager.Instance.PlayBGM(SOUND_TYPE.)
				break;
			case "STATION":
				//SoundManager.Instance.PlayBGM(SOUND_TYPE.)
				break;
			case "GAME_OVER":
				//SoundManager.Instance.PlayBGM(SOUND_TYPE.)
				break;
			default:
				// 지정되지 않은 씬의 경우 BGM을 끄거나 기본 BGM을 유지하는 등의 처리
				break;
		}
	}

	//씬 전환 전 정리 후 로드
	//LoadScene내에서 쓰이는 코루틴
	private IEnumerator LoadSceneRoutine(string sceneName)
	{
		// 전환 전 정리
		Time.timeScale = 1f;
		PoolManager.Instance.DisableAllProjectiles();
		SoundManager.Instance.StopSFXAll();

		// 필요 시 페이드아웃 연출 여기서 추가
		// yield return StartCoroutine(FadeOut());

		yield return null;
		SceneManager.LoadScene(sceneName);
	}

	// 씬 전환 전 정리 후 로드
	private IEnumerator LoadSceneRoutine(SCENE_TYPE sceneType)
	{
		// 전환 전 정리
		Time.timeScale = 1f;
		PoolManager.Instance.DisableAllProjectiles();
		SoundManager.Instance.StopSFXAll();

		// 필요 시 페이드아웃 연출 여기서 추가
		// yield return StartCoroutine(FadeOut());

		yield return null;
		SceneManager.LoadScene((int)sceneType);
	}


	#region UI매니저에서 호출

	/// <summary>
	/// UI매니저에서 호출.
	/// 새게임 시작.
	/// </summary>
	public void NewGame()
	{
		ClearData();
		ChangeState(GAME_STATE.PLAYING);
		LoadScene(SCENE_TYPE.STAGE1);
	}

	/// <summary>
	/// UI매니저에서 호출.
	/// 저장된 게임 로드. 세이브슬롯 번호로 호출.
	/// </summary>
	public void LoadGame(int saveSlotNum)
	{
		ChangeState(GAME_STATE.PLAYING);
		LoadData(saveSlotNum);
		// LoadScene(해당씬)
	}

	/// <summary>
	/// UI매니저에서 호출.
	/// 현재 게임 세이브.
	/// </summary>
	public void SaveGame(int saveSlotNum)
	{
		SaveData(saveSlotNum);
	}

	/// <summary>
	/// UI매니저에서 호출.
	/// 일시정지.
	/// </summary>
	public void PauseGame()
	{
		if (curState != GAME_STATE.PLAYING)
		{
			return;
		}
		Time.timeScale = 0f;
		ChangeState(GAME_STATE.PAUSED);
	}

	/// <summary>
	/// UI매니저에서 호출.
	/// 일시정지 해제.
	/// </summary>
	public void ResumeGame()
	{
		if (curState != GAME_STATE.PAUSED)
		{
			return;
		}
		Time.timeScale = 1f;
		ChangeState(GAME_STATE.PLAYING);
	}

	#endregion


	#region 게임 내부에서 호출

	/// <summary>
	/// 플레이어 사망 시 Player.Die()에서 호출.
	/// </summary>
	public void GameOver()
	{
		if (curState == GAME_STATE.GAME_OVER)
		{
			return;
		}
		ChangeState(GAME_STATE.GAME_OVER);
		Time.timeScale = 0f;
		PoolManager.Instance.DisableAllProjectiles();
		SoundManager.Instance.StopSFXAll();
	}

	/// <summary>
	/// 스테이지 클리어 조건 달성 시 호출.
	/// </summary>
	public void GameClear()
	{
		if (curState == GAME_STATE.CLEAR)
		{
			return;
		}
		ChangeState(GAME_STATE.CLEAR);
		
		PoolManager.Instance.DisableAllProjectiles();
	}

	
	

	#endregion


	// 상태 변경 + 이벤트 발행
	private void ChangeState(GAME_STATE state)
	{
		curState = state;
		OnGameStateChanged?.Invoke(curState);
	}

	/// <summary>
	/// 매개변수 세이브슬롯을 받아 모든 데이터를 로드할 메서드.
	/// 모든 적유닛의 배치, 상호작용 오브젝트, 잔탄, 잔여HP, 게이지, 실드량, 장비, 인벤, 골드, 호감도, 경험치 등 플레이어 정보.
	/// </summary>
	private void LoadData(int saveSlot)
	{
		// Array[saveSlot]에서 데이터 꺼내기 혹은 리스트, 맵, 딕셔너리 등
		//
	}

	private void SaveData(int saveSlot)
	{
		// 로드와 반대로 해당 배열에 저장
	}

	private void ClearData()
	{

	}
}
