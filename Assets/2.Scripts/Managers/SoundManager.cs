using System.Collections;
using System.Collections.Generic;
using UnityEngine;



// ================================================================
// [SoundManager — 외부 참조 / 사용 가이드]
// ================================================================
// 싱글톤. 씬에 하나만 존재.
// BGM / UI 효과음 / 3D 공간 효과음 / 3D 루프음 재생 담당.
//
// ================================================================
// [사운드 종류별 호출 함수]
// ================================================================
// PlayBGM(SOUND_TYPE)                               BGM 재생 (2D, 전체 공간)
// StopBGM()                                         BGM 정지
//
// PlaySFXUI(SOUND_TYPE)                             UI 효과음 재생 (2D)
// PlaySFXUI(SOUND_TYPE, float pitchMin, float pitchMax)   피치 범위 지정 버전
//
// PlaySFX3DAtPosition(SOUND_TYPE, Vector3)          월드 고정 위치에서 3D 효과음 재생
// PlaySFX3DAtPosition(SOUND_TYPE, Vector3, float pitchMin, float pitchMax)
//
// PlaySFX3DAtUnit(SOUND_TYPE, Transform unitTr, Transform playPos = null)
//   유닛에 부착된 3D 효과음 재생. unitTr = 소스 부모(유닛 루트), playPos = 실제 재생 위치(총구 등).
//   playPos 생략 시 unitTr 위치에서 재생.
//
// PlaySFX3DLoop(SOUND_TYPE, Transform targetTr)     루프 사운드 시작 (유닛에 부착), AudioSource 반환
//                                                     → (대상,타입) 조합별로 동시 재생 가능 (엔진음 레이어 크로스페이드 등)
//                                                     → 이미 재생 중이면 기존 AudioSource 그대로 반환(volume/pitch 직접 조절용)
// StopSFX3DLoop(SOUND_TYPE, Transform targetTr)     루프 사운드 정지
// StopSFXAll()                                      모든 효과음 정지
//
// ================================================================
// [볼륨 제어]
// ================================================================
// SetBGMVolume(float)    BGM 전체 볼륨 (0~1)
// SetSFXUIVolume(float)  UI 효과음 전체 볼륨
// SetSFX3DVolume(float)  3D 효과음 전체 볼륨
//
// ================================================================
// [SoundTypeClip 인스펙터 설정 항목]
// ================================================================
// type          : SOUND_TYPE 매핑
// clips         : 오디오 클립(들). 여러 개 등록 시 재생마다 랜덤 선택
// volumeScale   : 개별 볼륨 배율 (0~1). volumeMin/Max가 다르면 무시됨
// volumeMin/Max : 볼륨 랜덤 범위 (같으면 volumeScale 고정값 사용)
// minDistance   : 3D 전용 — 최대 볼륨 유지 거리
// maxDistance   : 3D 전용 — 소리 소멸 거리
// maxConcurrent : 동시 재생 한도 (0=무제한)
// dropOldest    : 한도 초과 시 오래된 소리 끊기(true) / 새 소리 무시(false)
// pitchMin/Max  : 피치 랜덤 범위 (같으면 고정)
// ================================================================
//
// 게임 내 모든 사운드 종류를 정의
// 차후 STATE 등등 맞춰서 더추가


//인스펙터에 노출하기 위한 클래스
[System.Serializable]
public class SoundTypeClip
{
	[Tooltip("재생할 사운드의 종류를 선택")]
	public SOUND_TYPE type; // 사운드 종류
	[Tooltip("연결할 오디오 클립(.wav, .mp3 등)을 할당. 여러 개 등록 시 재생마다 랜덤으로 하나 선택됨")]
	public AudioClip[] clips;// 실제 사운드 파일(들)

	[Header("<size=14>소리 재생 설정</size>")]
	[Range(0f, 1f)]
	[Tooltip("해당 사운드 클립 볼륨 개별 배율 (0~1). volumeMin/Max가 서로 다르면 무시되고 그쪽이 우선됨")]
	public float volumeScale = 1.0f; // 기본값은 1 (최대)
	[Range(0f, 1f)]
	[Tooltip("재생 볼륨 최솟값. volumeMax와 같으면 무시(volumeScale 고정값 사용)")]
	public float volumeMin = 1.0f;
	[Range(0f, 1f)]
	[Tooltip("재생 볼륨 최댓값. volumeMin과 같으면 무시(volumeScale 고정값 사용)")]
	public float volumeMax = 1.0f;
	[Range(0f, 1.0f)]
	[Tooltip("재생 피치 최솟값. pitchMax와 같으면 고정 피치")]
	public float pitchMin = 1.0f;
	[Range(0f, 1.0f)]
	[Tooltip("재생 피치 최댓값. pitchMin과 같으면 고정 피치")]
	public float pitchMax = 1.0f;

	[Tooltip("최소 재생 간격(초). 마지막 재생 후 이 시간 안에 다시 호출되면 무시(스킵). 0이면 비활성(제한 없음).\n" +
		"초고속 연사 무기(발칸 등)처럼 너무 잦은 호출로 사운드가 겹쳐서 찢어질 때 사용.")]
	public float minPlayInterval;

	[Header("<size=14>폴리포니(다중재생) 설정</size>")]
	[Tooltip("동시 재생 허용 개수. 0 = 무제한 (권장: 발사음 3~4, 폭발음 3, 이동루프 1)")]
	public int maxConcurrent = 4;
	[Tooltip("한도 초과 시 true이면 가장 오래된 소리를 끊고 새 소리 재생, false이면 새 소리 무시")]
	public bool dropOldest = false;


	[Space(10)]
	[Header("<size=14>3D 사운드 전용 재생 범위 설정</size>")]
	[Tooltip("3D 효과음 전용.이 거리 안에서는 소리가 최대유지")]
	public float minDistance = 1.0f;
	[Tooltip("3D 효과음 전용.이 거리 밖에서는 소리 X")]
	public float maxDistance = 50.0f;



}

