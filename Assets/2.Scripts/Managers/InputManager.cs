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
//
// [JoystickButton 번호 ↔ 실제 버튼] — Xbox 컨트롤러 / Windows 기준(레거시 Input)
//   Button0 = A        Button1 = B        Button2 = X        Button3 = Y
//   Button4 = LB(L1)   Button5 = RB(R1)
//   Button6 = Back(View)   Button7 = Start(Menu)
//   Button8 = L3(왼쪽 스틱 누름)   Button9 = R3(오른쪽 스틱 누름)
//   ※ LT/RT(=L2/R2)는 버튼이 아니라 '축(아날로그 트리거)'임 — JoystickButton으로 안 잡힘.
//      쓰려면 Project Settings > Input Manager에 축으로 등록해 axis 이름으로 읽어야 함.
//   ※ Xbox/Windows는 버튼 0~9만 존재함 — Button10 이상은 이 조합에서 안 눌림.
//   ※ PlayStation 패드/다른 OS는 번호 체계가 다름 — 위는 Xbox+Windows 기준.
// =====================================================================
[System.Serializable]
public class GamepadConfig
{
    // 버튼 액션은 GAMEPAD_BUTTON(PS 명칭)으로 지정 — 인스펙터에 R2/Square 등으로 직관적으로 뜸.
    // 실제 KeyCode/축 변환은 InputManager가 함. None이면 이 패드엔 미배정(안 눌림).
    // 물리 입력이 액션 수보다 적어 몇 개는 기본 None임 — 필요한 것만 인스펙터에서 배정하면 됨.

    [Header("이동/회전")]
    public GAMEPAD_BUTTON rollLeft  = GAMEPAD_BUTTON.Square;
    public GAMEPAD_BUTTON rollRight = GAMEPAD_BUTTON.Circle;
    public GAMEPAD_BUTTON boost     = GAMEPAD_BUTTON.L1;
    public GAMEPAD_BUTTON dodge     = GAMEPAD_BUTTON.Cross;

    [Header("사격")]
    public GAMEPAD_BUTTON fireBullet  = GAMEPAD_BUTTON.R2;
    public GAMEPAD_BUTTON fireMissile = GAMEPAD_BUTTON.L2;

    [Header("미사일 슬롯 전환")]
    public GAMEPAD_BUTTON missilePrev = GAMEPAD_BUTTON.DpadLeft;
    public GAMEPAD_BUTTON missileNext = GAMEPAD_BUTTON.DpadRight;

    [Header("소모품")]
    public GAMEPAD_BUTTON switchConsumable = GAMEPAD_BUTTON.DpadUp;
    public GAMEPAD_BUTTON useConsumable    = GAMEPAD_BUTTON.DpadDown;

    [Header("스킬")]
    public GAMEPAD_BUTTON switchSkillSlot = GAMEPAD_BUTTON.R1;
    public GAMEPAD_BUTTON useSkill        = GAMEPAD_BUTTON.Triangle;

    [Header("락온 모드 전환")]
    public GAMEPAD_BUTTON toggleClusterLockMode = GAMEPAD_BUTTON.R3;

    [Header("락온 대상 전환")]
    public GAMEPAD_BUTTON lockOnPrev = GAMEPAD_BUTTON.L3;    // 이전 락온 대상 (키마 마우스휠 아래에 대응)
    public GAMEPAD_BUTTON lockOnNext = GAMEPAD_BUTTON.Share; // 다음 락온 대상 (키마 마우스휠 위에 대응)

    [Header("UI 패널 토글")]
    public GAMEPAD_BUTTON fuelGaugeToggle = GAMEPAD_BUTTON.None; // 물리 버튼 부족 — 기본 미배정
    public GAMEPAD_BUTTON inventoryToggle = GAMEPAD_BUTTON.None; // 물리 버튼 부족 — 기본 미배정
    public GAMEPAD_BUTTON pauseMenu       = GAMEPAD_BUTTON.Options;
    public GAMEPAD_BUTTON mapToggle       = GAMEPAD_BUTTON.None; // 물리 버튼 부족 — 기본 미배정

    [Header("상호작용(STATION에서)")]
    public GAMEPAD_BUTTON interAct        = GAMEPAD_BUTTON.Cross; // 전투의 dodge와 씬 문맥이 달라 공유 무방

