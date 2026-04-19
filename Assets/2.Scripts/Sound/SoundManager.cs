using System.Collections.Generic;
using UnityEngine;






// ####실제 사용 함수는 맨 밑에~~#####

// 게임 내 모든 사운드 종류를 정의
// 차후 STATE 등등 맞춰서 더추가

public enum SOUND_TYPE
{
	BGM_LOBBY,      // 정거장(상점) 배경음
	BGM_BATTLE,     // 우주 전투 배경음
	SFX_SHOOT,      // 기본 미사일 발사음
	SFX_HIT,        // 피격음
	SFX_EXPLOSION,  // 폭발음
	SFX_DICE_ROLL,  // 주사위 굴리는 소리
	SFX_UI_CLICK    // 버튼 클릭음
}

//인스펙터에 노출하기 위한 클래스
[System.Serializable]
public class SoundTypeClip
{
	public SOUND_TYPE type;   // 사운드 종류
	public AudioClip clip;    // 실제 사운드 파일
}

public class SoundManager : MonoBehaviour
{
	private static SoundManager instance = null;
	public static SoundManager Instance
	{
		get
		{
			if (instance == null)
			{
				
				instance = FindObjectOfType<SoundManager>();
				if (instance == null)
				{
					Debug.LogError("씬에 SoundManager 누락! 하이어라키에 사운드매니저 필요");
				}
			}
			return instance;
		}
	}

	[Header("사운드 등록 (인스펙터에서 드래그 앤 드롭)")]
	[SerializeField] private SoundTypeClip[] soundList;

	[Header("Audio Sources")]
	[SerializeField]
	private AudioSource bgmSource; // BGM 전용 스피커 (반복 재생 켜두기)
	[SerializeField]
	[Tooltip("2D UI용")]
	private AudioSource sfxSource; // UI/일반 효과음 전용 스피커 (2D)

	//열거형으로 빠르게 클립p을 찾기 위한 딕셔너리
	private Dictionary<SOUND_TYPE, AudioClip> soundDict = new Dictionary<SOUND_TYPE, AudioClip>();

	private void Awake()
	{
		// 싱글톤 기본 세팅 (씬이 넘어가도 파괴되지 않게 유지)
		if (instance == null)
		{
			instance = this;
			DontDestroyOnLoad(gameObject);
			InitializeDictionary(); // 시작할 때 딕셔너리 세팅
		}
		else if (instance != this)
		{
			
			Debug.LogWarning("중복된 SoundManager 발견. 파괴 후 실행");
			Destroy(gameObject);
		}
	}

	//인스펙터에 올린거 딕셔너리로 자동으로 옮겨 담는작업
	private void InitializeDictionary()
	{
		foreach (var item in soundList)
		{
			// 중복 방지: 딕셔너리에 같은 키가 이미 있는지 확인
			if (!soundDict.ContainsKey(item.type))
			{
				soundDict.Add(item.type, item.clip);
			}
			else
			{
				Debug.LogWarning($"[SoundManager] {item.type} 사운드 중복 등록 확인요망");
			}
		}
	}

	//재생용 오디오클립을  꺼내오기(여기서만사용)
	private AudioClip GetClip(SOUND_TYPE type)
	{
		if (soundDict.TryGetValue(type, out AudioClip clip))
		{
			return clip;
		}
		//디버깅
		Debug.LogError($"[SoundManager] {type}에 해당하는 사운드 파일누락! 인스펙터 확인ㅇ망.");
		return null;
	}







	// ================== [실제 사용되는 재생 함수들] ==================



	// PlayBGM(사운드 열거형) BGM재생
	// PlaySFX(사운드 열거형) UI 클릭,주사위굴리기 등 화면전체에서 들려야하는 2d사운드재생
	// PlaySFXAtPosition(사운드 열거형, 좌표) 이동,총알,폭발등등 3d사운드 재생




	// BGM재생

	public void PlayBGM(SOUND_TYPE type)
	{
		AudioClip clip = GetClip(type);
		if (clip != null)
		{
			bgmSource.clip = clip;
			bgmSource.loop = true; // BGM은 무한반복
			bgmSource.Play();
		}
	}
	// 사용예
	//private void Start()
	//{
	//	// 게임 시작(또는 로비 씬 로드) 시 로비 BGM 재생
	//	SoundManager.Instance.PlayBGM(SOUND_TYPE.BGM_LOBBY);
	//}

	// UI 클릭, 주사위 굴리기 등 화면 전체에서 들려야 하는 2D 효과음

	public void PlaySFX(SOUND_TYPE type)
	{
		AudioClip clip = GetClip(type);
		if (clip != null)
		{
			sfxSource.PlayOneShot(clip);
		}
	}
	// 사용예
	// SoundManager.Instance.PlaySFX(SOUND_TYPE.SFX_DICE_ROLL);



	// 총소리, 폭발음,이동 등 특정 위치에서 나야 하는 3D 효과음을 재생
	// 타입과 좌표받기
	public void PlaySFXAtPosition(SOUND_TYPE type, Vector3 position)
	{
		AudioClip clip = GetClip(type);
		if (clip != null)
		{
			// 지정된 위치에 임시 스피커를 만들고, 소리가 끝나면 알아서 삭제됨
			AudioSource.PlayClipAtPoint(clip, position);
		}
	}
	//사용예
	//Soundmanager.Instance.PlaySFXAtPosition(SOUND_TYPE.SFX_SHOOT, transform.position);
}


//빈 오브젝트 생성: 하이어라키(Hierarchy) 창에서 우클릭 후 Create Empty를 눌러 빈 게임 오브젝트를 만들고,
//이름을 SoundManager로 변경

//스크립트 부착: 만들어진 SoundManager 오브젝트에 작성한 SoundManager.cs 스크립트를 컴포넌트로 추가(Add Component)

//오디오 소스 추가: 해당 오브젝트에 Audio Source 컴포넌트를 2개 추가

//컴포넌트 할당: *인스펙터 창의 SoundManager 스크립트에서 Bgm Source 빈칸에 첫 번째 Audio Source를 드래그

//Sfx Source 빈칸에 두 번째 Audio Source를 드래그

//세부 설정 (필수 주의사항):

//추가한 2개의 Audio Source 컴포넌트 모두 Play On Awake 체크를 반드시 해제
//(체크해 두면 게임 시작과 동시에 빈 소리가 재생되려다 에러가 날 수 있습니다.)

//사운드 파일 등록: 스크립트의 Sound List를 열어 필요한 개수만큼 늘린 뒤,
//사운드 타입(Enum)과 실제 오디오 파일(.wav, .mp3)을 각각 짝지어 넣어줍니다.