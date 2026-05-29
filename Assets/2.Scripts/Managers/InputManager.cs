using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// 조작 방식 선택 열거형
// 인스펙터 [조작 방식 선택] 드롭다운에서 선택
// =====================================================================


// =====================================================================
// 키보드/마우스 키 설정
// 인스펙터에서 각 키를 드롭다운으로 변경 가능
// =====================================================================
[System.Serializable]
public class KeyboardMouseConfig
{
    [Header("이동")]
    public KeyCode moveUp    = KeyCode.Mouse4;    // 수직 상승
    public KeyCode moveDown  = KeyCode.Mouse3;    // 수직 하강
    public KeyCode rollLeft  = KeyCode.Q;         // 기체 좌 롤
    public KeyCode rollRight = KeyCode.E;         // 기체 우 롤
    public KeyCode boost     = KeyCode.LeftShift; // 부스트
    public KeyCode dodge     = KeyCode.Space;     // 회피

    [Header("사격")]
    public KeyCode fireBullet  = KeyCode.Mouse0;  // 총알 (꾹)
    public KeyCode fireMissile = KeyCode.Mouse1;  // 미사일 (순간)
    public KeyCode fireLaser   = KeyCode.F;       // 레이저 (순간)
    public KeyCode fireAll     = KeyCode.V;       // 전체 발사 (순간)

    [Header("미사일 슬롯 전환")]
    public KeyCode missilePrev = KeyCode.Z;         // 이전 슬롯
    public KeyCode missileNext = KeyCode.X;         // 다음 슬롯

    [Header("소모품")]
    public KeyCode switchConsumable = KeyCode.R;    // 소모품 슬롯 전환
    public KeyCode useConsumable    = KeyCode.T;    // 소모품 사용
    
	[Header("다이스 패널 (토글)")]
	public KeyCode dicePanelToggle = KeyCode.Tab;

	[Header("모드 전환")]
    public KeyCode switchFireMode = KeyCode.C;      // 발사 모드 전환 (교차/동시)

    [Header("Unity Input Settings 축 이름")]
    [Tooltip("Edit > Project Settings > Input Manager 에 등록된 이름과 일치해야 함")]
    public string axisHorizontal  = "Horizontal";      // A/D
    public string axisVertical    = "Vertical";        // W/S
    public string axisMouseX      = "Mouse X";
    public string axisMouseY      = "Mouse Y";
    public string axisScrollWheel = "Mouse ScrollWheel";

    
}

// =====================================================================
// 게임패드 키 설정
// 축 이름은 Unity Input Settings에서 직접 등록한 이름과 맞춰야 함
// =====================================================================
[System.Serializable]
public class GamepadConfig
{
    [Header("이동")]
    public string axisLeftStickX   = "LeftStickX";
    public string axisLeftStickY   = "LeftStickY";
    public string axisVerticalMove = "VerticalMove";
    public KeyCode rollLeft        = KeyCode.JoystickButton6;
    public KeyCode rollRight       = KeyCode.JoystickButton7;
    public KeyCode boost           = KeyCode.JoystickButton8;
    public KeyCode dodge           = KeyCode.JoystickButton9;

    [Header("사격")]
    public KeyCode fireBullet      = KeyCode.JoystickButton5;
    public KeyCode fireMissile     = KeyCode.JoystickButton4;
    public KeyCode fireLaser       = KeyCode.JoystickButton3;
    public KeyCode fireAll         = KeyCode.JoystickButton2;

    [Header("미사일 슬롯 전환")]
    public string axisDPadX        = "DPadX";

    [Header("소모품")]
    public string axisDPadY        = "DPadY";

    [Header("다이스 패널(토글)")]
    public KeyCode dicePanelToggle = KeyCode.JoystickButton1;

    [Header("모드 전환")]
    public KeyCode switchFireMode  = KeyCode.JoystickButton10;

    [Header("Unity Input Settings 축 이름")]
    public string axisRightStickX  = "RightStickX";
    public string axisRightStickY  = "RightStickY";
}