    [Header("Unity Input Settings 축 이름 (Project Settings에 등록 필요)")]
    public string axisLeftStickX   = "LeftStickX";
    public string axisLeftStickY   = "LeftStickY";
    public string axisVerticalMove = "VerticalMove";
    public string axisRightStickX  = "RightStickX";
    public string axisRightStickY  = "RightStickY";
    public string axisDPadX        = "DPadX";
    public string axisDPadY        = "DPadY";
    public string axisL2           = "LeftTrigger";  // L2 트리거(축)
    public string axisR2           = "RightTrigger"; // R2 트리거(축)
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
    // Awake에서만 세팅됨. Awake 전엔 null이므로 최초 접근은 Start부터 할 것.
    // (예전엔 여기서 FindObjectOfType으로 찾아줬는데, 그게 매니저 자신의 Awake보다 먼저
    //  instance를 채워버려서 Awake의 초기화 블록이 통째로 스킵되는 버그를 만들었음)
    public static InputManager Instance => instance;

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

    // 게임패드 축(트리거/D패드)을 매 프레임 1번만 샘플해두는 값 — GetPad/GetPadDown이 이걸 봄.
    // 축은 버튼이 아니라 "누른 순간"을 이전 프레임과 비교해 판정하므로 현재/직전 값을 같이 보관함.
    private float _padDpadX, _padDpadY, _padL2, _padR2;
    private float _prevPadDpadX, _prevPadDpadY, _prevPadL2, _prevPadR2;
    private const float TriggerThreshold = 0.5f; // 트리거를 "눌림"으로 볼 임계값(축)

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
            || PauseMenuUI.IsOpen   // 멀티에선 IsPaused가 false라, 메뉴 열림 자체로 커서를 풀어야 클릭 가능
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
    // 버튼 액션은 GAMEPAD_BUTTON(PS 명칭)으로 지정되고, 여기서 실제 KeyCode/축으로 매핑함.
    // 아래 축들은 Edit > Project Settings > Input Manager 에서 직접 등록 필요:
    //   LeftStickX, LeftStickY, RightStickX, RightStickY, VerticalMove, DPadX, DPadY, LeftTrigger, RightTrigger
    // =====================================================================
    private void ReadGamepad()
    {
        var gp = gamepadConfig;

        // 축(트리거/D패드)을 이번 프레임 1번만 샘플 — GetPad/GetPadDown이 엣지 판정에 이 값을 씀.
        _padDpadX = Input.GetAxisRaw(gp.axisDPadX);
        _padDpadY = Input.GetAxisRaw(gp.axisDPadY);
        _padL2    = Input.GetAxisRaw(gp.axisL2);
        _padR2    = Input.GetAxisRaw(gp.axisR2);

        // 이동 (왼쪽 스틱)
        moveInput = new Vector3
        (
            Input.GetAxisRaw(gp.axisLeftStickX),
            Input.GetAxisRaw(gp.axisVerticalMove),
            Input.GetAxisRaw(gp.axisLeftStickY)
        );

        // 롤 회전 (누르는 동안)
        rollInput = (GetPad(gp.rollRight) ? 1f  : 0f)
                  + (GetPad(gp.rollLeft)  ? -1f : 0f);

        // 시야 (오른쪽 스틱)
        lookInput = new Vector2
        (
            Input.GetAxisRaw(gp.axisRightStickX),
            Input.GetAxisRaw(gp.axisRightStickY)
        );

        // 부스트(누르는 동안) / 회피(누른 순간)
        isBoosting = GetPad(gp.boost);
        isDodging  = GetPadDown(gp.dodge);

        // 사격 — 총알은 연사(누르는 동안), 미사일은 즉발(누른 순간)
        fireBullet  = GetPad(gp.fireBullet);
        fireMissile = GetPadDown(gp.fireMissile);
        // 락온 대상 전환 - 지정 버튼 이전/다음(누른 순간 +1/-1). 키마 마우스휠과 같은 의미.
        switchLockOnTarget = (GetPadDown(gp.lockOnNext) ? 1f  : 0f)
                           + (GetPadDown(gp.lockOnPrev) ? -1f : 0f);

        toggleClusterLockMode = GetPadDown(gp.toggleClusterLockMode);

        // 미사일 슬롯 / 소모품 (기본 D패드 — 축 엣지 판정은 GetPadDown이 처리)
        switchMissilePrev = GetPadDown(gp.missilePrev);
        switchMissileNext = GetPadDown(gp.missileNext);
        switchConsumable  = GetPadDown(gp.switchConsumable);
        useConsumable     = GetPadDown(gp.useConsumable);

        // 스킬
        switchSkillSlot = GetPadDown(gp.switchSkillSlot);
        useSkill        = GetPadDown(gp.useSkill);

        // UI 토글/상호작용 - 키마와 동일하게 지정 버튼으로 처리(None이면 안 눌림)
        fuelGaugeToggle = GetPadDown(gp.fuelGaugeToggle);
        inventoryToggle = GetPadDown(gp.inventoryToggle);
        pauseMenu       = GetPadDown(gp.pauseMenu);
        mapToggle       = GetPadDown(gp.mapToggle);
        interAct        = GetPadDown(gp.interAct);

        // 다음 프레임 엣지 판정용으로 이번 축값 보관
        _prevPadDpadX = _padDpadX;
        _prevPadDpadY = _padDpadY;
        _prevPadL2    = _padL2;
        _prevPadR2    = _padR2;
    }

