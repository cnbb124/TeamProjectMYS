using System.Collections.Generic;
using UnityEngine;






// ####실제 사용 함수는 맨 밑에~~#####


// 게임 내 모든 사운드 종류를 정의
// 차후 STATE 등등 맞춰서 더추가


//인스펙터에 노출하기 위한 클래스
[System.Serializable]
public class SoundTypeClip
{
	[Tooltip("재생할 사운드의 종류를 선택")]
	public SOUND_TYPE type; // 사운드 종류
	[Tooltip("연결할 오디오 클립(.wav, .mp3 등)을 할당")]
	public AudioClip clip;// 실제 사운드 파일




	[Header("3D 사운드 설정 (BGM,UI등 2D 사운드는 적용 안 됨)")]
	[Tooltip("3D 효과음 전용.이 거리 안에서는 소리가 최대유지")]
	public float minDistance = 1.0f;
	[Tooltip("3D 효과음 전용.이 거리 밖에서는 소리 X")]
	public float maxDistance = 50.0f;
	[Range(0f, 1f)]
	[Tooltip("3D 사운드 해당클립 볼륨 개별 배율(3D용) (0~1)")]
	public float volumeScale = 1.0f; // 기본값은 1 (최대)

}

public class SoundManager : MonoBehaviour
{
	[TextArea(1, 999), SerializeField]
	private string memo =
		"사용시 SoundManager.Instance.함수명\n" +
		"사운드 데이터 등록시 필요한만큼 리스트 우측 숫자변경\n" +
		"" +
		"=== 사용 가능한 함수 목록 ===\n" +
		"1. PlayBGM(SOUND_TYPE) - 배경음 재생함\n" +
		"2. PlayUISFX(SOUND_TYPE) - UI(2D)효과음 재생함\n" +
		"3. Play3DSFXAtPosition(SOUND_TYPE, Vector3) - 3D 효과음 재생함\n" +
		"※ 플레이 함수 뒤에 피치값(float형 min, max) 추가 시 랜덤 재생됨(오버로딩)\n" +
		"4. StopBGM() - 배경음 정지함\n" +
		"5. StopAll() - 모든 소리 정지함\n" +
		"=== 본인이 필요한 메모 사용 밑으로 추가 ===\n";



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

	[Space(10)]
	[Header("1. 사운드 데이터 등록")]
	[Tooltip("사운드 타입과 오디오 클립을 짝지어 등록하는 리스트")]
	[SerializeField] private SoundTypeClip[] soundList;

	[Space(10)]
	[Header("2. 오디오 소스 설정 BGM&UI(2D)")]
	[SerializeField]
	[Tooltip("배경음악(BGM) 재생을 전담하는 소스. Loop(반복 재생)가 자동으로 활성화")]
	private AudioSource bgmSource; // BGM 전용 스피커 (반복 재생 켜두기)

	[SerializeField]
	[Tooltip("UI(2D)용")]
	private AudioSource sfxUISource; // UI/일반 효과음 전용 스피커 (2D)

	[Space(10)]
	[Header("3. 기본 볼륨 설정")]
	[Range(0f, 1f)]
	public float bgmVolume = 1.0f;
	[Range(0f, 1f)]
	public float sfxUIVolume = 1.0f;
	[Range(0f, 1f)]
	public float sfx3DVolume = 1.0f;

	[Space(10)]
	[Header("4. 3D 사운드 풀링 사이즈 설정")]
	[Tooltip("게임 시작 시 미리 만들어둘 3D 스피커의 개수.")]
	[SerializeField]
	private uint initialPoolSize = 20;

	


	//열거형으로 빠르게 클립p을 찾기 위한 딕셔너리
	//사운드타입을 키로받고, 클래스를 값으로
	private Dictionary<SOUND_TYPE, SoundTypeClip> soundDict = new Dictionary<SOUND_TYPE, SoundTypeClip>();
	// 3D 효과음 재생을 위한 오디오 소스 풀(Pool)
	private List<AudioSource> sfx3DPool = new List<AudioSource>();