// =====================================================================
// EngineSoundConfig
// 엔진 루프음(공회전/가속/부스트) 크로스페이드 전용 튜닝값.
// Unit.cs는 speedRatio/isBoosting만 계산해서 넘기고, 곡선 자체는 여기(SoundManager) 전담 — RTPC 스타일 분리.
// SFX_IDLE/SFX_MOVING/SFX_BOOST(SoundTypeClip)와는 별개 — 모든 사운드가 공유하는 SoundTypeClip에
// 엔진 전용 필드를 끼워넣으면 BGM 등 무관한 항목까지 같이 보여서 혼란스러워지므로 전용 클래스로 분리.
// =====================================================================
[System.Serializable]
public class EngineSoundConfig
{
	[Tooltip("정지 상태(속도비율 0)일 때 공회전음 최대 볼륨")]
	public float idleMaxVolume = 1f;
	[Tooltip("최고속(속도비율 1)일 때 가속음 최대 볼륨")]
	public float thrustMaxVolume = 1f;
	[Tooltip("부스트 중일 때 부스트음 최대 볼륨")]
	public float boostMaxVolume = 1f;
	[Tooltip("부스트 사운드가 켜지고/꺼질 때 볼륨이 변하는 속도(초당)")]
	public float boostFadeSpeed = 4f;
	[Tooltip("가속음 피치 범위 — 속도비율 0일 때 minPitch, 1일 때 maxPitch")]
	public float thrustMinPitch = 0.9f;
	public float thrustMaxPitch = 1.3f;
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
				else
				{
					DontDestroyOnLoad(instance.gameObject);
					// 다른 오브젝트가 자기 Awake/OnEnable에서 Instance를 먼저 건드리면 이 SoundManager의
					// Awake가 아직 안 돌았을 수 있음(Unity는 스크립트 간 Awake 순서를 보장 안 함).
					// 그 경우 여기서 즉시 초기화해야 사운드 목록(_soundDict)이 빈 채로 굳지 않는다.
					instance.EnsureInitialized();
				}
			}
			return instance;
		}
	}
	

	[Header("<size=22>사용시 SoundManager.Instance.메서드명</size>\n\n" +
		"사운드 데이터 등록시 필요한만큼 리스트 우측 숫자변경\n" +
		"" +
		"====== 사용 가능한 메서드 목록 ======\n" +
		"1. PlayBGM(SOUND_TYPE) - 배경음 재생함\n" +
		"2. PlaySFXUI(SOUND_TYPE) - UI(2D)효과음 재생함\n" +
		"3. PlaySFX3DAtPosition(SOUND_TYPE, Vector3) - 3D 효과음 재생함\n" +
		"4. PlaySFX3DAtUnit(SOUND_TYPE, Transform unitTr, Transform playPos) - 유닛에 부착된 단발성 3D 효과음 재생함\n" +
		"5. PlaySFX3DLoop(SOUND_TYPE, Transform targetTr) - 루프 3D 효과음 시작함 (AudioSource 반환)\n" +
		"6. StopSFX3DLoop(SOUND_TYPE, Transform targetTr) - 루프 3D 효과음 정지함\n" +
		"※ 플레이 함수 뒤에 피치값(float형 min, max) 추가 시 랜덤 재생됨(오버로딩)\n" +
		"7. StopBGM() - 배경음 정지함\n" +
		"8. StopSFXAll() - 모든 소리 정지함\n" +
		"9. SetBGM,SFX등 메서드 - 차후 UI옵션창과 연동")]


	[Space(10)]
	[Header("<size=18>사운드 데이터 등록</size>")]
	[Tooltip("사운드 타입과 오디오 클립을 짝지어 등록하는 리스트")]
	[SerializeField]
	private SoundTypeClip[] _soundList;

	[Space(10)]
	[Header("<size=14>오디오 소스 연결 BGM&UI(2D)</size>")]
	[SerializeField]
	[Tooltip("배경음악(BGM) SFX재생을 전담할 소스 연결. Loop(반복 재생)가 자동으로 활성화")]
	private AudioSource _bgmSource; // BGM 전용 스피커 (반복 재생 켜두기)
	[SerializeField]
	[Tooltip("UI(2D) SFX재생을 전담할 소스연결.")]
	private AudioSource _sfxUISource; // UI/일반 효과음 전용 스피커 (2D)
	[Header("현재 BGM소스에 입력된 사운드(출력 확인용)")]
	public SOUND_TYPE curBGM;
	private SoundTypeClip _curBgmData; // SetBGMVolume에서 GetVolume 적용하기 위한 캐시

	// BGM 볼륨 합성용 내부 상태 (실제 볼륨 = bgmVolume × 클립볼륨 × _bgmFadeFactor × 일시정지 시 pauseAllVolumeScale×pauseBGMVolumeScale)
	private float _bgmFadeFactor = 1f;    // 씬 전환/보스 전환 페이드용 배율(0~1)
	private bool _bgmAtGamePaused = false;       // 일시정지 중 볼륨 감쇠 적용 여부
	private bool _bgmRotating = false;     // 여러 클립 순환(플레이리스트) 재생 중인지
	private int _lastBgmClipIndex = -1;    // 직전 재생 클립 인덱스(랜덤 중복 방지)
	private Coroutine _bgmFadeRoutine;     // 진행 중인 BGM 페이드 전환 코루틴


	private bool _allVolumeAtGamePaused = false;
	// 현재 일시정지 감쇠 배율. 3D SFX·엔진음의 볼륨 계산에 곱해, 정지 중 새로 시작되는 소리와
	// 매 프레임 재계산되는 엔진 루프도 함께 감쇠시킨다(BGM의 pauseScale과 같은 역할).
	private float SfxPauseScale => _allVolumeAtGamePaused ? pauseAllVolumeScale : 1f;

	[Space(10)]
	[Header("<size=14>기본 볼륨 설정</size>")]
	[Range(0f, 1f)]
	public float bgmVolume = 1.0f;
	[Range(0f, 1f)]
	[Tooltip("일시정지 중 BGM에만 추가로 곱해지는 배율(pauseAllVolumeScale에 중첩 적용). 예: All=0.5, 이 값=0.5면 BGM은 최종 0.25배. 1이면 추가 감쇠 없이 All 배율만 적용.")]
	public float pauseBGMVolumeScale = 0.5f;
	[Range(0f, 1f)]
	[Tooltip("일시정지 중 전체 사운드(BGM+SFX+엔진) 1차 감쇠 배율. BGM은 여기에 pauseBGMVolumeScale이 추가로 곱해짐.")]
	public float pauseAllVolumeScale = 0.5f;
	[Range(0f, 1f)]
	public float sfxUIVolume = 1.0f;
	[Range(0f, 1f)]
	public float sfx3DVolume = 1.0f;

	[Space(10)]
	[Header("<size=14>엔진 루프음 크로스페이드 설정</size>")]
	public EngineSoundConfig engineSoundConfig = new EngineSoundConfig();

	[Space(10)]
	[Header("<size=14>3D 사운드 풀링 사이즈 설정</size>")]
	[Tooltip("게임 시작 시 미리 만들어둘 3D 스피커의 개수.")]
	[SerializeField]
	private uint _initialPoolSize = 20;




	//열거형으로 빠르게 클립p을 찾기 위한 딕셔너리
	//사운드타입을 키로받고, 클래스를 값으로
	private Dictionary<SOUND_TYPE, SoundTypeClip> _soundDict = new Dictionary<SOUND_TYPE, SoundTypeClip>();
	//루프 사운드를 추적하기 위한 딕셔너리 (어떤 오브젝트가 어떤 소스를 쓰고 있는지 기록)
	// (Transform, SOUND_TYPE) 복합키 — 같은 유닛이라도 사운드 종류가 다르면 동시에 여러 루프 재생 가능
	// (예: 엔진 공회전/가속/부스트 레이어를 한 유닛에서 동시에 크로스페이드)
	private Dictionary<(Transform, SOUND_TYPE), AudioSource> _activeLoopSounds = new Dictionary<(Transform, SOUND_TYPE), AudioSource>();
	// 3D 효과음 재생을 위한 오디오 소스 풀(Pool)
	private List<AudioSource> _sfx3DPool = new List<AudioSource>();
	// PlaySFX3DAtUnit으로 유닛에 부착된 단발성 소스 추적 (재생 끝나면 매니저로 unparent)
	private List<AudioSource> _pendingUnparentSources = new List<AudioSource>();
	// 타입별 현재 재생 중인 3D SFX 소스 추적 (폴리포니 제한 용도)
	private Dictionary<SOUND_TYPE, List<AudioSource>> _activeTypeSourceMap = new Dictionary<SOUND_TYPE, List<AudioSource>>();
	// (타입, 발사 주체) 조합별 마지막 재생 시각. minPlayInterval(최소 재생 간격) 체크용.
	// 발사 주체(unitTr)까지 키에 포함 — 유닛별로 따로 제한해야 여러 유닛이 같은 무기를 써도 서로 안 막음.
	private Dictionary<(SOUND_TYPE, Transform), float> _lastPlayTimeMap = new Dictionary<(SOUND_TYPE, Transform), float>();

	private void Awake()
	{
		// 싱글톤 기본 세팅 (씬이 넘어가도 파괴되지 않게 유지)
		if (instance == null)
		{
			instance = this;
			DontDestroyOnLoad(gameObject);
			EnsureInitialized();
		}
		else if (instance != this)
		{

			Debug.LogWarning("중복된 SoundManager 발견. 파괴 후 실행");
			Destroy(gameObject);
		}
	}

	private bool _initialized;

	// 사운드 목록 딕셔너리/풀 초기화. Awake와 Instance getter 양쪽에서 호출될 수 있어(실행 순서 무관하게
	// 안전하려면 둘 다 필요) 중복 실행 방지 플래그로 감쌈. 어느 쪽이 먼저 오든 딱 한 번만 실행됨.
	private void EnsureInitialized()
	{
		if (_initialized)
		{
			return;
		}
		_initialized = true;
		InitializeDictionary(); // 시작할 때 딕셔너리 세팅
		InitializeSFXPool();    // 시작할 때 3D 사운드 풀링 세팅
	}

	private void Update()
	{
		// BGM 플레이리스트 순환: 여러 곡 BGM에서 현재 곡이 끝나면 다음 곡(직전 곡 제외 랜덤) 재생.
		// (일시정지 중엔 BGM이 볼륨만 줄고 계속 재생되므로 isPlaying=true라 여기서 잘못 넘어가지 않음)
		if (_bgmRotating && _bgmSource != null && _bgmSource.clip != null && !_bgmSource.isPlaying)
		{
			PlayNextBGMClip();
		}

		// PlaySFX3DAtUnit으로 유닛에 부착됐던 단발성 소스 중 재생이 끝난 것을 매니저로 회수
		for (int i = _pendingUnparentSources.Count - 1; i >= 0; i--)
		{
			AudioSource source = _pendingUnparentSources[i];

			// 부착됐던 유닛이 파괴되면 이 소스도 같이 파괴됨 — .isPlaying 접근 전에 걸러야 예외가 안 남(해결책②).
			if (source == null)
			{
				Debug.LogWarning($"[SoundManager] 파괴된 AudioSource 감지 @Update(_pendingUnparentSources 인덱스 {i}) — 접근 전 제거함. (원인: 부착 유닛이 파괴되며 같이 파괴됨)");
				_pendingUnparentSources.RemoveAt(i);
				continue;
			}

			//재생중이면 아직 처리 안함
			if (source.isPlaying)
			{
				continue;
			}

			//대상이 파괴/비활성화 됐어도 매니저 자식으로 복귀시켜 풀에서 재사용 가능하게함
			source.transform.SetParent(this.transform);
			_pendingUnparentSources.RemoveAt(i);
		}
	}

	//인스펙터에 올린거 딕셔너리로 자동으로 옮겨 담는작업
	private void InitializeDictionary()
	{
		foreach (var item in _soundList)
		{
			// 중복 방지: 딕셔너리에 같은 키가 이미 있는지 확인
			if (!_soundDict.ContainsKey(item.type))
			{
				//클래스내의 타입멤버를 키로, 클래스자체를 값으로더하기
				_soundDict.Add(item.type, item);
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
		for (int i = 0; i < _initialPoolSize; i++)
		{
			CreateNewAudioSourceToPool();
		}
	}

	// 새로운 오디오 소스 생성 및 풀리스트에 추가
	private AudioSource CreateNewAudioSourceToPool()
	{
		GameObject go = new GameObject($"SFX_Pool_Speaker_{_sfx3DPool.Count}");//이름번호매기기
		go.transform.SetParent(this.transform); // 매니저의 자식 오브젝트로 정리

		AudioSource source = go.AddComponent<AudioSource>();
		source.spatialBlend = 1.0f; // 1.0 = 완전한 3D 사운드
		source.playOnAwake = false;
		source.dopplerLevel = 0f; // 도플러 효과 끔 — 빠르게 움직이는 유닛(미사일/부스트 등)에서 피치가 왜곡되며 소리가 찢어지는 현상 방지

		_sfx3DPool.Add(source);
		return source;
	}

	// 풀에서 사용 가능한(현재 재생 중이 아닌) 스피커를 찾아 반환
	private AudioSource GetAvailableSFX3DSource()
	{
		// 뒤에서부터 순회 — 파괴된(유닛과 함께 destroy된) 슬롯을 접근 전에 제거하기 위함(해결책②).
		for (int i = _sfx3DPool.Count - 1; i >= 0; i--)
		{
			// Unity의 == null은 destroy된 오브젝트도 true로 잡음. .isPlaying 접근 전에 먼저 걸러야 예외가 안 남.
			if (_sfx3DPool[i] == null)
			{
				Debug.LogWarning($"[SoundManager] 파괴된 AudioSource 감지 @GetAvailableSFX3DSource (풀 인덱스 {i}) — 접근 전 제거함. (원인: 스피커가 SetParent된 유닛이 파괴되며 같이 파괴됨)");
				_sfx3DPool.RemoveAt(i);
				continue;
			}
			if (!_sfx3DPool[i].isPlaying)
			{
				// dropOldest로 강제 Stop된 소스가 유닛 자식에 남아있을 수 있으므로 복귀 보장
				_sfx3DPool[i].transform.SetParent(this.transform);
				return _sfx3DPool[i];
			}
		}

		//모든 스피커가 사용 중일 경우, 새롭게 하나를 더 생성하여 반환
		return CreateNewAudioSourceToPool();
	}


	//private AudioClip GetClip(SOUND_TYPE type)
	//{
	//	if (_soundDict.TryGetValue(type, out AudioClip clip))
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
		if (type == SOUND_TYPE.SFX_NONE)
		{
			return null;
		}
		if (_soundDict.TryGetValue(type, out SoundTypeClip data))
		{
			return data;
		}
		Debug.LogWarning($"[SoundManager] {type} 데이터 누락됨");
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
	//	foreach (var item in _soundList)
	//	{
	//		if (item.clip == targetClip) return item.volumeScale;
	//	}
	//	return 1.0f;
	//}






	// 폴리포니 체크 후 재생 가능한 소스 반환. maxConcurrent 초과 + dropOldest=false이면 null 반환(재생 스킵).
	// sourceUnit: 발사 주체(유닛). minPlayInterval 체크를 유닛별로 따로 적용하기 위함 — null이면(위치 기반 1회성 사운드 등) 체크 생략.
	// bypassPolyphony: 루프 사운드(PlaySFX3DLoop)용 — 유닛당 1개라는 보장은 _activeLoopSounds 딕셔너리가 이미 하고 있어서,
	// 1회성 사운드용 maxConcurrent/dropOldest를 또 거치면 유닛 구분 없이 "가장 오래된" 다른 유닛의 루프를 멋대로 꺼버리는 문제가 있었음.
	private AudioSource AcquireSFX3DSource(SOUND_TYPE type, SoundTypeClip data, Transform sourceUnit = null, bool bypassPolyphony = false)
	{
		if (bypassPolyphony)
		{
			return GetAvailableSFX3DSource();
		}

		// 최소 재생 간격(minPlayInterval) 체크 — 같은 유닛이 마지막 재생 후 이 시간 안에 또 호출하면 스킵.
		var throttleKey = (type, sourceUnit);
		if (sourceUnit != null && data.minPlayInterval > 0f
			&& _lastPlayTimeMap.TryGetValue(throttleKey, out float lastTime)
			&& Time.time < lastTime + data.minPlayInterval)
		{
			return null;
		}

		if (!_activeTypeSourceMap.ContainsKey(type))
		{
			_activeTypeSourceMap[type] = new List<AudioSource>();
		}
		List<AudioSource> active = _activeTypeSourceMap[type];

		// 재생 완료된 소스 정리 (파괴된 소스도 == null로 함께 걸러짐 — 해결책②)
		for (int i = active.Count - 1; i >= 0; i--)
		{
			if (active[i] == null)
			{
				Debug.LogWarning($"[SoundManager] 파괴된 AudioSource 감지 @AcquireSFX3DSource (type={type}, active 인덱스 {i}) — 접근 전 제거함. (원인: 부착 유닛이 파괴되며 같이 파괴됨)");
				active.RemoveAt(i);
				continue;
			}
			if (!active[i].isPlaying)
			{
				active.RemoveAt(i);
			}
		}

		if (data.maxConcurrent > 0 && active.Count >= data.maxConcurrent)
		{
			if (data.dropOldest)
			{
				// 가장 오래된 것(리스트 맨 앞) 중단
				//Debug.Log($"[SoundManager-DEBUG] dropOldest로 강제정지: type={type}, maxConcurrent={data.maxConcurrent}, 정지대상parent={active[0]?.transform.parent?.name}, 정지대상clip={active[0]?.clip?.name}, 새로요청한sourceUnit={sourceUnit?.name}");
				active[0].Stop();
				active.RemoveAt(0);
			}
			else
			{
				return null;
			}
		}

		AudioSource source = GetAvailableSFX3DSource();
		active.Add(source);
		if (sourceUnit != null)
		{
			_lastPlayTimeMap[throttleKey] = Time.time;
		}
		return source;
	}

	// SoundTypeClip 피치 설정 적용. pitchMin == pitchMax이면 고정값 그대로 반환.
	private float GetPitch(SoundTypeClip data)
	{
		if (data.pitchMin != data.pitchMax)
		{
			return Random.Range(data.pitchMin, data.pitchMax);
		}
		return data.pitchMin;
	}

	// clips 배열에서 랜덤으로 하나 선택. 1개뿐이면 그대로 반환, 비어있으면 null.
	private AudioClip GetRandomClip(SoundTypeClip data)
	{
		if (data.clips == null || data.clips.Length == 0)
		{
			Debug.LogWarning($"[SoundManager] {data.type} 클립 배열이 비어있음");
			return null;
		}
		if (data.clips.Length == 1)
		{
			return data.clips[0];
		}
		return data.clips[Random.Range(0, data.clips.Length)];
	}

	// SoundTypeClip 볼륨 설정 적용. volumeMin == volumeMax이면 volumeScale 고정값 반환.
	private float GetVolume(SoundTypeClip data)
	{
		if (data.volumeMin != data.volumeMax)
		{
			return Random.Range(data.volumeMin, data.volumeMax);
		}
		return data.volumeScale;
	}

	#region 외부 호출용
	// ================== [실제 사용되는 재생 함수들] ==================



	// PlayBGM(사운드 열거형) BGM재생
	// PlaySFX(사운드 열거형) UI 클릭,주사위굴리기 등 화면전체에서 들려야하는 2d사운드재생
	// PlaySFXAtPosition(사운드 열거형, 좌표) 이동,총알,폭발등등 3d사운드 재생




	/// <summary>
	/// BGM재생
	/// </summary>
	/// <param name="type">사운드타입 입력</param>
	public void PlayBGM(SOUND_TYPE type)
	{
		SoundTypeClip data = GetSoundData(type);
		if (data != null && data.type != SOUND_TYPE.SFX_NONE)
		{
			curBGM = type;
			_curBgmData = data;
			// 클립이 여러 개면 '플레이리스트 순환'(loop=false → Update가 곡 끝나면 다음 곡). 1개면 무한루프.
			_bgmRotating = data.clips != null && data.clips.Length > 1;
			_lastBgmClipIndex = -1;
			_bgmSource.clip = PickBGMClip(data);
			_bgmSource.loop = !_bgmRotating;
			_bgmSource.Play();
			ApplyBGMVolume();
		}
	}

	// clips에서 다음 재생 클립 선택. 여러 개면 직전 곡(_lastBgmClipIndex)을 제외한 랜덤(연속 중복 방지).
	private AudioClip PickBGMClip(SoundTypeClip data)
	{
		if (data.clips == null || data.clips.Length == 0)
		{
			return null;
		}
		if (data.clips.Length == 1)
		{
			_lastBgmClipIndex = 0;
			return data.clips[0];
		}
		int index;
		do
		{
			index = Random.Range(0, data.clips.Length);
		}
		while (index == _lastBgmClipIndex);
		_lastBgmClipIndex = index;
		return data.clips[index];
	}

	// 플레이리스트 순환 — 현재 BGM 세트에서 다음 곡으로 교체. Update가 곡 종료를 감지해 호출.
	private void PlayNextBGMClip()
	{
		if (_curBgmData == null)
		{
			return;
		}
		_bgmSource.clip = PickBGMClip(_curBgmData);
		_bgmSource.Play();
		ApplyBGMVolume();
	}

	// BGM 실제 볼륨 = 설정볼륨 × 클립볼륨 × 페이드배율 × (일시정지면 pauseAllVolumeScale × pauseBGMVolumeScale).
	// 일시정지 시 전체 1차 감쇠(pauseAllVolumeScale)에 BGM만 추가로 한 번 더 감쇠(pauseBGMVolumeScale)를 곱해
	// 세밀 조정한다 — SFX/엔진음은 pauseAllVolumeScale만 적용(SfxPauseScale 참고).
	// 볼륨을 바꾸는 모든 경로(설정/페이드/일시정지/곡교체)가 이 한 곳을 거치게 해 합성 일관성 유지.
	private void ApplyBGMVolume()
	{
		if (_bgmSource == null)
		{
			return;
		}
		float clipVol = _curBgmData != null ? GetVolume(_curBgmData) : 1f;
		float pauseScale = _bgmAtGamePaused ? (pauseAllVolumeScale * pauseBGMVolumeScale) : 1f;
		_bgmSource.volume = bgmVolume * clipVol * _bgmFadeFactor * pauseScale;
	}

	// 일시정지 볼륨 감쇠 적용/해제. GameManager.PauseGame/ResumeGame에서 호출.
	public void SetBGMAtGamePaused(bool paused)
	{
		_bgmAtGamePaused = paused;
		ApplyBGMVolume();
	}

	public void SetAllVolumeAtGamePaused(bool paused)
	{
		_allVolumeAtGamePaused = paused;
		//bgm
		SetBGMAtGamePaused(paused);

		// UI SFX(메뉴 클릭음 등)는 일시정지 중에도 들려야 하므로 감쇠하지 않음.

		// 3D SFX — 새로 시작되는 소리와 엔진 루프는 재생/재계산 경로에서 SfxPauseScale이 곱해져 자동 처리
		// 여기서는 이미 재생중이던 단발음을 배율만큼 곱하거나(감쇠) 나눠서(복원) 개별 volumeScale을 보존
		float scale = pauseAllVolumeScale;
		if (scale <= 0f)
		{
			return; // 0 이하면 나눗셈 복원이 불가 — 감쇠 자체를 건너뜀(안전).
		}
		for (int i = _sfx3DPool.Count - 1; i >= 0; i--)
		{
			AudioSource source = _sfx3DPool[i];
			if (source == null)
			{
				_sfx3DPool.RemoveAt(i);
				continue;
			}
			if (!source.isPlaying)
			{
				continue;
			}
			source.volume = paused ? source.volume * scale : source.volume / scale;
		}
	}

	// 보스 등장 등 상태 전환 시 BGM 교체(현재 곡 페이드아웃 → 새 BGM → 페이드인).
	public void ChangeBGMWithFade(SOUND_TYPE newType, float duration)
	{
		if (_bgmFadeRoutine != null)
		{
			StopCoroutine(_bgmFadeRoutine);
		}
		_bgmFadeRoutine = StartCoroutine(ChangeBGMRoutine(newType, duration));
	}

	private IEnumerator ChangeBGMRoutine(SOUND_TYPE newType, float duration)
	{
		float half = Mathf.Max(0.01f, duration * 0.5f);
		yield return FadeBGMFactorRoutine(_bgmFadeFactor, 0f, half); // 현재 곡 페이드아웃
		PlayBGM(newType);                                            // 새 BGM 교체(_bgmFadeFactor=0이라 무음 시작)
		yield return FadeBGMFactorRoutine(0f, 1f, half);             // 새 곡 페이드인
		_bgmFadeRoutine = null;
	}

	private IEnumerator FadeBGMFactorRoutine(float from, float to, float duration)
	{
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			SetBGMFadeFactor(Mathf.Lerp(from, to, elapsed / duration));
			yield return null;
		}
		SetBGMFadeFactor(to);
	}
	// 사용예
	//private void Start()
	//{
	//	// 게임 시작(또는 로비 씬 로드) 시 로비 BGM 재생
	//	SoundManager.Instance.PlayBGM(SOUND_TYPE.BGM_LOBBY);
	//}


	/// <summary>
	/// UI 클릭, 주사위 굴리기 등 화면 전체에서 들려야 하는 2D 효과음
	/// </summary>
	/// <param name="type"></param>
	public void PlaySFXUI(SOUND_TYPE type)
	{
		SoundTypeClip data = GetSoundData(type);
		if (data != null && data.type != SOUND_TYPE.SFX_NONE)
		{

			_sfxUISource.pitch = 1.0f; // 기본 피치로 초기화
			_sfxUISource.PlayOneShot(GetRandomClip(data), sfxUIVolume * GetVolume(data));
		}
	}
	// 사용예
	// SoundManager.Instance.PlaySFX(SOUND_TYPE.SFX_DICE_ROLL);



	/// <summary>
	/// 단조로움을 방지하기 위해 랜덤한 피치(직접 입력)로 2D 효과음 재생
	/// </summary>
	/// <param name="type"></param>
	/// <param name="pitchMin"></param>
	/// <param name="pitchMax"></param>
	// 사용 예: PlaySFX(SOUND_TYPE.SFX_SHOOT, 0.9f, 1.1f);
	public void PlaySFXUI(SOUND_TYPE type, float pitchMin, float pitchMax)
	{
		SoundTypeClip data = GetSoundData(type);
		if (data != null && data.type != SOUND_TYPE.SFX_NONE)
		{

			_sfxUISource.pitch = Random.Range(pitchMin, pitchMax);
			_sfxUISource.PlayOneShot(GetRandomClip(data), sfxUIVolume * GetVolume(data));

		}
	}


	/// <summary>
	/// 피격음등 특정 위치에서 나야 하는 단발성 3D 효과음을 재생
	///  타입과 좌표받기
	/// </summary>
	/// <param name="type"></param>
	/// <param name="position"></param>

	public void PlaySFX3DAtPosition(SOUND_TYPE type, Vector3 position)
	{
		SoundTypeClip data = GetSoundData(type);
		//Debug.Log($"[PlaySFX3DAtPosition-DEBUG] type={type}, data!=null={data != null}");
		if (data != null && data.type != SOUND_TYPE.SFX_NONE)
		{

			// 지정된 위치에 임시 스피커를 만들고, 소리가 끝나면 알아서 삭제됨
			AudioSource source = AcquireSFX3DSource(type, data);
			//Debug.Log($"[PlaySFX3DAtPosition-DEBUG] source!=null={source != null}, clipsCount={data.clips?.Length ?? 0}, volumeScale={data.volumeScale}");
			if (source == null) { return; }
			source.transform.position = position;
			source.clip = GetRandomClip(data);

			source.minDistance = data.minDistance;
			source.maxDistance = data.maxDistance;
			source.volume = sfx3DVolume * GetVolume(data) * SfxPauseScale;
			source.pitch = GetPitch(data);
			source.loop = false;
			//Debug.Log($"[PlaySFX3DAtPosition-DEBUG] clip!=null={source.clip != null}, clipName={source.clip?.name}, finalVolume={source.volume}, sfx3DVolume={sfx3DVolume}, minDist={source.minDistance}, maxDist={source.maxDistance}, sourcePos={source.transform.position}, mute={source.mute}");
			source.Play();
			//Debug.Log($"[PlaySFX3DAtPosition-DEBUG] isPlaying={source.isPlaying}, gameObjectActive={source.gameObject.activeInHierarchy}");

		}
	}
    //사용예
    //Soundmanager.Instance.PlaySFXAtPosition(SOUND_TYPE.SFX_SHOOT, transform.position);

    /// <summary>
    /// 피격음등 특정 위치에서 나야 하는 단발성 3D 효과음을 재생(피치랜덤 직접설정)
    /// </summary>
    /// <param name="type"></param>
    /// <param name="position"></param>
    /// <param name="pitchMin"></param>
    /// <param name="pitchMax"></param>

    public void PlaySFX3DAtPosition(SOUND_TYPE type, Vector3 position, float pitchMin, float pitchMax)
	{
		SoundTypeClip data = GetSoundData(type);
		if (data != null && data.type != SOUND_TYPE.SFX_NONE)
		{

			// 지정된 위치에 임시 스피커를 만들고, 소리가 끝나면 알아서 삭제됨
			AudioSource source = AcquireSFX3DSource(type, data);
			if (source == null) { return; }
			source.transform.position = position;
			source.clip = GetRandomClip(data);
			source.minDistance = data.minDistance;
			source.maxDistance = data.maxDistance;
			source.volume = sfx3DVolume * GetVolume(data) * SfxPauseScale;

			source.pitch = Random.Range(pitchMin, pitchMax);
			source.loop = false;
			source.Play();

		}
	}

	/// <summary>
	/// 발사음등 특정 유닛이 단발로 재생할 사운드
	/// </summary>
	/// <param name="type"></param>
	/// <param name="position"></param>
    // unitTr : 부착(SetParent) 대상. 유닛 루트처럼 파츠보다 오래 사는 안전한 transform을 넘길 것.
    // playPos : 실제 재생 위치(좌표만 사용). 총구 등 파츠의 자식 transform을 넘기면 그 위치에서 재생됨.
    //           생략(null) 시 unitTr 위치에서 재생 (기존 Idle/Moving/Boost 루프 사운드 호출 방식과 동일).
    public void PlaySFX3DAtUnit(SOUND_TYPE type, Transform unitTr, Transform playPos = null)
    {
        SoundTypeClip data = GetSoundData(type);
        if (data != null && data.type != SOUND_TYPE.SFX_NONE)
        {
            Transform posSource = (playPos != null) ? playPos : unitTr;

            // 지정된 위치에 임시 스피커를 만들고, 소리가 끝나면 알아서 삭제됨
            // unitTr을 발사 주체로 넘겨서 minPlayInterval이 유닛별로 적용되게 함 (다른 유닛이 같은 사운드 써도 안 막히도록)
            AudioSource source = AcquireSFX3DSource(type, data, unitTr);
            if (source == null)
            {
                //Debug.Log($"[SoundManager-DEBUG] PlaySFX3DAtUnit 스킵됨(소스확보 실패): type={type}, unit={unitTr?.name}, clipsCount={data.clips?.Length ?? 0}, maxConcurrent={data.maxConcurrent}, minPlayInterval={data.minPlayInterval}");
                return;
            }
            //좌표일치 (재생 위치 = 총구 등 playPos 기준)
            source.transform.position = posSource.position;
            //유닛(생명주기 안전한 대상)에 이 오디오소스 붙이기(지속재생용)
            //주의: playPos(총구 등 파츠 자식)에 직접 붙이면, 재생 도중 파츠가 교체/파괴될 때
            //같이 파괴되어 풀 손실 + _pendingUnparentSources에서 파괴된 참조 접근 문제가 생길 수 있음.
            source.transform.SetParent(unitTr);
            source.clip = GetRandomClip(data);

            source.minDistance = data.minDistance;
            source.maxDistance = data.maxDistance;
            source.volume = sfx3DVolume * GetVolume(data) * SfxPauseScale;
            source.pitch = GetPitch(data);
            source.loop = false;
            source.Play();
            //Debug.Log($"[SoundManager-DEBUG] PlaySFX3DAtUnit 재생: type={type}, unit={unitTr?.name}, clip={source.clip?.name}, pitch={source.pitch:F2}, volume={source.volume:F2}, source={source.GetInstanceID()}");

            //재생 끝나면 Update에서 매니저로 unparent 처리
            _pendingUnparentSources.Add(source);

        }
    }


    /// <summary>
    /// 상태에 따른 루프 사운드 사용시(ex 부스터 사운드)
    /// </summary>
    /// <param name="type"></param>
    /// <param name="targetTr"></param>

    // 이미 같은 (타겟, 타입) 조합으로 재생 중이면 그 AudioSource를 그대로 반환 — 호출한 쪽이 참조를
    // 들고 매 프레임 volume/pitch를 직접 조절할 수 있음(엔진음 레이어 크로스페이드 등에 사용).
    // startVolume: 지정 안 하면(null) 기존처럼 SoundManager 등록볼륨으로 시작. 0f 등을 넘기면 그 값으로 시작
    // (예: Unit.cs 엔진사운드처럼 Play() 직후 자기 로직으로 볼륨을 다시 잡는 경우, 새어나가는 소리 방지용).
    public AudioSource PlaySFX3DLoop(SOUND_TYPE type, Transform targetTr, float? startVolume = null)
	{
		var key = (targetTr, type);

		//이미 같은 조합으로 재생 중이면 기존 소스 그대로 반환
		if (_activeLoopSounds.TryGetValue(key, out AudioSource existing))
		{
			//Debug.Log($"[SoundManager-DEBUG] PlaySFX3DLoop 기존소스 반환: type={type}, target={targetTr?.name}, existing!=null={existing != null}, existing.isPlaying={existing != null && existing.isPlaying}");
			return existing;
		}

		//데이타갖고오기
		SoundTypeClip data = GetSoundData(type);
		if (data == null || data.type == SOUND_TYPE.SFX_NONE)
		{
			//Debug.Log($"[SoundManager-DEBUG] PlaySFX3DLoop 등록실패(data없음/SFX_NONE): type={type}, target={targetTr?.name}, data==null={data == null}");
			return null;
		}

		//루프 사운드는 유닛당 1개 보장이 이미 _activeLoopSounds로 돼있어 폴리포니 체크 생략(bypassPolyphony=true) —
		//안 그러면 maxConcurrent 초과 시 다른 유닛의 루프가 dropOldest로 멋대로 꺼지는 문제가 있었음.
		AudioSource source = AcquireSFX3DSource(type, data, targetTr, bypassPolyphony: true);
		if (source == null)
		{
			//Debug.Log($"[SoundManager-DEBUG] PlaySFX3DLoop 소스확보실패: type={type}, target={targetTr?.name}, clipsCount={data.clips?.Length ?? 0}");
			return null;
		}
		//좌표일치
		source.transform.position = targetTr.position;
		//해당 타겟에 이 오디오소스 붙이기(지속재생용)
		source.transform.SetParent(targetTr);

		source.clip = GetRandomClip(data);
		source.volume = startVolume ?? (sfx3DVolume * GetVolume(data));
		source.minDistance = data.minDistance;
		source.maxDistance = data.maxDistance;
		source.pitch = GetPitch(data);

		source.loop = true;
		source.Play();
		//Debug.Log($"[SoundManager-DEBUG] PlaySFX3DLoop 신규생성: type={type}, target={targetTr?.name}, clip={source.clip?.name}, startVolume={source.volume:F3}, isPlaying={source.isPlaying}");

		_activeLoopSounds.Add(key, source);
		return source;
	}


	// =====================================================================
	// 엔진 루프음(공회전/가속/부스트) 크로스페이드 갱신.
	// Unit.cs가 매 프레임 speedRatio(0~1)/isBoosting/mute만 계산해서 넘기고,
	// 실제 볼륨·피치 곡선은 engineSoundConfig 기준으로 여기서 전부 처리(RTPC 스타일 분리).
	// 대상 AudioSource는 PlaySFX3DLoop()로 이미 걸어둔 _activeLoopSounds에서 직접 조회.
	// =====================================================================
	private HashSet<Transform> _engineLoopMissingWarned = new HashSet<Transform>();

	public void UpdateEngineLoopVolumes(Transform targetTr, float speedRatio, bool isBoosting, bool mute = false)
	{
		_activeLoopSounds.TryGetValue((targetTr, SOUND_TYPE.SFX_IDLE), out AudioSource idle);
		_activeLoopSounds.TryGetValue((targetTr, SOUND_TYPE.SFX_MOVING), out AudioSource thrust);
		_activeLoopSounds.TryGetValue((targetTr, SOUND_TYPE.SFX_BOOST), out AudioSource boost);

		// 셋 다 못 찾으면(루프가 애초에 등록 안 됐으면) 매 프레임 대신 유닛당 1번만 경고
		if (idle == null && thrust == null && boost == null && !_engineLoopMissingWarned.Contains(targetTr))
		{
			_engineLoopMissingWarned.Add(targetTr);
			Debug.Log($"[SoundManager-DEBUG] UpdateEngineLoopVolumes 루프없음: target={targetTr?.name} — _activeLoopSounds에 이 유닛의 SFX_IDLE/MOVING/BOOST가 전혀 등록 안 돼있음");
		}

		if (mute)
		{
			if (idle != null) idle.volume = 0f;
			if (thrust != null) thrust.volume = 0f;
			if (boost != null) boost.volume = 0f;
			return;
		}

		speedRatio = Mathf.Clamp01(speedRatio);

		// 엔진 루프도 3D SFX 카테고리 — 마스터(sfx3DVolume)와 일시정지 배율(SfxPauseScale)을 함께 반영.
		float sfxScale = sfx3DVolume * SfxPauseScale;

		if (idle != null)
		{
			idle.volume = Mathf.Lerp(engineSoundConfig.idleMaxVolume, 0f, speedRatio) * sfxScale;
		}
		if (thrust != null)
		{
			thrust.volume = Mathf.Lerp(0f, engineSoundConfig.thrustMaxVolume, speedRatio) * sfxScale;
			thrust.pitch = Mathf.Lerp(engineSoundConfig.thrustMinPitch, engineSoundConfig.thrustMaxPitch, speedRatio);
		}
		if (boost != null)
		{
			float targetVolume = (isBoosting ? engineSoundConfig.boostMaxVolume : 0f) * sfxScale;
			boost.volume = Mathf.MoveTowards(boost.volume, targetVolume, Time.deltaTime * engineSoundConfig.boostFadeSpeed);
		}
	}

	// ================== [정지 함수들] ==================


	// 재생 중인 BGM 정지

	public void StopBGM()
	{
		_bgmRotating = false; // 순환 중지(Update가 다시 재생하지 않게)
		if (_bgmSource.isPlaying)
		{
			_bgmSource.Stop();
		}
	}

	
	// 3D 루프 사운드 정지 및 회수

	public void StopSFX3DLoop(SOUND_TYPE type, Transform targetTr)
	{
		var key = (targetTr, type);
		//해당 (트랜스폼,타입) 조합으로 재생 중인 루프 사운드가 있는지 확인 및 가져오기
		if (_activeLoopSounds.TryGetValue(key, out AudioSource source))
		{
			//source가 이미 파괴된 상태일 수 있음(예외 시에도 키는 반드시 제거해야 캐스케이드 방지)
			if (source != null)
			{
				//사운드 재생 정지
				source.Stop();

				//풀링 시스템 재사용 시 설정에러를 예방 루프 해제
				source.loop = false;

				// 여기서 SetParent로 즉시 원상복구하지 않음 — 이 함수는 대상 유닛의 OnDisable()에서
				// 호출되는데, 그 유닛이 SetActive(false)로 비활성화되는 도중이면 Unity가 그 자식의
				// reparent를 막아서 에러가 남. 실제 reparent는 GetAvailableSFX3DSource()가 이 소스를
				// 다음에 재사용할 때 처리(이미 그쪽에 동일한 복귀 로직이 있음).
			}

			//루프 사운드 추적 딕셔너리에서 해당 항목 제거
			_activeLoopSounds.Remove(key);
		}
	}
	// 모든 사운드(BGM 및 2D SFX) 정지

	// StopSFXAll은 씬 전환/게임오버 등 teardown에서만 호출됨(GameManager.LoadSceneRoutine/GameOver).
	// 여기서 풀 스피커를 전부 정지 + 매니저 자식으로 '회수(reparent home)'한다 — 이게 예방(옵션①)의 핵심:
	// 회수해두면 직후 씬 언로드로 유닛이 파괴돼도 스피커가 자식으로 딸려 죽지 않아, DDOL 매니저에
	// 파괴된 참조가 남는 문제 자체가 안 생긴다. 유닛에 매여있던 추적 정보도 같이 초기화(다음 씬에서 새로 등록).
	public void StopSFXAll()
	{
		_bgmRotating = false; // BGM 순환 중지(Update가 다시 재생하지 않게)
		// 진행 중인 보스 BGM 페이드 전환이 있으면 취소(씬 전환과 겹쳐 새 씬에서 엉키지 않게)
		if (_bgmFadeRoutine != null)
		{
			StopCoroutine(_bgmFadeRoutine);
			_bgmFadeRoutine = null;
		}
		_bgmSource.Stop();
		_sfxUISource.Stop();

		// 풀링된 3D 스피커 전부 정지 + 회수. 뒤에서부터 순회 — 파괴된 슬롯은 접근 전에 제거(값싼 안전망②).
		for (int i = _sfx3DPool.Count - 1; i >= 0; i--)
		{
			AudioSource speaker = _sfx3DPool[i];
			if (speaker == null)
			{
				Debug.LogWarning($"[SoundManager] 파괴된 AudioSource 감지 @StopSFXAll (풀 인덱스 {i}) — 접근 전 제거함. (원인: 스피커가 SetParent된 유닛이 파괴되며 같이 파괴됨)");
				_sfx3DPool.RemoveAt(i);
				continue;
			}
			if (speaker.isPlaying)
			{
				speaker.Stop();
			}
			speaker.loop = false;
			speaker.transform.SetParent(this.transform); // 유닛에서 떼어 매니저(DDOL)로 회수 → 씬 언로드 때 안 죽음
		}

		// 유닛에 매여있던 추적 정보 초기화 — 다음 씬 유닛들이 새로 등록함.
		_activeLoopSounds.Clear();
		_pendingUnparentSources.Clear();
		_activeTypeSourceMap.Clear();
		_lastPlayTimeMap.Clear();
		_engineLoopMissingWarned.Clear();
	}

	// ================== [실시간 볼륨 조절 함수 (UI 옵션 창 연동용)] ==================

	// 차후 UI팀이 환경설정 창의 슬라이더(OnValueChanged)에 연결할 함수


	public void SetBGMVolume(float volume)
	{
		bgmVolume = volume;//입력한 볼륨값 현재설정에 저장
		ApplyBGMVolume();
	}

	// 씬 전환 페이드 연출용 — 저장된 볼륨 설정(bgmVolume)은 건드리지 않고 일시적으로 배율(fadeFactor)만 곱해 적용.
	// GameManager의 FadeOut/FadeIn 루프가 화면 알파와 같은 진행도로 이걸 호출해 BGM을 같이 줄였다/늘렸다 함.
	// fadeFactor: 0(무음) ~ 1(설정 볼륨 그대로). 상태를 저장하지 않으므로(매 호출 재계산) PlayBGM 이후엔 자동으로 1 기준으로 복귀.
	public void SetBGMFadeFactor(float fadeFactor)
	{
		_bgmFadeFactor = Mathf.Clamp01(fadeFactor);
		ApplyBGMVolume();
	}

	public void SetSFXUIVolume(float volume)
	{
		// 실제 반영은 PlayOneShot 시 (sfxUIVolume × 개별볼륨)으로 적용됨. 여기서 소스 자체 볼륨에
		// 또 sfxUIVolume을 걸면 이중 적용되므로, 소스 배율은 1로 고정한다.
		sfxUIVolume = volume;//입력한 볼륨값 현재설정에 저장
		if (_sfxUISource != null)
		{
			_sfxUISource.volume = 1f;
		}
	}

	public void SetSFX3DVolume(float volume)
	{
		float prev = sfx3DVolume;
		sfx3DVolume = volume;//입력한 볼륨값 현재설정에 저장
		if (prev <= 0f)
		{
			return; // 이전 마스터가 0이면 소스별 개별볼륨 비율을 복원할 수 없음 — 다음 재생부터 새 값 반영.
		}
		float ratio = volume / prev;
		// 뒤에서부터 순회 — 파괴된 슬롯을 접근 전에 제거하기 위함
		for (int i = _sfx3DPool.Count - 1; i >= 0; i--)
		{
			AudioSource source = _sfx3DPool[i];
			if (source == null)
			{
				Debug.LogWarning($"[SoundManager] 파괴된 AudioSource 감지 @SetSFX3DVolume (풀 인덱스 {i}) — 접근 전 제거함. (원인: 스피커가 SetParent된 유닛이 파괴되며 같이 파괴됨)");
				_sfx3DPool.RemoveAt(i);
				continue;
			}
			if (source.isPlaying)//혹여나 실행되고있는게있따면
			{
				// 마스터 변경분만큼 비율로 조정 — 개별 volumeScale·일시정지 배율을 그대로 보존한다.
				source.volume *= ratio;
			}
		}
	}


	#endregion

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