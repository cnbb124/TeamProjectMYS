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
    public KeyCode moveForward = KeyCode.W;        // 전진
    public KeyCode moveBack    = KeyCode.S;        // 후진
    public KeyCode moveLeft    = KeyCode.A;        // 좌
    public KeyCode moveRight   = KeyCode.D;        // 우
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

    // 이동(WASD)은 위 KeyCode로 직접 읽음(축 아님). 마우스 시야/휠 축 이름은 값이 고정이라
    // InputManager의 const(AxisMouseX/Y/ScrollWheel)로 옮김 — 인스펙터에 노출할 필요 없음.
}

// =====================================================================
// 조합키 — 키 여러 개를 동시에 눌렀을 때만 나가는 입력.
//
// 키가 모자라서 한 키에 두 기능을 얹고 싶을 때 씀.
// 예) Shift+Q = 스킬 사용   /   Ctrl+Space = 소모품 사용
//
// [인스펙터 사용법]
//   Keys 에 같이 눌러야 할 키를 2개 이상 넣고, Action 에서 시킬 행동을 고르면 됨.
//   행동 목록(INPUT_ACTION)은 InputManager가 내보내는 출력 필드와 1:1로 대응함.
// =====================================================================
[System.Serializable]
public class ComboBinding
{
    [Tooltip("인스펙터에서 알아보기 위한 이름. 동작에는 영향 없음.")]
    public string name = "새 조합키";

    [Tooltip("동시에 누르고 있어야 하는 키들. 2개 이상 넣어야 동작함.\n" +
             "여기 있는 키가 전부 눌려 있을 때만 아래 행동이 나감.")]
    public List<KeyCode> keys = new List<KeyCode>();

    [Tooltip("이 조합을 눌렀을 때 시킬 행동.")]
    public INPUT_ACTION action = INPUT_ACTION.None;

    [Tooltip("ON  : 누르고 있는 동안 계속 유지됨 (이동/부스트/연사용)\n" +
             "OFF : 조합이 완성되는 순간 한 프레임만 (토글/사용/전환용)")]
    public bool holdAction = false;

    [Tooltip("ON: 조합이 성립하는 동안 그 키들의 원래 단독 기능을 막음.\n" +
             "예) Shift+Q를 조합키로 쓸 때 Q의 좌롤이 같이 나가는 것을 방지.\n" +
             "OFF: 단독 기능도 같이 나감(부스트+스킬처럼 겹쳐 쓰고 싶을 때).")]
    public bool suppressSingleKeys = true;

    // 직전 프레임에 조합이 성립해 있었는지 — '완성되는 순간'을 잡기 위한 것.
    // 저장할 값이 아니라 실행 중 상태라 직렬화 대상에서 뺌.
    [System.NonSerialized] public bool wasActive;
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

    // 축은 문자열이 아니라 GAMEPAD_AXIS로 지정 — 오타가 나면 컴파일에서 걸리고,
    // Project Settings에 등록된 이름과의 대응은 InputManager.AxisNames 한 곳에서만 관리됨.
    // None으로 두면 그 축은 아예 안 읽고 0으로 취급함(등록 안 한 축 때문에 예외 나는 것 방지).
    [Header("게임패드 축 배정")]
    public GAMEPAD_AXIS axisLeftStickX   = GAMEPAD_AXIS.LeftStickX;
    public GAMEPAD_AXIS axisLeftStickY   = GAMEPAD_AXIS.LeftStickY;
    public GAMEPAD_AXIS axisVerticalMove = GAMEPAD_AXIS.VerticalMove;
    public GAMEPAD_AXIS axisRightStickX  = GAMEPAD_AXIS.RightStickX;
    public GAMEPAD_AXIS axisRightStickY  = GAMEPAD_AXIS.RightStickY;
    public GAMEPAD_AXIS axisDPadX        = GAMEPAD_AXIS.DPadX;
    public GAMEPAD_AXIS axisDPadY        = GAMEPAD_AXIS.DPadY;
    public GAMEPAD_AXIS axisL2           = GAMEPAD_AXIS.LeftTrigger;   // L2 트리거(축)
    public GAMEPAD_AXIS axisR2           = GAMEPAD_AXIS.RightTrigger;  // R2 트리거(축)

