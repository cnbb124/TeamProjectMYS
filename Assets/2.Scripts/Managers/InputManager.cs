using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//플레이어의 키보드/마우스 입력을 매 프레임 수집하여 저장하는 싱글톤 매니저.
// 다른 클래스에서 InputManager.Instance.필드명 으로 접근해서 사용.
//모든 키입력, 마우스입력 담당

//차후에 추가
public class InputManager : MonoBehaviour
{
	//싱글톤 매니저 설정
	private static InputManager instance = null;
	public static InputManager Instance
	{
		get
		{
			if (instance == null)
			{
				instance = FindObjectOfType<InputManager>();
				if (instance == null)
				{
					Debug.LogError("씬에 InputManager 누락! 하이어라키에 인풋매니저필요 필요");
				}
			}
			return instance;
		}
	}




	[Header("<size=18>사용시 InputManager.Instance.필드 명</size>\n\n" +
	"플레이어 스크립트 등에서 사용\n" +
	"\n" +
	"====== 이동 및 시야 조작 ======\n" +
	"W / S : 전진 및 후진 (moveInput.z)\n" +
	"A / D : 좌우 이동 (moveInput.x)\n" +
	"Mouse4 / 3 : 수직 상승 및 하강 (moveInput.y)\n" +
	"Q / E : 기체 좌우 롤 회전 (rollInput)\n" +
	"마우스 이동 : 시야 조작 (lookInput)\n" +
	"LeftShift : 부스트 (isBoosting, 누르는 동안)\n" +
	"Space : 회피 (isDodging, 누른 순간)\n" +
	"\n" +
	"====== 무기 및 공격 조작 ======\n" +
	"마우스 좌클릭 : 총알 발사 (fireBullet, 누르는 동안)\n" +
	"마우스 우클릭 : 미사일 발사 (fireMissile, 누른 순간)\n" +
	"F : 레이저 발사 (fireLaser, 누른 순간)\n" +
	"V : 전체 무기 동시 발사 (fireAll, 누른 순간)\n" +
	"숫자 1 / 3 : 좌우 미사일 장착 토글 (equipMissileL/R, 누른 순간)")]
	

	//==========================이동관련 조작=====================
	[Header("이동 입력")]
	[Tooltip("WASD + 차후 결정상하키 입력값. X=좌우, Y=상하, Z=전후.")]
	//GetAxis?보간있  GetAxisRaw?보간없 뭘로받아올지
	public Vector3 moveInput;
	// W/S          → Z축 전후
	// A/D          → X축 좌우
	// Mouse4(앞으로) → Y축 상승
	// Mouse3(뒤로)   → Y축 하강
	// EQ 말고 마우스 버튼 상하로? eq는 선회하도록


	[Header("회전 입력")]
	[Tooltip("마우스 이동량. X=좌우, Y=상하 Pitch")]
	public Vector2 lookInput;
	[Tooltip("Z축기준 회전(전진방향기준 좌우회전) Q=좌 E=우")]
	public float rollInput;
	// Q → -1f (좌 롤)
	// E → +1f (우 롤)

	[Tooltip("부스트 키(LeftShift) 누르는 동안 true")]
	public bool isBoosting;//부스트사용유뮤ㅜ
	[Tooltip("닷지 키(Space) 누른 순간 한 프레임만 true")]
	public bool isDodging;//회피사용유무

	//========================사격관련 조작========================

	[Header("사격 입력")]
	[Tooltip("총알 발사. 마우스 좌클릭 누르는 동안 true")]
	public bool fireBullet;

	[Tooltip("미사일 발사. 마우스 우클릭 누른 순간 한 프레임만")]
	public bool fireMissile;

	[Tooltip("미사일 장착/해제. 1(좌),3(우)버튼 누른순간 한프레임")]
	public bool equipMissileL;
	public bool equipMissileR;


	[Tooltip("레이저 발사. F 누르면  true")]
	public bool fireLaser;

	[Tooltip("전체 무기 동시 발사. V 누른 순간 한 프레임만 true")]
	public bool fireAll;




	private void Awake()
	{
		// 싱글톤 기본 세팅 (씬이 넘어가도 파괴되지 않게 유지)
		if (instance == null)
		{
			instance = this;
			DontDestroyOnLoad(gameObject);

		}
		else if (instance != this)
		{

			Debug.LogWarning("중복된 InputManager 발견. 파괴 후 실행");
			Destroy(gameObject);
		}
	}
	// Start is called before the first frame update
	void Start()
	{

	}

	// Update is called once per frame

	//	입력 코드   대응하는 마우스 버튼
	//KeyCode.Mouse0  왼쪽 마우스 버튼 (LMB)
	//KeyCode.Mouse1 오른쪽 마우스 버튼 (RMB)
	//KeyCode.Mouse2 휠 클릭 버튼 (MMB)
	//KeyCode.Mouse3 보조 버튼 1 (측면 하단/뒤로)
	//KeyCode.Mouse4 보조 버튼 2 (측면 상단/앞으로)
	//KeyCode.Mouse5 ~6	추가적인 특수 버튼(마우스 사양에 따라 다름)

	void Update()
	{
		//이동조작
		moveInput = new Vector3(Input.GetAxisRaw("Horizontal"),//키보드  A=-1, D=1
			(Input.GetKey(KeyCode.Mouse4) ? 1f : 0f) + (Input.GetKey(KeyCode.Mouse3) ? -1f : 0f),//수직이동 마우스 34,
			Input.GetAxisRaw("Vertical")); //키보드 // S=-1, W=1

		//선체 회전 (롤): Q=좌(-1), E=우(+1), 동시입력 시 상쇄되어 0
		rollInput = (Input.GetKey(KeyCode.E) ? 1f : 0f) + (Input.GetKey(KeyCode.Q) ? -1f : 0f);


		//부스트(꾹)
		isBoosting = Input.GetKey(KeyCode.LeftShift);
		//회피
		isDodging = Input.GetKeyDown(KeyCode.Space);

		//시야조작
		lookInput = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));



		// 사격
		// GetKey  = 누르는 동안 (총알 연사 가능성 고려. 추후 연사속도는 Player에서 제어)
		// GetKeyDown = 누른 순간만 (미사일/레이저/전체는 토글/즉발 개념)
		fireBullet = Input.GetKey(KeyCode.Mouse0);

		fireMissile = Input.GetKeyDown(KeyCode.Mouse1);
		fireLaser = Input.GetKeyDown(KeyCode.F);
		fireAll = Input.GetKeyDown(KeyCode.V);

		// 미사일 슬롯 장착/해제 토글 (Player에서 토글 로직 처리)
		equipMissileL = Input.GetKeyDown(KeyCode.Alpha1);
		equipMissileR = Input.GetKeyDown(KeyCode.Alpha3);

		


	}
}