    // =====================================================================
    // 게임패드 버튼 매핑/판정 — GAMEPAD_BUTTON(PS 명칭)을 실제 입력으로 변환.
    // 페이스/범퍼/스틱/메뉴 버튼은 KeyCode.JoystickButton, L2/R2·D패드는 축으로 읽음.
    // 물리 번호는 Xbox+Windows(레거시 Input) 기준 — 다른 패드/OS면 ToJoystickKey만 고치면 됨.
    // =====================================================================

    // 누르는 동안 true
    private bool GetPad(GAMEPAD_BUTTON b)
    {
        switch (b)
        {
            case GAMEPAD_BUTTON.None:      return false;
            case GAMEPAD_BUTTON.L2:        return _padL2 > TriggerThreshold;
            case GAMEPAD_BUTTON.R2:        return _padR2 > TriggerThreshold;
            case GAMEPAD_BUTTON.DpadUp:    return _padDpadY >  0.5f;
            case GAMEPAD_BUTTON.DpadDown:  return _padDpadY < -0.5f;
            case GAMEPAD_BUTTON.DpadLeft:  return _padDpadX < -0.5f;
            case GAMEPAD_BUTTON.DpadRight: return _padDpadX >  0.5f;
            default:                       return Input.GetKey(ToJoystickKey(b));
        }
    }

    // 누른 순간 한 프레임만 true (축은 이전 프레임과 비교해 엣지 판정)
    private bool GetPadDown(GAMEPAD_BUTTON b)
    {
        switch (b)
        {
            case GAMEPAD_BUTTON.None:      return false;
            case GAMEPAD_BUTTON.L2:        return _padL2 > TriggerThreshold && _prevPadL2 <= TriggerThreshold;
            case GAMEPAD_BUTTON.R2:        return _padR2 > TriggerThreshold && _prevPadR2 <= TriggerThreshold;
            case GAMEPAD_BUTTON.DpadUp:    return _padDpadY >  0.5f && _prevPadDpadY <=  0.5f;
            case GAMEPAD_BUTTON.DpadDown:  return _padDpadY < -0.5f && _prevPadDpadY >= -0.5f;
            case GAMEPAD_BUTTON.DpadLeft:  return _padDpadX < -0.5f && _prevPadDpadX >= -0.5f;
            case GAMEPAD_BUTTON.DpadRight: return _padDpadX >  0.5f && _prevPadDpadX <=  0.5f;
            default:                       return Input.GetKeyDown(ToJoystickKey(b));
        }
    }

    // 실제 버튼(페이스/범퍼/스틱/메뉴)만 KeyCode로 변환. 축(L2/R2/D패드)은 위에서 처리하므로 여기 안 옴.
    // Xbox+Windows 레거시 Input 기준 번호. 다른 패드/OS면 이 표만 교체하면 됨.
    private KeyCode ToJoystickKey(GAMEPAD_BUTTON b)
    {
        switch (b)
        {
            case GAMEPAD_BUTTON.Cross:    return KeyCode.JoystickButton0;
            case GAMEPAD_BUTTON.Circle:   return KeyCode.JoystickButton1;
            case GAMEPAD_BUTTON.Square:   return KeyCode.JoystickButton2;
            case GAMEPAD_BUTTON.Triangle: return KeyCode.JoystickButton3;
            case GAMEPAD_BUTTON.L1:       return KeyCode.JoystickButton4;
            case GAMEPAD_BUTTON.R1:       return KeyCode.JoystickButton5;
            case GAMEPAD_BUTTON.Share:    return KeyCode.JoystickButton6;
            case GAMEPAD_BUTTON.Options:  return KeyCode.JoystickButton7;
            case GAMEPAD_BUTTON.L3:       return KeyCode.JoystickButton8;
            case GAMEPAD_BUTTON.R3:       return KeyCode.JoystickButton9;
            default:                      return KeyCode.None;
        }
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