    [Header("시야 옵션")]
    [Tooltip("오른쪽 스틱 상하(Y축) 반전. 켜면 스틱을 위로 밀 때 시야가 아래로 감(항공 스타일).")]
    public bool invertRStickY = false;
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
    [Header("━━━━━━ 조작 방식 ━━━━━━")]
    [Tooltip("MOBILE = 모바일 입력 강제. 그 외(KEYBOARD_MOUSE/GAMEPAD)는 값과 무관하게 키마+패드를 자동 병합함" +
             "(둘 다 동시 사용, 동시 입력 시 패드 우선). 현재 사용 중인 장치는 LastUsedDevice로 확인.")]
    public INPUT_CONTROL_TYPE controlType = INPUT_CONTROL_TYPE.KEYBOARD_MOUSE;

    [Space(5)]
    [Header("━━━━━━ 키보드/마우스 키 설정 ━━━━━━")]
    public KeyboardMouseConfig keyboardMouseConfig = new KeyboardMouseConfig();

    [Space(5)]
    [Header("━━━━━━ 게임패드 키 설정 ━━━━━━")]
    public GamepadConfig gamepadConfig = new GamepadConfig();

    [Space(5)]
    [Header("━━━━━━ 조합키 (동시 입력) ━━━━━━")]
    [Tooltip("키 2개 이상을 같이 눌렀을 때만 나가는 입력.\n" +
             "키가 모자랄 때 한 키에 기능을 더 얹는 용도. 개수 제한 없음.")]
    public List<ComboBinding> comboBindings = new List<ComboBinding>();

    // =====================================================================
    // 마우스 가상 조종간 (에버스페이스/엘리트 데인저러스 방식)
    //
    // 마우스는 '움직인 양'만 보고하는 장치라, 그대로 쓰면 마우스를 멈추는 순간 회전도 멈춤.
    // 그래서 델타를 누적해서 화면 중앙에 조종간이 하나 있는 것처럼 취급함 —
    // 기울여두면 되돌리기 전까지 계속 그 방향으로 돌게 됨(스틱과 같은 성격).
    // 이렇게 해야 lookInput의 의미가 패드 스틱과 통일돼서 Player가 장치를 구분할 필요가 없어짐.
    // =====================================================================
    [Space(5)]
    [Header("━━━━━━ 마우스 가상 조종간 ━━━━━━")]
    [Tooltip("마우스를 조금만 움직여도 조종간이 얼마나 기울어지는지. 클수록 예민함.\n" +
             "0.1~0.2 권장 — 너무 크면 조준이 튀고, 너무 작으면 최대 기울기까지 한참 밀어야 함.")]
    public float mouseStickSensitivity = 0.15f;

    [Tooltip("마우스를 멈췄을 때 조종간이 중앙으로 돌아오는 속도(초당 기울기량).\n" +
             "0이면 안 돌아옴(엘리트 방식 — 직접 되돌려야 멈춤). 1~2면 손 떼면 서서히 수평 복귀.")]
    public float mouseStickAutoCenter = 0f;

    // 현재 가상 조종간 기울기(-1~1). UI로 조종간 위치를 그려주려면 이 값을 읽으면 됨.
    public Vector2 MouseStick => _mouseStick;
    private Vector2 _mouseStick;

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

    // === 키마+패드 자동 병합/전환 ===
    // 마지막으로 실제 입력한 장치. UI가 이 값을 구독해 프롬프트(키/패드) 아이콘을 바꾸면 됨.
    public INPUT_CONTROL_TYPE LastUsedDevice { get; private set; } = INPUT_CONTROL_TYPE.KEYBOARD_MOUSE;