// =====================================================================
// InputManager
//
// 플레이어의 입력을 매 프레임 수집해 공용 필드에 저장하는 싱글톤.
// 다른 스크립트는 InputManager.Instance.필드명 으로 읽기만 하면 됨.
// 조작 방식을 바꿔도 출력 필드는 동일하게
//
// ====== 출력 필드 요약 ======
// moveInput          : Vector3  X=좌우  Y=상하  Z=전후  (-1~1)
// lookInput          : Vector2  X=Yaw   Y=Pitch
// rollInput          : float    -1=좌롤  +1=우롤
// isBoosting         : bool     누르는 동안 true
// isDodging          : bool     누른 순간 한 프레임
// fireBullet         : bool     누르는 동안 true
// fireMissile        : bool     누른 순간 한 프레임
// fireLaser          : bool     누른 순간 한 프레임
// fireAll            : bool     누른 순간 한 프레임
// switchLockOnTarget : float    양수=다음  음수=이전  0=없음
// switchMissile1~3   : bool     누른 순간 한 프레임
// switchMissileShootMode : bool 누른 순간 한 프레임
// =====================================================================
public class InputManager : MonoBehaviour
{
    // =====================================================================
    // 싱글톤
    // =====================================================================
    private static InputManager instance = null;
    public static InputManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<InputManager>();
                if (instance == null)
                    Debug.LogError("[InputManager] 씬에 InputManager 없음! 하이어라키에 추가 필요");
            }
            return instance;
        }
    }

    // =====================================================================
    // 인스펙터 설정
    // =====================================================================
    [Header("━━━━━━ 조작 방식 선택 ━━━━━━")]
    [Tooltip("KEYBOARD_MOUSE / GAMEPAD / MOBILE 중 선택")]
    public INPUT_CONTROL_TYPE controlType = INPUT_CONTROL_TYPE.KEYBOARD_MOUSE;

    [Space(5)]
    [Header("━━━━━━ 키보드/마우스 키 설정 ━━━━━━")]
    public KeyboardMouseConfig keyboardMouseConfig = new KeyboardMouseConfig();

    [Space(5)]
    [Header("━━━━━━ 게임패드 키 설정 ━━━━━━")]
    public GamepadConfig gamepadConfig = new GamepadConfig();

    // =====================================================================
    // 출력 필드 (외부 스크립트는 읽기만)
    // =====================================================================
    [Space(10)]
    [Header("━━━━━━ 출력값 (읽기 전용) ━━━━━━")]

    [Header("이동/회전")]
    [Tooltip("X=좌우  Y=상하  Z=전후  |  -1~1")]
    public Vector3 moveInput;

    [Tooltip("X=Yaw(좌우시야)  Y=Pitch(상하시야)")]
    public Vector2 lookInput;

    [Tooltip("-1=좌 롤  +1=우 롤")]
    public float rollInput;

    [Tooltip("부스트 - 누르는 동안 true")]
    public bool isBoosting;

    [Tooltip("회피 - 누른 순간 한 프레임만 true")]
    public bool isDodging;

    [Header("사격")]
    [Tooltip("총알 - 누르는 동안 true")]
    public bool fireBullet;

    [Tooltip("미사일 - 누른 순간 한 프레임만 true")]
    public bool fireMissile;

    [Tooltip("레이저 - 누른 순간 한 프레임만 true")]
    public bool fireLaser;

    [Tooltip("전체 발사 - 누른 순간 한 프레임만 true")]
    public bool fireAll;

    [Tooltip("락온 대상 전환. 양수=다음  음수=이전  0=없음")]
    public float switchLockOnTarget;

    [Header("미사일 슬롯/모드")]
    [Tooltip("이전 슬롯 - 누른 순간 한 프레임만 true")]
    public bool switchMissilePrev;

    [Tooltip("다음 슬롯 - 누른 순간 한 프레임만 true")]
    public bool switchMissileNext;

    [Tooltip("발사 모드 전환(교차/동시) - 누른 순간 한 프레임만 true")]
    public bool switchMissileShootMode;

    [Header("소모품")]
    [Tooltip("소모품 슬롯 전환 - 누른 순간 한 프레임만 true")]
    public bool switchConsumable;

    [Tooltip("소모품 사용 - 누른 순간 한 프레임만 true")]
    public bool useConsumable;
    public bool dicePanelToggle;

    // D-패드 이전 프레임값 (게임패드 "누른 순간" 감지용)
    private float _prevDPadX = 0f;
    private float _prevDPadY = 0f;

    // =====================================================================
    // 싱글톤 초기화
    // =====================================================================
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Debug.LogWarning("[InputManager] 중복 감지. 기존 인스턴스 유지, 이 오브젝트 파괴");
            Destroy(gameObject);
        }
    }

    // =====================================================================
    // 매 프레임 입력 수집 - controlType에 따라 읽기 방식 분기
    // =====================================================================
    private void Update()
    {
        switch (controlType)
        {
            case INPUT_CONTROL_TYPE.KEYBOARD_MOUSE: ReadKeyboardMouse(); break;
            case INPUT_CONTROL_TYPE.GAMEPAD:        ReadGamepad();  break;
            case INPUT_CONTROL_TYPE.MOBILE:         ReadMobile();   break;
        }
    }

    // =====================================================================
    // 키보드 + 마우스 입력
    // =====================================================================
    private void ReadKeyboardMouse()
    {
        var km = keyboardMouseConfig;

        // 이동
        // WASD: Unity 기본 축(Horizontal/Vertical) 사용
        // 상하: Mouse4(상승) / Mouse3(하강)
        moveInput = new Vector3(
            Input.GetAxisRaw(km.axisHorizontal),
            (Input.GetKey(km.moveUp)   ? 1f : 0f)
          + (Input.GetKey(km.moveDown) ? -1f : 0f),
            Input.GetAxisRaw(km.axisVertical)
        );

        // 롤 회전 - 동시 입력 시 상쇄
        rollInput = (Input.GetKey(km.rollRight) ? 1f  : 0f)
                  + (Input.GetKey(km.rollLeft)  ? -1f : 0f);

        // 시야 (마우스 이동량)
        lookInput = new Vector2(
            Input.GetAxisRaw(km.axisMouseX),
            Input.GetAxisRaw(km.axisMouseY)
        );

        // 부스트 / 회피
        isBoosting = Input.GetKey(km.boost);
        isDodging  = Input.GetKeyDown(km.dodge);

        // 사격
        // fireBullet은 GetKey (연사 - 속도는 Player.fireDelay로 제어)
        // 나머지는 GetKeyDown (즉발/토글)
        fireBullet  = Input.GetKey(km.fireBullet);
        fireMissile = Input.GetKeyDown(km.fireMissile);
        fireLaser   = Input.GetKeyDown(km.fireLaser);
        fireAll     = Input.GetKeyDown(km.fireAll);

        // 락온 대상 전환 (마우스휠)
        switchLockOnTarget = Input.GetAxisRaw(km.axisScrollWheel);

        // 미사일 슬롯/모드 전환
        switchMissilePrev      = Input.GetKeyDown(km.missilePrev);
        switchMissileNext      = Input.GetKeyDown(km.missileNext);
        switchMissileShootMode = Input.GetKeyDown(km.switchFireMode);

        // 소모품
        switchConsumable = Input.GetKeyDown(km.switchConsumable);
        useConsumable    = Input.GetKeyDown(km.useConsumable);
        dicePanelToggle = Input.GetKeyDown(km.dicePanelToggle);
    }

    // =====================================================================
    // 게임패드 입력
    // 아래 축들은 Edit > Project Settings > Input Manager 에서 직접 등록 필요:
    //   LeftStickX, LeftStickY, RightStickX, RightStickY, VerticalMove, DPadY
    // =====================================================================
    private void ReadGamepad()
    {
        var gp = gamepadConfig;

        // 이동 (왼쪽 스틱)
        moveInput = new Vector3(
            Input.GetAxisRaw(gp.axisLeftStickX),
            Input.GetAxisRaw(gp.axisVerticalMove),
            Input.GetAxisRaw(gp.axisLeftStickY)
        );

        // 롤 회전 (LT/RT 버튼)
        rollInput = (Input.GetKey(gp.rollRight) ? 1f  : 0f)
                  + (Input.GetKey(gp.rollLeft)  ? -1f : 0f);

        // 시야 (오른쪽 스틱)
        lookInput = new Vector2(
            Input.GetAxisRaw(gp.axisRightStickX),
            Input.GetAxisRaw(gp.axisRightStickY)
        );

        // 부스트 / 회피
        isBoosting = Input.GetKey(gp.boost);
        isDodging  = Input.GetKeyDown(gp.dodge);

        // 사격
        fireBullet  = Input.GetKey(gp.fireBullet);
        fireMissile = Input.GetKeyDown(gp.fireMissile);
        fireLaser   = Input.GetKeyDown(gp.fireLaser);
        fireAll     = Input.GetKeyDown(gp.fireAll);

        // 락온 대상 전환 - D-패드 사용으로 충돌, 패드 미지원
        // TODO: 추후 별도 버튼 지정 필요
        switchLockOnTarget = 0f;

        // 모드 전환
        switchMissileShootMode = Input.GetKeyDown(gp.switchFireMode);

        // D-패드: 이전 프레임 비교로 "누른 순간" 감지
        //   좌 → 미사일 이전 슬롯
        //   우 → 미사일 다음 슬롯
        //   상 → 소모품 슬롯 전환
        //   하 → 소모품 사용
        float dpadX = Input.GetAxisRaw(gp.axisDPadX);
        float dpadY = Input.GetAxisRaw(gp.axisDPadY);

        switchMissilePrev = (dpadX < -0.5f) && (_prevDPadX >= -0.5f);
        switchMissileNext = (dpadX >  0.5f) && (_prevDPadX <=  0.5f);
        switchConsumable  = (dpadY >  0.5f) && (_prevDPadY <=  0.5f);
        useConsumable     = (dpadY < -0.5f) && (_prevDPadY >= -0.5f);

        dicePanelToggle = Input.GetKeyDown(gp.dicePanelToggle);
        _prevDPadX = dpadX;
        _prevDPadY = dpadY;
    }

    // =====================================================================
    // 모바일 입력 (미구현 - 추후 가상 조이스틱/버튼 연동 시 작성)
    // =====================================================================
    private void ReadMobile()
    {
        // 구현 예시 (가상 조이스틱 에셋 연동 시):
        //   moveInput  = VirtualJoystick.Instance.Direction;
        //   fireBullet = VirtualButton.Instance.IsHeld("FireBullet");
        Debug.LogWarning("[InputManager] MOBILE 조작은 아직 미구현입니다.");
    }
}