	private void Awake()
	{
		// 싱글톤 기본 세팅 (씬이 넘어가도 파괴되지 않게 유지)
		if (instance == null)
		{
			instance = this;
			DontDestroyOnLoad(gameObject);
			InitializeDictionary(); // 시작할 때 딕셔너리 세팅
			InitializeSFXPool();    // 시작할 때 3D 사운드 풀링 세팅

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
				//클래스내의 타입멤버를 키로, 클래스자체를 값으로더하기
				soundDict.Add(item.type, item);
			}
			else
			{
				Debug.LogWarning($"[SoundManager] {item.type} 사운드 중복 등록 확인요망");
			}
		}
	}



	// ================== [오브젝트 풀링 함수들] ==================

	// 초기 스피커 풀 생성
	private void InitializeSFXPool()
	{
		for (int i = 0; i < initialPoolSize; i++)
		{
			CreateNewAudioSourceToPool();
		}
	}

	// 새로운 오디오 소스 생성 및 풀 리스트에 추가
	private AudioSource CreateNewAudioSourceToPool()
	{
		GameObject go = new GameObject($"SFX_Pool_Speaker_{sfx3DPool.Count}");//이름번호매기기
		go.transform.SetParent(this.transform); // 매니저의 자식 오브젝트로 정리

		AudioSource source = go.AddComponent<AudioSource>();
		source.spatialBlend = 1.0f; // 1.0 = 완전한 3D 사운드
		source.playOnAwake = false;

		sfx3DPool.Add(source);
		return source;
	}

	// 풀에서 사용 가능한(현재 재생 중이 아닌) 스피커를 찾아 반환
	private AudioSource GetAvailableSource()
	{
		for (int i = 0; i < sfx3DPool.Count; i++)
		{
			if (!sfx3DPool[i].isPlaying)
			{
				return sfx3DPool[i];
			}
		}

		// 모든 스피커가 사용 중일 경우, 새롭게 하나를 더 생성하여 반환
		return CreateNewAudioSourceToPool();
	}


	//private AudioClip GetClip(SOUND_TYPE type)
	//{
	//	if (soundDict.TryGetValue(type, out AudioClip clip))
	//	{
	//		return clip;
	//	}
	//	//디버깅
	//	Debug.LogError($"[SoundManager] {type}에 해당하는 사운드 파일누락! 인스펙터 확인ㅇ망.");
	//	return null;
	//}

	//재생용, 클래스  꺼내오기(여기서만사용)
	private SoundTypeClip GetSoundData(SOUND_TYPE type)
	{
		if (soundDict.TryGetValue(type, out SoundTypeClip data))
		{
			return data;
		}
		Debug.LogError($"[SoundManager] {type} 데이터 누락됨");
		return null;
	}

	//// ================== [실시간 배율 추적 함수] ==================//미사용
	//// 현재3D 스피커에서 재생 중인 소리 파일이 무엇인지 확인하여 각자맞춰둔 배율갖고오기
	//private float GetSFX3DVolumeScale(AudioClip targetClip)
	//{
	//	if (targetClip == null)
	//	{
	//		return 1.0f;
	//	}
	//	foreach (var item in soundList)
	//	{
	//		if (item.clip == targetClip) return item.volumeScale;
	//	}
	//	return 1.0f;
	//}






	// ================== [실제 사용되는 재생 함수들] ==================



	// PlayBGM(사운드 열거형) BGM재생
	// PlaySFX(사운드 열거형) UI 클릭,주사위굴리기 등 화면전체에서 들려야하는 2d사운드재생
	// PlaySFXAtPosition(사운드 열거형, 좌표) 이동,총알,폭발등등 3d사운드 재생




	// BGM재생

	public void PlayBGM(SOUND_TYPE type)
	{
		SoundTypeClip data = GetSoundData(type);
		if (data != null)
		{

			bgmSource.volume = bgmVolume;
			bgmSource.clip = data.clip;
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

	public void PlaySFXUI(SOUND_TYPE type)
	{
		SoundTypeClip data = GetSoundData(type);
		if (data != null)
		{

			sfxUISource.pitch = 1.0f; // 기본 피치로 초기화
			sfxUISource.PlayOneShot(data.clip, sfxUIVolume * data.volumeScale);
		}
	}
	// 사용예
	// SoundManager.Instance.PlaySFX(SOUND_TYPE.SFX_DICE_ROLL);


	// <summary>
	// 단조로움을 방지하기 위해 랜덤한 피치(음높이)로 2D 효과음 재생
	// 사용 예: PlaySFX(SOUND_TYPE.SFX_SHOOT, 0.9f, 1.1f);
	// </summary>
	public void PlaySFXUI(SOUND_TYPE type, float pitchMin, float pitchMax)
	{
		SoundTypeClip data = GetSoundData(type);
		if (data != null)
		{

			sfxUISource.pitch = Random.Range(pitchMin, pitchMax);
			sfxUISource.PlayOneShot(data.clip, sfxUIVolume * data.volumeScale);

		}
	}


	// 총소리, 폭발음,이동 등 특정 위치에서 나야 하는 3D 효과음을 재생(랜덤x)
	// 타입과 좌표받기
	public void PlaySFX3DAtPosition(SOUND_TYPE type, Vector3 position)
	{
		SoundTypeClip data = GetSoundData(type);
		if (data != null)
		{

			// 지정된 위치에 임시 스피커를 만들고, 소리가 끝나면 알아서 삭제됨
			AudioSource source = GetAvailableSource();//가능한 소스 풀에서 갖고오기
			source.transform.position = position;// 입력한좌표로 출력할 좌표지정
			source.clip = data.clip;//타입으로 갖고온 클립을 출력할 클립으로 지정

			source.minDistance = data.minDistance;
			source.maxDistance = data.maxDistance;
			source.volume = sfx3DVolume * data.volumeScale;//볼ㄹ뮤지정
			source.pitch = 1.0f;//랜덤 아니므로 기본설정
			source.Play();

		}
	}
	//사용예
	//Soundmanager.Instance.PlaySFXAtPosition(SOUND_TYPE.SFX_SHOOT, transform.position);

	//랜덤 재생(오버로딩)
	public void PlaySFX3DAtPosition(SOUND_TYPE type, Vector3 position, float pitchMin, float pitchMax)
	{
		SoundTypeClip data = GetSoundData(type);
		if (data != null)
		{

			// 지정된 위치에 임시 스피커를 만들고, 소리가 끝나면 알아서 삭제됨
			AudioSource source = GetAvailableSource();//가능한 소스갖고오기
			source.transform.position = position;// 입력한좌표로 출력할 좌표지정
			source.clip = data.clip;//타입으로 갖고온 클립을 출력할 클립으로 지정
			source.minDistance = data.minDistance;
			source.maxDistance = data.maxDistance;
			source.volume = sfx3DVolume * data.volumeScale;//볼ㄹ뮤지정

			source.pitch = Random.Range(pitchMin, pitchMax);//랜덤
			source.Play();

		}
	}



	// ================== [정지 함수들] ==================


	// 재생 중인 BGM 정지

	public void StopBGM()
	{
		if (bgmSource.isPlaying)
		{
			bgmSource.Stop();
		}
	}


	// 모든 사운드(BGM 및 2D SFX) 정지

	public void StopAll()
	{
		bgmSource.Stop();
		sfxUISource.Stop();

		// 풀링된 3D 스피커들도 모두 재생 정지
		for (int i = 0; i < sfx3DPool.Count; i++)
		{
			if (sfx3DPool[i].isPlaying)
			{
				sfx3DPool[i].Stop();
			}
		}
	}

	// ================== [실시간 볼륨 조절 함수 (UI 옵션 창 연동용)] ==================

	// 차후 UI팀이 환경설정 창의 슬라이더(OnValueChanged)에 연결할 함수


	public void SetBGMVolume(float volume)
	{
		bgmVolume = volume;//입력한 볼륨값 현재설정에 저장
		if (bgmSource != null && bgmSource.clip != null)
		{
			bgmSource.volume = bgmVolume; //현재설정을 실제로 반영
		}
	}

	public void SetSFXUIVolume(float volume)
	{
		sfxUIVolume = volume;//입력한 볼륨값 현재설정에 저장
		if (sfxUISource != null)
		{
			sfxUISource.volume = sfxUIVolume;//현재설정을 실제로 반영
		}
	}

	public void SetSFX3DVolume(float volume)
	{
		sfx3DVolume = volume;//입력한 볼륨값 현재설정에 저장
		foreach (var src in sfx3DPool)
		{
			if (src.isPlaying)//혹여나 실행되고있는게있따면
			{
				src.volume = sfx3DVolume;
			}
		}
	}
	



#if UNITY_EDITOR
	private void OnValidate()
	{
		// 에디터 플레이 중 인스펙터 조작 시 세 가지 볼륨 모두 실시간 갱신
		if (Application.isPlaying)
		{
			SetBGMVolume(bgmVolume);
			SetSFXUIVolume(sfxUIVolume);
			SetSFX3DVolume(sfx3DVolume);
		}
	}
#endif
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