    // 패드가 하나라도 연결돼 있는지(자동 인식용).
    public bool IsGamepadConnected
    {
        get
        {
            string[] names = Input.GetJoystickNames();
            for (int i = 0; i < names.Length; i++)
            {
                if (!string.IsNullOrEmpty(names[i]))
                {
                    return true;
                }
            }
            return false;
        }
    }

    // GAMEPAD_AXIS → Project Settings에 등록된 실제 축 이름.
    // enum 순서와 반드시 같아야 함(GAMEPAD_AXIS에 항목을 늘리면 여기도 같은 자리에 추가할 것).
    // enum.ToString()을 안 쓰는 이유: ToString은 호출할 때마다 문자열을 새로 만들어서
    // 매 프레임 축을 9개씩 읽는 이 코드에선 쓰레기가 계속 쌓임.
    private static readonly string[] AxisNames =
    {
        string.Empty,     // None
        "LeftStickX",
        "LeftStickY",
        "RightStickX",
        "RightStickY",
        "DPadX",
        "DPadY",
        "LeftTrigger",
        "RightTrigger",
        "VerticalMove",
    };

    // 배정된 축을 읽음. None이거나 표 밖의 값이면 0 — 등록 안 된 축을 읽어 예외 나는 것도 같이 막힘.
    private static float ReadAxis(GAMEPAD_AXIS axis)
    {
        int index = (int)axis;
        if (index <= 0 || index >= AxisNames.Length)
        {
            return 0f;
        }
        return Input.GetAxisRaw(AxisNames[index]);
    }

    // 마우스 시야/휠 축 이름(Unity 기본값 — 안 바뀜).
    private const string AxisMouseX = "Mouse X";
    private const string AxisMouseY = "Mouse Y";
    private const string AxisScrollWheel = "Mouse ScrollWheel";
    // 스틱이 이 값을 넘으면 '패드로 조작 중'으로 보고 키마 이동/시야를 덮어씀(패드 우선).
    private const float StickActiveDeadzone = 0.2f;

    private bool _kbActive;   // 이번 프레임 키마 입력 있었나
    private bool _padActive;  // 이번 프레임 패드 입력 있었나

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
    // 매 프레임 입력 수집
    // MOBILE은 별도 경로. 그 외(PC)는 키마+패드를 매 프레임 둘 다 읽어 병합한다:
    //   - 키마 값으로 먼저 채우고, 패드 입력이 있으면 아날로그(이동/시야/롤/락온)를 덮어씀(패드 우선)
    //   - 버튼은 둘을 OR (아무 장치나 누르면 동작)
    //   - 마지막 입력 장치를 LastUsedDevice에 기록 → UI 프롬프트 자동 전환
    // controlType은 이제 'MOBILE 강제'용으로만 의미 있음(KEYBOARD_MOUSE/GAMEPAD 둘 다 자동 병합).
    // =====================================================================
    private void Update()
    {
        UpdateCursorLock();

        if (controlType == INPUT_CONTROL_TYPE.MOBILE)
        {
            ReadMobile();
            return;
        }

        ReadKeyboardMouse();  // 키보드/마우스 값으로 먼저 채움

        // 패드가 연결됐을 때만 오버레이 — 연결 안 됐으면 패드 축을 읽지 않음.
        // (패드용 커스텀 축이 Project Settings에 없으면 GetAxisRaw가 예외를 던지므로, 키보드만 쓸 땐 아예 스킵)
        _padActive = false;
        if (IsGamepadConnected)
        {
            OverlayGamepad(); // 패드 입력이 있으면 덮어씀(아날로그) / OR(버튼)
        }

        // 마지막으로 실제 입력한 장치 갱신 — 동시 입력이면 패드 우선(UI도 패드로 전환)
        if (_padActive)
        {
            LastUsedDevice = INPUT_CONTROL_TYPE.GAMEPAD;
        }
        else if (_kbActive)
        {
            LastUsedDevice = INPUT_CONTROL_TYPE.KEYBOARD_MOUSE;
        }

        // 조합키는 키마+패드 병합이 '끝난 뒤'에 얹는다 —
        // 단독 키 기능을 덮어써야 하는데, 병합 전에 처리하면 뒤이어 다시 켜져버림.
        UpdateComboBindings();

        // UI/메뉴가 열려있으면(=입력 잠금) 키마+패드 병합이 끝난 최종 출력에서 게임플레이 입력만 무효화함.
        // 인벤토리/일시정지 등을 닫아야 하는 UI 토글 키는 살려둠(안 그러면 못 닫음).
        if (IsGameplayInputLocked())
        {
            ClearGameplayInput();
        }
    }

