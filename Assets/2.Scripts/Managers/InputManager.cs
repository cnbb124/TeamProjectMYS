using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================================================
// [InputManager — 외부 참조 / 사용 가이드]
// ================================================================
// 싱글톤. 매 프레임 키 입력을 읽어 필드에 저장.
// Player 등 다른 스크립트는 이 필드를 읽기만 할 것 (직접 쓰기 금지).
//
//   참조 예시)
//   InputManager _input = InputManager.Instance;
//   if (_input.isBoosting) { ... }
//
// ================================================================
// [입력 필드 목록 (읽기 전용)]
// ================================================================
// [축 입력 — 연속값]
// moveInput          : Vector3  이동 (X=좌우, Y=상하, Z=전후, -1~1)
// lookInput          : Vector2  시야 (X=Yaw, Y=Pitch)
// rollInput          : float    롤 (-1=좌, +1=우)
// switchLockOnTarget : float    휠 (양수=올림, 음수=내림, 0=없음)
//
// [버튼 입력 — 누르는 동안 true]
// isBoosting         : bool     부스트
// fireBullet         : bool     총알
//
// [버튼 입력 — 누른 순간 한 프레임만 true]
// isDodging              : bool   회피
// fireMissile            : bool   미사일
// fireAll                : bool   전체 발사
// switchMissilePrev      : bool   이전 미사일 슬롯
// switchMissileNext      : bool   다음 미사일 슬롯
// switchMissileShootMode : bool   발사 모드 전환 (교차/동시)
// toggleClusterLockMode  : bool   클러스터 단일/다중 락온 전환
// switchConsumable       : bool   소모품 슬롯 전환
// useConsumable          : bool   소모품 사용
// switchSkillSlot        : bool   스킬 슬롯 전환
// useSkill               : bool   스킬 사용
// fuelGaugeToggle        : bool   연료 게이지 패널 토글
// inventoryToggle        : bool   인벤토리 패널 토글
// pauseMenu              : bool   일시정지 메뉴 토글(ESC)
// mapToggle              : bool   전체맵 토글(M)
//
// ================================================================
// [키 설정 변경]
// ================================================================
// 인스펙터 [키보드/마우스 설정] 또는 [조이스틱 설정] 항목에서
// 각 KeyCode를 드롭다운으로 변경 가능.
//
// ================================================================
// 조작 방식 선택 열거형
// 인스펙터 [조작 방식 선택] 드롭다운에서 선택
// ================================================================


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
    //public KeyCode fireAll     = KeyCode.V;       // 전체 발사 (순간) 미사용레거시

    [Header("미사일/슬롯전환")]
    public KeyCode missilePrev = KeyCode.Z;         // 이전 슬롯
    public KeyCode missileNext = KeyCode.X;         // 다음 슬롯

    [Header("소모품/슬롯전환")]
    public KeyCode switchConsumable = KeyCode.R;    // 소모품 슬롯 전환
    public KeyCode useConsumable    = KeyCode.T;    // 소모품 사용

    [Header("스킬/슬롯전환")]
    public KeyCode switchSkillSlot = KeyCode.B;     // 스킬 슬롯 전환
    public KeyCode useSkill        = KeyCode.H;     // 스킬 사용

    [Header("UI 패널 토글")]
    public KeyCode fuelGaugeToggle  = KeyCode.G; // 연료 게이지 패널
    public KeyCode inventoryToggle  = KeyCode.I; // 인벤토리 패널
    public KeyCode pauseMenu        = KeyCode.Escape; // 일시정지 메뉴
    public KeyCode mapToggle        = KeyCode.M; // 전체맵

    [Header("상호작용(STATION에서)")]
    public KeyCode interAct         = KeyCode.E; // 상호작용(맵에서만)
	//[Header("모드 전환")]
 //   public KeyCode switchFireMode = KeyCode.C;      // 발사 모드 전환 (교차/동시) 미사용 레거시

    [Header("락온 모드 전환")]
    public KeyCode toggleClusterLockMode = KeyCode.C; // 클러스터 미사일 단일/다중 락온 전환

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
    //public KeyCode fireAll         = KeyCode.JoystickButton2; 미사용레거시

    [Header("미사일 슬롯 전환")]
    public string axisDPadX        = "DPadX";

    [Header("소모품")]
    public string axisDPadY        = "DPadY";

    [Header("스킬")]
    public KeyCode switchSkillSlot = KeyCode.JoystickButton11;  // 스킬 슬롯 전환
    public KeyCode useSkill        = KeyCode.JoystickButton12; // 스킬 사용

    //[Header("모드 전환")]
    //public KeyCode switchFireMode  = KeyCode.JoystickButton10; 미사용 레거시

    [Header("락온 모드 전환")]
    public KeyCode toggleClusterLockMode = KeyCode.JoystickButton0;

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
// switchLockOnTarget : float    양수=다음  음수=이전  0=없음
// switchMissilePrev/Next : bool 누른 순간 한 프레임 (미사일 슬롯 전환)
// switchMissileShootMode : bool 누른 순간 한 프레임 (발사모드 전환)
// toggleClusterLockMode  : bool 누른 순간 한 프레임 (클러스터 단일/다중 락온 전환)
// switchConsumable   : bool     누른 순간 한 프레임 (소모품 슬롯 전환)
// useConsumable      : bool     누른 순간 한 프레임 (소모품 사용)
// switchSkillSlot    : bool     누른 순간 한 프레임 (스킬 슬롯 전환)
// useSkill           : bool     누른 순간 한 프레임 (스킬 사용)
// fuelGaugeToggle    : bool     누른 순간 한 프레임 (연료 게이지 패널 토글)
// inventoryToggle    : bool     누른 순간 한 프레임 (인벤토리 패널 토글)
// pauseMenu          : bool     누른 순간 한 프레임 (일시정지 메뉴 토글 ESC)
// mapToggle          : bool     누른 순간 한 프레임 (전체맵 토글 M)
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
                {
                    Debug.LogError("[InputManager] 씬에 InputManager 없음! 하이어라키에 추가 필요");
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

    //[Tooltip("전체 발사 - 누른 순간 한 프레임만 true")]
    //public bool fireAll;미사요ㅗㅇ레거시

    [Tooltip("락온 대상 전환. 양수=다음  음수=이전  0=없음")]
    public float switchLockOnTarget;

    [Header("미사일 슬롯/모드")]
    [Tooltip("이전 슬롯 - 누른 순간 한 프레임만 true")]
    public bool switchMissilePrev;

    [Tooltip("다음 슬롯 - 누른 순간 한 프레임만 true")]
    public bool switchMissileNext;

    [Tooltip("발사 모드 전환(교차/동시) - 누른 순간 한 프레임만 true")]
    public bool switchMissileShootMode;

    [Tooltip("클러스터 단일/다중 락온 전환 - 누른 순간 한 프레임만 true")]
    public bool toggleClusterLockMode;

    [Header("소모품")]
    [Tooltip("소모품 슬롯 전환 - 누른 순간 한 프레임만 true")]
    public bool switchConsumable;

    [Tooltip("소모품 사용 - 누른 순간 한 프레임만 true")]
    public bool useConsumable;

    [Header("스킬")]
    [Tooltip("스킬 슬롯 전환 - 누른 순간 한 프레임만 true")]
    public bool switchSkillSlot;

    [Tooltip("스킬 사용 - 누른 순간 한 프레임만 true")]
    public bool useSkill;

    [Header("UI 패널 토글")]
    [Tooltip("연료 게이지 패널 토글 - 누른 순간 한 프레임만 true")]
    public bool fuelGaugeToggle;

    [Tooltip("인벤토리 패널 토글 - 누른 순간 한 프레임만 true")]
    public bool inventoryToggle;

    [Tooltip("일시정지 메뉴 토글 - 누른 순간 한 프레임만 true")]
    public bool pauseMenu;

    [Tooltip("전체맵 토글 - 누른 순간 한 프레임만 true")]
    public bool mapToggle;

    [Tooltip("상호작용 - 누른 순간 한 프레임만 true ")]
    public bool interAct;

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
        UpdateCursorLock();

        switch (controlType)
        {
            case INPUT_CONTROL_TYPE.KEYBOARD_MOUSE: ReadKeyboardMouse(); break;
            case INPUT_CONTROL_TYPE.GAMEPAD:        ReadGamepad();  break;
            case INPUT_CONTROL_TYPE.MOBILE:         ReadMobile();   break;
        }
    }

    // =====================================================================
    // 마우스 커서 잠금/해제
    // 평소(조종 중): Locked + 숨김 — Mouse X/Y가 카메라 시야 조작용 델타로 쓰임.
    // UI 패널이 열려있거나 Alt를 누르는 동안, 또는 씬에 조종할 플레이어가 없을 때(로비/메뉴 등):
    //   None + 보임 — 커서로 UI 클릭/창 밖 이동 가능.
    // InputManager는 DontDestroyOnLoad라 모든 씬에서 같은 로직이 도는데, 플레이어가 없는 씬에서는
    // 항상 잠금이 걸려버리는 문제가 있었음 — playerRef 체크로 해결.
    // 신규 UI 패널도 같은 방식으로 열림상태를 알리고 싶으면, IsUIRequestingCursor()에 || 조건만 추가.
    // =====================================================================
    private bool IsUIRequestingCursor()
    {
        bool noPlayerInScene = GameManager.Instance == null || GameManager.Instance.playerRef == null;
        bool isPaused = GameManager.Instance != null && GameManager.Instance.IsPaused;
        return noPlayerInScene
            || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)
            || InventoryPanelUI.IsOpen
            || isPaused;
    }

    private void UpdateCursorLock()
    {
        bool freeCursor = IsUIRequestingCursor();
        Cursor.lockState = freeCursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = freeCursor;
    }

    // 알트탭 등으로 창 포커스를 잃으면 OS가 강제로 커서 잠금을 풀어버림 — 포커스 복귀 시 재적용.
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            UpdateCursorLock();
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
        moveInput = new Vector3
        (
            Input.GetAxisRaw(km.axisHorizontal),
            (Input.GetKey(km.moveUp)   ? 1f : 0f)
          + (Input.GetKey(km.moveDown) ? -1f : 0f),
            Input.GetAxisRaw(km.axisVertical)
        );

        // 롤 회전 - 동시 입력 시 상쇄
        rollInput = (Input.GetKey(km.rollRight) ? 1f  : 0f)
                  + (Input.GetKey(km.rollLeft)  ? -1f : 0f);

        // 시야 (마우스 이동량) — 커서가 풀려있는 동안(UI/Alt)은 카메라 조종 안 함
        lookInput = IsUIRequestingCursor()? Vector2.zero
            : new Vector2(Input.GetAxisRaw(km.axisMouseX),Input.GetAxisRaw(km.axisMouseY));

        // 부스트 / 회피
        isBoosting = Input.GetKey(km.boost);
        isDodging  = Input.GetKeyDown(km.dodge);

        // 사격
        // fireBullet은 GetKey (연사 - 속도는 WeaponSystem.fireBulletDelay로 제어)
        // 나머지는 GetKeyDown (즉발/토글)
        fireBullet  = Input.GetKey(km.fireBullet);
        fireMissile = Input.GetKeyDown(km.fireMissile);
        //fireAll     = Input.GetKeyDown(km.fireAll);미사용레거시

        // 락온 대상 전환 (마우스휠)
        switchLockOnTarget = Input.GetAxisRaw(km.axisScrollWheel);

        // 미사일 슬롯/모드 전환
        switchMissilePrev      = Input.GetKeyDown(km.missilePrev);
        switchMissileNext      = Input.GetKeyDown(km.missileNext);
		// switchMissileShootMode = Input.GetKeyDown(km.switchFireMode);미사용레거시
		toggleClusterLockMode = Input.GetKeyDown(km.toggleClusterLockMode);

        // 소모품
        switchConsumable = Input.GetKeyDown(km.switchConsumable);
        useConsumable    = Input.GetKeyDown(km.useConsumable);

        // 스킬
        switchSkillSlot = Input.GetKeyDown(km.switchSkillSlot);
        useSkill        = Input.GetKeyDown(km.useSkill); 

        fuelGaugeToggle = Input.GetKeyDown(km.fuelGaugeToggle);
        inventoryToggle = Input.GetKeyDown(km.inventoryToggle);
        pauseMenu       = Input.GetKeyDown(km.pauseMenu);
        mapToggle       = Input.GetKeyDown(km.mapToggle);
        interAct        = Input.GetKeyDown(km.interAct);
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
        moveInput = new Vector3
        (
            Input.GetAxisRaw(gp.axisLeftStickX),
            Input.GetAxisRaw(gp.axisVerticalMove),
            Input.GetAxisRaw(gp.axisLeftStickY)
        );

        // 롤 회전 (LT/RT 버튼)
        rollInput = (Input.GetKey(gp.rollRight) ? 1f  : 0f)
                  + (Input.GetKey(gp.rollLeft)  ? -1f : 0f);

        // 시야 (오른쪽 스틱)
        lookInput = new Vector2
        (
            Input.GetAxisRaw(gp.axisRightStickX),
            Input.GetAxisRaw(gp.axisRightStickY)
        );

        // 부스트 / 회피
        isBoosting = Input.GetKey(gp.boost);
        isDodging  = Input.GetKeyDown(gp.dodge);

        // 사격
        fireBullet  = Input.GetKey(gp.fireBullet);
        fireMissile = Input.GetKeyDown(gp.fireMissile);
		// fireAll     = Input.GetKeyDown(gp.fireAll);미사용레거시

		// 락온 대상 전환 - D-패드 사용으로 충돌, 패드 미지원
		// TODO: 추후 별도 버튼 지정 필요
		switchLockOnTarget = 0f;

        // 모드 전환
        //switchMissileShootMode = Input.GetKeyDown(gp.switchFireMode); //미사용레거시
		toggleClusterLockMode  = Input.GetKeyDown(gp.toggleClusterLockMode);

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

        // 스킬
        switchSkillSlot = Input.GetKeyDown(gp.switchSkillSlot);
        useSkill        = Input.GetKeyDown(gp.useSkill);

        fuelGaugeToggle = false; // 게임패드 미지원 (키 없음)
        inventoryToggle = false; // 게임패드 미지원 (키 없음)
        pauseMenu       = false; // 게임패드 미지원 (전용 버튼 배정 시 gamepadConfig에 추가)
        mapToggle       = false; // 게임패드 미지원 (키 없음)
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