    // =====================================================================
    // 조합키 처리 — 등록된 조합들을 훑어서 성립한 것만 행동으로 바꿈.
    //
    // 판정 순서가 중요함:
    //   1) 단독 키 기능을 먼저 끈다(suppressSingleKeys)
    //   2) 그 다음에 조합 행동을 켠다
    // 반대로 하면 1)이 2)를 지워버림. 조합 행동이 단독 기능과 같은 필드를 쓸 수 있기 때문.
    // =====================================================================
    private void UpdateComboBindings()
    {
        if (comboBindings == null)
        {
            return;
        }

        for (int i = 0; i < comboBindings.Count; i++)
        {
            ComboBinding combo = comboBindings[i];
            if (combo == null || combo.action == INPUT_ACTION.None)
            {
                continue;
            }

            bool active = IsComboHeld(combo);
            if (active)
            {
                if (combo.suppressSingleKeys)
                {
                    for (int k = 0; k < combo.keys.Count; k++)
                    {
                        ApplyAction(FindActionForKey(combo.keys[k]), false);
                    }
                }

                // holdAction이면 누르는 동안 계속, 아니면 조합이 '막 성립한' 프레임에만.
                if (combo.holdAction || !combo.wasActive)
                {
                    ApplyAction(combo.action, true);
                }
            }

            combo.wasActive = active;
        }
    }

    // 등록된 키가 전부 눌려 있는지. 2개 미만이면 조합키가 아니므로 성립 안 시킴
    // (1개짜리를 허용하면 단독 키 설정과 겹쳐서 어느 쪽이 이겼는지 알 수 없게 됨).
    private bool IsComboHeld(ComboBinding combo)
    {
        if (combo.keys == null || combo.keys.Count < 2)
        {
            return false;
        }

        for (int i = 0; i < combo.keys.Count; i++)
        {
            if (!Input.GetKey(combo.keys[i]))
            {
                return false;
            }
        }
        return true;
    }

    // 이 키에 원래 물려 있던 단독 행동을 찾음. 없으면 None.
    // 조합키가 성립했을 때 그 단독 기능을 꺼주기 위한 역참조라, 키보드 설정만 보면 됨.
    private INPUT_ACTION FindActionForKey(KeyCode key)
    {
        var km = keyboardMouseConfig;

        if (key == km.moveForward) return INPUT_ACTION.MoveForward;
        if (key == km.moveBack)    return INPUT_ACTION.MoveBack;
        if (key == km.moveLeft)    return INPUT_ACTION.MoveLeft;
        if (key == km.moveRight)   return INPUT_ACTION.MoveRight;
        if (key == km.moveUp)      return INPUT_ACTION.MoveUp;
        if (key == km.moveDown)    return INPUT_ACTION.MoveDown;
        if (key == km.rollLeft)    return INPUT_ACTION.RollLeft;
        if (key == km.rollRight)   return INPUT_ACTION.RollRight;
        if (key == km.boost)       return INPUT_ACTION.Boost;
        if (key == km.dodge)       return INPUT_ACTION.Dodge;

        if (key == km.fireBullet)  return INPUT_ACTION.FireBullet;
        if (key == km.fireMissile) return INPUT_ACTION.FireMissile;

        if (key == km.missilePrev) return INPUT_ACTION.MissilePrev;
        if (key == km.missileNext) return INPUT_ACTION.MissileNext;

        if (key == km.switchConsumable) return INPUT_ACTION.SwitchConsumable;
        if (key == km.useConsumable)    return INPUT_ACTION.UseConsumable;

        if (key == km.switchSkillSlot) return INPUT_ACTION.SwitchSkillSlot;
        if (key == km.useSkill)        return INPUT_ACTION.UseSkill;

        if (key == km.fuelGaugeToggle) return INPUT_ACTION.FuelGaugeToggle;
        if (key == km.inventoryToggle) return INPUT_ACTION.InventoryToggle;
        if (key == km.pauseMenu)       return INPUT_ACTION.PauseMenu;
        if (key == km.mapToggle)       return INPUT_ACTION.MapToggle;

        if (key == km.interAct)             return INPUT_ACTION.Interact;
        if (key == km.toggleClusterLockMode) return INPUT_ACTION.ToggleClusterLockMode;

        return INPUT_ACTION.None;
    }

    // 행동 하나를 출력 필드에 반영. on=false면 그 행동을 끔(조합키의 단독 기능 차단용).
    // 이동은 축 성분이라 끌 때 해당 방향 성분만 0으로 되돌림 —
    // 반대 방향 키를 같이 누르고 있을 수 있어서 통째로 0을 넣으면 안 됨.
    private void ApplyAction(INPUT_ACTION action, bool on)
    {
        switch (action)
        {
            case INPUT_ACTION.None: break;

            case INPUT_ACTION.MoveForward: if (on) moveInput.z = 1f;  else if (moveInput.z > 0f) moveInput.z = 0f; break;
            case INPUT_ACTION.MoveBack:    if (on) moveInput.z = -1f; else if (moveInput.z < 0f) moveInput.z = 0f; break;
            case INPUT_ACTION.MoveRight:   if (on) moveInput.x = 1f;  else if (moveInput.x > 0f) moveInput.x = 0f; break;
            case INPUT_ACTION.MoveLeft:    if (on) moveInput.x = -1f; else if (moveInput.x < 0f) moveInput.x = 0f; break;
            case INPUT_ACTION.MoveUp:      if (on) moveInput.y = 1f;  else if (moveInput.y > 0f) moveInput.y = 0f; break;
            case INPUT_ACTION.MoveDown:    if (on) moveInput.y = -1f; else if (moveInput.y < 0f) moveInput.y = 0f; break;

            case INPUT_ACTION.RollRight:   if (on) rollInput = 1f;  else if (rollInput > 0f) rollInput = 0f; break;
            case INPUT_ACTION.RollLeft:    if (on) rollInput = -1f; else if (rollInput < 0f) rollInput = 0f; break;

            case INPUT_ACTION.LockOnNext:  if (on) switchLockOnTarget = 1f;  else if (switchLockOnTarget > 0f) switchLockOnTarget = 0f; break;
            case INPUT_ACTION.LockOnPrev:  if (on) switchLockOnTarget = -1f; else if (switchLockOnTarget < 0f) switchLockOnTarget = 0f; break;

            case INPUT_ACTION.Boost:       isBoosting = on; break;
            case INPUT_ACTION.Dodge:       isDodging = on; break;
            case INPUT_ACTION.FireBullet:  fireBullet = on; break;
            case INPUT_ACTION.FireMissile: fireMissile = on; break;

            case INPUT_ACTION.MissilePrev:           switchMissilePrev = on; break;
            case INPUT_ACTION.MissileNext:           switchMissileNext = on; break;
            case INPUT_ACTION.MissileShootMode:      switchMissileShootMode = on; break;
            case INPUT_ACTION.ToggleClusterLockMode: toggleClusterLockMode = on; break;

            case INPUT_ACTION.SwitchConsumable: switchConsumable = on; break;
            case INPUT_ACTION.UseConsumable:    useConsumable = on; break;
            case INPUT_ACTION.SwitchSkillSlot:  switchSkillSlot = on; break;
            case INPUT_ACTION.UseSkill:         useSkill = on; break;

            case INPUT_ACTION.FuelGaugeToggle: fuelGaugeToggle = on; break;
            case INPUT_ACTION.InventoryToggle: inventoryToggle = on; break;
            case INPUT_ACTION.PauseMenu:       pauseMenu = on; break;
            case INPUT_ACTION.MapToggle:       mapToggle = on; break;
            case INPUT_ACTION.Interact:        interAct = on; break;
        }
    }

    // =====================================================================
    // 게임플레이 입력 무효화 — UI/메뉴 열림 중(IsGameplayInputLocked) 호출.
    // 키마+패드 병합이 끝난 최종 출력에서 게임플레이 액션(이동/사격/스킬 등)만 기본값으로 되돌림.
    // UI 토글(연료/인벤토리/일시정지/맵)은 남겨둬야 패널을 닫을 수 있으므로 건드리지 않음.
    // =====================================================================
    private void ClearGameplayInput()
    {
        moveInput = Vector3.zero;
        lookInput = Vector2.zero;
        rollInput = 0f;
        // 조종간도 같이 수평으로 — 안 그러면 메뉴를 닫는 순간 기울어져 있던 값으로 기수가 계속 돌아감
        _mouseStick = Vector2.zero;
        isBoosting = false;
        isDodging = false;
        fireBullet = false;
        fireMissile = false;
        switchLockOnTarget = 0f;
        switchMissilePrev = false;
        switchMissileNext = false;
        switchMissileShootMode = false;
        toggleClusterLockMode = false;
        switchConsumable = false;
        useConsumable = false;
        switchSkillSlot = false;
        useSkill = false;
        interAct = false;
    }

    // =====================================================================
    // 게임플레이 입력 잠금 판정 (커서 잠금/해제도 이 값에 연동)
    // true면: 커서를 풀어(None+보임) UI 클릭 가능 + 게임플레이 입력(이동/사격/스킬 등)을 이번 프레임 무시함.
    //   → UI 패널이 열려있거나 Alt를 누르는 동안, 또는 씬에 조종할 플레이어가 없을 때(로비/메뉴 등).
    // false면(평소 조종 중): 커서 Locked+숨김 — Mouse X/Y가 카메라 시야 조작용 델타로 쓰임.
    // InputManager는 DontDestroyOnLoad라 모든 씬에서 같은 로직이 도는데, 플레이어가 없는 씬에서는
    // 항상 잠금이 걸려버리는 문제가 있었음 — playerRef 체크로 해결.
    // 신규 UI 패널도 게임 입력을 막고 싶으면, IsGameplayInputLocked()에 || 조건만 추가하면 됨.
    // (실제 게임플레이 필드 클리어는 Update 끝의 ClearGameplayInput가 함 — UI 토글 키는 살려둠)
    // =====================================================================
    private bool IsGameplayInputLocked()
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
        bool freeCursor = IsGameplayInputLocked();
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

        // 이동 — 전부 개별 키(리바인딩 가능). 값은 -1/0/1(디지털), 기존 GetAxisRaw(WASD)와 동일.
        // 좌우: A/D, 상하: Mouse4/Mouse3, 전후: W/S
        moveInput = new Vector3
        (
            (Input.GetKey(km.moveRight)   ? 1f : 0f) + (Input.GetKey(km.moveLeft) ? -1f : 0f),
            (Input.GetKey(km.moveUp)      ? 1f : 0f) + (Input.GetKey(km.moveDown) ? -1f : 0f),
            (Input.GetKey(km.moveForward) ? 1f : 0f) + (Input.GetKey(km.moveBack) ? -1f : 0f)
        );

        // 롤 회전 - 동시 입력 시 상쇄
        rollInput = (Input.GetKey(km.rollRight) ? 1f  : 0f)
                  + (Input.GetKey(km.rollLeft)  ? -1f : 0f);

        // 시야 — 마우스 이동량을 그대로 쓰지 않고 가상 조종간에 누적함(위 필드 설명 참고).
        // 커서가 풀려있는 동안(UI/Alt)은 조종간을 안 건드림 — 메뉴에서 마우스를 움직여도 기수가 안 돌게.
        if (!IsGameplayInputLocked())
        {
            Vector2 mouseDelta = new Vector2(Input.GetAxisRaw(AxisMouseX), Input.GetAxisRaw(AxisMouseY));
            _mouseStick += mouseDelta * mouseStickSensitivity;
            // 원형으로 제한 — 대각선이 축별로 잘려서 더 빨라지는 현상 방지
            _mouseStick = Vector2.ClampMagnitude(_mouseStick, 1f);

            if (mouseStickAutoCenter > 0f && mouseDelta.sqrMagnitude <= 0f)
            {
                _mouseStick = Vector2.MoveTowards(_mouseStick, Vector2.zero, mouseStickAutoCenter * Time.deltaTime);
            }
        }
        lookInput = _mouseStick;

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
        switchLockOnTarget = Input.GetAxisRaw(AxisScrollWheel);

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

        // 이번 프레임 키마 입력이 있었는지(장치 전환 판정용). 이 시점 필드는 아직 키마 값만 담겨 있음.
        _kbActive = moveInput.sqrMagnitude > 0f
            || lookInput.sqrMagnitude > 0f
            || Mathf.Abs(switchLockOnTarget) > 0.01f
            || rollInput != 0f
            || isBoosting || fireBullet || isDodging || fireMissile
            || switchMissilePrev || switchMissileNext
            || switchConsumable || useConsumable
            || switchSkillSlot || useSkill || toggleClusterLockMode
            || fuelGaugeToggle || inventoryToggle || pauseMenu || mapToggle || interAct;
    }

    // =====================================================================
    // 게임패드 입력 — '오버레이' 방식. ReadKeyboardMouse가 채운 값 위에 얹는다.
    //   아날로그(이동/시야/롤/락온): 패드 입력이 있으면 키마 값을 덮어씀(패드 우선).
    //   버튼: 키마 결과에 OR (아무 장치나 누르면 동작).
    // 버튼 액션은 GAMEPAD_BUTTON(PS 명칭)으로 지정되고, 여기서 실제 KeyCode/축으로 매핑함.
    // 아래 축들은 Edit > Project Settings > Input Manager 에서 직접 등록 필요:
    //   LeftStickX, LeftStickY, RightStickX, RightStickY, VerticalMove, DPadX, DPadY, LeftTrigger, RightTrigger
    // =====================================================================
    private void OverlayGamepad()
    {
        var gp = gamepadConfig;

        // 축(트리거/D패드)을 이번 프레임 1번만 샘플 — GetPad/GetPadDown이 엣지 판정에 이 값을 씀.
        _padDpadX = ReadAxis(gp.axisDPadX);
        _padDpadY = ReadAxis(gp.axisDPadY);
        _padL2    = ReadAxis(gp.axisL2);
        _padR2    = ReadAxis(gp.axisR2);

        // --- 아날로그: 패드가 데드존 넘게 들어오면 키마 값을 덮어씀(패드 우선) ---
        // 이동 (왼쪽 스틱) — 스틱 크기가 그대로 반영돼 살짝=살살 / 확=빠르게(MovingByInput이 크기 보존).
        Vector3 padMove = new Vector3
        (
            ReadAxis(gp.axisLeftStickX),
            ReadAxis(gp.axisVerticalMove),
            ReadAxis(gp.axisLeftStickY)
        );
        bool padMoving = padMove.sqrMagnitude > StickActiveDeadzone * StickActiveDeadzone;
        if (padMoving)
        {
            moveInput = padMove;
        }

        // 시야 (오른쪽 스틱) — Y축 반전 옵션 반영
        float rStickY = ReadAxis(gp.axisRightStickY);
        Vector2 padLook = new Vector2
        (
            ReadAxis(gp.axisRightStickX),
            gp.invertRStickY ? -rStickY : rStickY
        );
        bool padLooking = padLook.sqrMagnitude > StickActiveDeadzone * StickActiveDeadzone;
        if (padLooking)
        {
            lookInput = padLook;
        }

        // 롤 (누르는 동안)
        float padRoll = (GetPad(gp.rollRight) ? 1f : 0f) + (GetPad(gp.rollLeft) ? -1f : 0f);
        if (padRoll != 0f)
        {
            rollInput = padRoll;
        }

        // 락온 대상 전환 — 지정 버튼 이전/다음(누른 순간 +1/-1). 키마 마우스휠과 같은 의미.
        float padLockOn = (GetPadDown(gp.lockOnNext) ? 1f : 0f) + (GetPadDown(gp.lockOnPrev) ? -1f : 0f);
        if (padLockOn != 0f)
        {
            switchLockOnTarget = padLockOn;
        }

        // --- 버튼: 키마 결과에 OR ---
        bool padBoost    = GetPad(gp.boost);
        bool padFire     = GetPad(gp.fireBullet);
        bool padDodge    = GetPadDown(gp.dodge);
        bool padMissile  = GetPadDown(gp.fireMissile);
        bool padCluster  = GetPadDown(gp.toggleClusterLockMode);
        bool padMPrev    = GetPadDown(gp.missilePrev);
        bool padMNext    = GetPadDown(gp.missileNext);
        bool padSwCons   = GetPadDown(gp.switchConsumable);
        bool padUseCons  = GetPadDown(gp.useConsumable);
        bool padSwSkill  = GetPadDown(gp.switchSkillSlot);
        bool padUseSkill = GetPadDown(gp.useSkill);
        bool padFuel     = GetPadDown(gp.fuelGaugeToggle);
        bool padInv      = GetPadDown(gp.inventoryToggle);
        bool padPause    = GetPadDown(gp.pauseMenu);
        bool padMap      = GetPadDown(gp.mapToggle);
        bool padInter    = GetPadDown(gp.interAct);

        isBoosting            |= padBoost;
        fireBullet            |= padFire;
        isDodging             |= padDodge;
        fireMissile           |= padMissile;
        toggleClusterLockMode |= padCluster;
        switchMissilePrev     |= padMPrev;
        switchMissileNext     |= padMNext;
        switchConsumable      |= padSwCons;
        useConsumable         |= padUseCons;
        switchSkillSlot       |= padSwSkill;
        useSkill              |= padUseSkill;
        fuelGaugeToggle       |= padFuel;
        inventoryToggle       |= padInv;
        pauseMenu             |= padPause;
        mapToggle             |= padMap;
        interAct              |= padInter;

        // 이번 프레임 패드 입력 있었나(장치 전환 판정용)
        bool padButton = padBoost || padFire || padDodge || padMissile || padCluster
            || padMPrev || padMNext || padSwCons || padUseCons || padSwSkill || padUseSkill
            || padFuel || padInv || padPause || padMap || padInter;
        _padActive = padMoving || padLooking || padRoll != 0f || padLockOn != 0f || padButton;

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
            // 듀얼센스 터치패드 클릭 — 드라이버/OS마다 번호가 달라 확정값이 아님. 실기기에서 눌리는 번호로 조정할 것.
            case GAMEPAD_BUTTON.TouchpadClick: return KeyCode.JoystickButton13;
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
