using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.DualShock;

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

    [Header("시야 옵션")]
    [Tooltip("마우스 상하(Y) 반전. 켜면 마우스를 위로 밀 때 기수가 아래로 감(항공 스타일).")]
    public bool invertLookY = false;

    [Header("부스트 방식")]
    [Tooltip("OFF: 누르고 있는 동안만 부스트(기본)\n" +
             "ON : 한 번 누르면 켜지고 다시 누르면 꺼짐(토글)\n" +
             "장치마다 따로 설정됨 — 키보드는 꾹, 패드는 토글처럼 다르게 쓸 수 있음.")]
    public bool boostToggle = false;

    [Header("상호작용(STATION에서)")]
    public KeyCode interAct         = KeyCode.E; // 상호작용(맵에서만)
	//[Header("모드 전환")]
 //   public KeyCode switchFireMode = KeyCode.C;      // 발사 모드 전환 (교차/동시) 미사용 레거시

    [Header("락온 모드 전환")]
    public KeyCode toggleClusterLockMode = KeyCode.C; // 클러스터 미사일 단일/다중 락온 전환

    // 이동(WASD)은 위 KeyCode로 직접 읽음(축 아님). 마우스 시야/휠 축 이름은 값이 고정이라
    // 이동(WASD)은 위 KeyCode로 직접 읽음(축 아님). 마우스 이동량은 장치에서 직접 읽고,
    // 휠 축 이름만 InputManager의 const(AxisScrollWheel)로 둠 — 인스펙터에 노출할 필요 없음.
}

// =====================================================================
// 모바일 설정.
// 가상 조이스틱/버튼은 아직 미구현(InputManager.ReadMobile)이라 지금은 부스트 방식만 있음.
// 구현할 때 여기에 감도/버튼 배치 등을 추가하면 됨.
// =====================================================================
[System.Serializable]
public class MobileConfig
{
    [Header("시야 옵션")]
    [Tooltip("터치 드래그 상하(Y) 반전. 켜면 위로 쓸어올릴 때 기수가 아래로 감(항공 스타일).")]
    public bool invertLookY = false;

    [Header("부스트 방식")]
    [Tooltip("OFF: 누르고 있는 동안만 부스트(기본)\n" +
             "ON : 한 번 누르면 켜지고 다시 누르면 꺼짐(토글)\n" +
             "터치는 계속 누르고 있기 불편해서 토글이 나을 수 있음.")]
    public bool boostToggle = false;
}

// =====================================================================
// 패드 조합키 — 패드 버튼 여러 개를 동시에 눌렀을 때만 나가는 입력.
//
// 패드는 버튼 수가 모자라서 한 버튼에 두 기능을 얹어야 할 때가 생김.
// 예) L1 + △ = 스킬 사용   /   R1 + ✕ = 소모품 사용
//
// [인스펙터 사용법]
//   Buttons 에 같이 눌러야 할 패드 버튼을 2개 이상 넣고, Action 에서 시킬 행동을 고르면 됨.
//   행동 목록(INPUT_ACTION)은 InputManager가 내보내는 출력 필드와 1:1로 대응함.
// =====================================================================
[System.Serializable]
public class ComboBinding
{
    [Tooltip("인스펙터에서 알아보기 위한 이름. 동작에는 영향 없음.")]
    public string name = "새 조합키";

    [Tooltip("같이 누르고 있어야 하는 패드 버튼들.\n" +
             "여기 있는 버튼이 전부 눌려 있어야 아래 행동이 나감.\n" +
             "예) L1 + Triangle")]
    public List<GAMEPAD_BUTTON> buttons = new List<GAMEPAD_BUTTON>();

    [Tooltip("스틱 방향도 조건에 넣을지.\n" +
             "켜면 '버튼을 누른 채로 스틱을 특정 방향으로 민 상태'여야 성립함.\n" +
             "같은 버튼에 방향별로 다른 행동을 붙일 때 씀 — 예) L1+↑=스킬1, L1+↓=스킬2")]
    public bool useStick = false;

    [Tooltip("조건으로 볼 스틱. Left=이동 스틱, Right=시야 스틱")]
    public GAMEPAD_STICK stick = GAMEPAD_STICK.Left;

    [Tooltip("그 스틱을 어느 쪽으로 밀어야 하는지")]
    public STICK_DIR stickDirection = STICK_DIR.Up;

    [Tooltip("얼마나 밀어야 '민 것'으로 볼지(0~1). 낮으면 살짝만 기울여도 걸려서 오발동함.")]
    [Range(0.2f, 1f)]
    public float stickThreshold = 0.5f;

    [Tooltip("이 조합을 눌렀을 때 시킬 행동.")]
    public INPUT_ACTION action = INPUT_ACTION.None;

    [Tooltip("ON  : 누르고 있는 동안 계속 유지됨 (이동/부스트/연사용)\n" +
             "OFF : 조합이 완성되는 순간 한 프레임만 (토글/사용/전환용)")]
    public bool holdAction = false;

    [Tooltip("ON: 조합이 성립하는 동안 그 버튼들의 원래 단독 기능을 막음.\n" +
             "예) L1+△를 조합키로 쓸 때 L1의 부스트가 같이 나가는 것을 방지.\n" +
             "OFF: 단독 기능도 같이 나감(부스트+스킬처럼 겹쳐 쓰고 싶을 때).")]
    public bool suppressSingleKeys = true;

    // 직전 프레임에 조합이 성립해 있었는지 — '완성되는 순간'을 잡기 위한 것.
    // 저장할 값이 아니라 실행 중 상태라 직렬화 대상에서 뺌.
    [System.NonSerialized] public bool wasActive;
}

// =====================================================================
// 게임패드 키 설정
//
// 버튼은 번호가 아니라 '자리'로 잡힌다 — 패드마다 내부 버튼 번호가 달라서
// (같은 아래쪽 버튼이 Xbox는 0번, DualSense는 1번) 번호로 잡으면 패드를 바꿀 때마다 어긋남.
//
// [이름 ↔ 실제 위치] — 이름은 PS 표기지만 자리 기준이라 어느 패드든 같은 자리에 붙음
//   Cross(✕)=아래  Circle(○)=오른쪽  Square(□)=왼쪽  Triangle(△)=위    (Xbox면 A/B/X/Y)
//   L1/R1 = 위쪽 범퍼        L2/R2 = 아래쪽 트리거
//   L3/R3 = 스틱 누르기      Options=시작   Share=선택
//   Dpad* = 십자키
//   TouchpadClick = 듀얼쇽/듀얼센스 전용. 다른 패드에서는 안 눌림
//
// 축(스틱/트리거)도 장치에서 직접 읽으므로 Project Settings에 축을 등록할 필요가 없음.
// =====================================================================
[System.Serializable]
public class GamepadConfig
{
    // None이면 이 패드엔 미배정(안 눌림).
    // 물리 입력이 액션 수보다 적어 몇 개는 기본 None임 — 필요한 것만 인스펙터에서 배정하면 됨.

    [Header("이동/회전")]
    public GAMEPAD_BUTTON rollLeft  = GAMEPAD_BUTTON.PsSquare_XboxX;
    public GAMEPAD_BUTTON rollRight = GAMEPAD_BUTTON.PsCircle_XboxB;
    public GAMEPAD_BUTTON boost     = GAMEPAD_BUTTON.PsL1_XboxLB;
    public GAMEPAD_BUTTON dodge     = GAMEPAD_BUTTON.PsCross_XboxA;
	[Tooltip("상승. 기본 None = 배정 안 함(패드로 상승 안 됨).")]
	public GAMEPAD_BUTTON moveUp = GAMEPAD_BUTTON.None;

	[Tooltip("하강. 기본 None = 배정 안 함(패드로 하강 안 됨).")]
	public GAMEPAD_BUTTON moveDown = GAMEPAD_BUTTON.None;

	[Header("시야 옵션")]
	[Tooltip("오른쪽 스틱 상하(Y축) 반전. 켜면 스틱을 위로 밀 때 시야가 아래로 감(항공 스타일).")]
	public bool invertRStickY = false;

	[Header("사격")]
    public GAMEPAD_BUTTON fireBullet  = GAMEPAD_BUTTON.PsR2_XboxRT;
    public GAMEPAD_BUTTON fireMissile = GAMEPAD_BUTTON.PsL2_XboxLT;

    [Header("미사일 슬롯 전환")]
    public GAMEPAD_BUTTON missilePrev = GAMEPAD_BUTTON.DpadLeft;
    public GAMEPAD_BUTTON missileNext = GAMEPAD_BUTTON.DpadRight;

    [Header("소모품")]
    public GAMEPAD_BUTTON switchConsumable = GAMEPAD_BUTTON.DpadUp;
    public GAMEPAD_BUTTON useConsumable    = GAMEPAD_BUTTON.DpadDown;

    [Header("스킬")]
    public GAMEPAD_BUTTON switchSkillSlot = GAMEPAD_BUTTON.PsR1_XboxRB;
    public GAMEPAD_BUTTON useSkill        = GAMEPAD_BUTTON.PsTriangle_XboxY;

    [Header("락온 모드 전환")]
    public GAMEPAD_BUTTON toggleClusterLockMode = GAMEPAD_BUTTON.PsR3_XboxRS;

    [Header("락온 대상 전환")]
    public GAMEPAD_BUTTON lockOnPrev = GAMEPAD_BUTTON.PsL3_XboxLS;    // 이전 락온 대상 (키마 마우스휠 아래에 대응)
    public GAMEPAD_BUTTON lockOnNext = GAMEPAD_BUTTON.PsShare_XboxView; // 다음 락온 대상 (키마 마우스휠 위에 대응)

    [Header("UI 패널 토글")]
    public GAMEPAD_BUTTON fuelGaugeToggle = GAMEPAD_BUTTON.None; // 물리 버튼 부족 — 기본 미배정
    public GAMEPAD_BUTTON inventoryToggle = GAMEPAD_BUTTON.None; // 물리 버튼 부족 — 기본 미배정
    public GAMEPAD_BUTTON pauseMenu       = GAMEPAD_BUTTON.PsOptions_XboxMenu;
    public GAMEPAD_BUTTON mapToggle       = GAMEPAD_BUTTON.None; // 물리 버튼 부족 — 기본 미배정

    [Header("상호작용(STATION에서)")]
    public GAMEPAD_BUTTON interAct        = GAMEPAD_BUTTON.PsCross_XboxA; // 전투의 dodge와 씬 문맥이 달라 공유 무방

    // 스틱은 배정 항목이 없음 — 왼쪽=이동, 오른쪽=시야로 고정임.
    // 패드 종류와 무관하게 '왼쪽 스틱'이라는 개념으로 바로 읽히므로 고를 이유가 없음.

    [Header("부스트 방식")]
    [Tooltip("OFF: 누르고 있는 동안만 부스트(기본)\n" +
             "ON : 한 번 누르면 켜지고 다시 누르면 꺼짐(토글)\n" +
             "키보드 설정과 별개로 동작함 — 패드만 토글로 쓸 수 있음.")]
    public bool boostToggle = false;

    
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

    [Tooltip("VR(XR) 실행 중에는 게임패드 축을 읽지 않음. 퀘스트 등 VR 컨트롤러가 조이스틱으로 잡히면 " +
             "패드용 축 번호에 엉뚱하게 걸려 트리거가 눌린 것처럼 들어옴(총이 계속 발사되는 증상). " +
             "VR에서 실제 게임패드를 쓰려면 이 값을 꺼야 함.")]
    public bool ignoreGamepadDuringXR = true;

    [Space(5)]
    [Header("━━━━━━ 키보드/마우스 키 설정 ━━━━━━")]
    public KeyboardMouseConfig keyboardMouseConfig = new KeyboardMouseConfig();

    [Space(5)]
    [Header("━━━━━━ 게임패드 키 설정 ━━━━━━")]
    public GamepadConfig gamepadConfig = new GamepadConfig();

    [Space(5)]
    [Header("━━━━━━ 모바일 설정 ━━━━━━")]
    public MobileConfig mobileConfig = new MobileConfig();

    // 부스트 토글 상태(래치). 장치별로 따로 둠 —
    // 키보드는 꾹 누르기, 패드는 토글처럼 서로 다른 방식으로 쓸 수 있어야 하므로
    // 한 개를 공유하면 한쪽에서 켠 게 다른 쪽 설정에 끌려다니게 됨.
    private bool _kbBoostLatch;
    private bool _padBoostLatch;
    private bool _mobileBoostLatch;

    // 부스트 입력 해석. 장치마다 같은 규칙이라 한 곳에 모음.
    // 토글이면 '누른 순간'마다 켜고 끄고, 아니면 누르고 있는 동안만 true.
    // 토글이 아닐 땐 래치를 비워둠 — 안 그러면 토글로 켜둔 채 설정을 바꿨을 때 그 값이 남음.
    // 이번 프레임에 시야 입력(마우스 이동 / 패드 오른쪽 스틱)이 있었는지.
    // 자동 복귀를 걸어도 되는 시점인지 판단하는 데만 씀.
    private bool _lookInputActive;

    // 패드 스틱을 최대로 밀었을 때 조종간이 초당 얼마나 기울어지는지(감도 50 기준).
    // 1.5면 약 0.67초 만에 최대까지 감. 감도를 올리면 이 값에 배율이 곱해짐.
    private const float PadStickRateAtMid = 1.5f;

    // 손을 뗀 조종간을 중앙으로 되돌림. 마우스·패드 어느 쪽도 입력이 없을 때만 동작.
    // mouseStickAutoCenter가 0이면 안 돌아옴(엘리트 방식 — 직접 되돌려야 멈춤).
    private void ApplyStickAutoCenter()
    {
        if (_lookInputActive || mouseStickAutoCenter <= 0f)
        {
            return;
        }

        _mouseStick = Vector2.MoveTowards(_mouseStick, Vector2.zero, mouseStickAutoCenter * Time.deltaTime);
        lookInput = _mouseStick;
    }

    // 전진 입력이 없으면 켜둔 부스트 토글을 해제함.
    // 부스트는 전진 중에만 걸리므로(Player.MovingByInput), 가속을 멈춘 순간 토글도 풀리는 게 맞음.
    // 안 그러면 켜둔 채 손을 뗐다가 다시 밀 때 예고 없이 부스트로 튀어나감.
    private void ReleaseBoostToggleIfNotThrusting()
    {
        if (moveInput.z > ThrustReleaseThreshold)
        {
            return;
        }

        _kbBoostLatch = false;
        _padBoostLatch = false;
        _mobileBoostLatch = false;
        isBoosting = false;
    }

    // 전진을 '놓았다'고 볼 기준. 스틱이 완전히 0으로 안 돌아오는 것을 감안한 여유값.
    private const float ThrustReleaseThreshold = 0.1f;

    private static bool ResolveBoost(bool toggleMode, bool pressed, bool pressedDown, ref bool latch)
    {
        if (!toggleMode)
        {
            latch = false;
            return pressed;
        }

        if (pressedDown)
        {
            latch = !latch;
        }
        return latch;
    }

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
    [Header("━━━━━━ 조종 감도━━━━━━")]
    // 마우스와 패드는 장치 성격이 달라 같은 숫자라도 체감이 다름 — 값을 따로 둠.
    // 둘 다 의미는 같음: '조종간을 얼마나 쉽게 끝까지 꺾느냐'. 50 기준 25칸마다 두 배.

    [Tooltip("마우스 조종 감도(1~100).\n" +
             "50: 조종간을 끝까지 꺾는 데 약 500px / 100: 약 125px / 1: 약 2000px")]
    [Range(MinSensitivity, MaxSensitivity)]
    [SerializeField] private int mouseLookSensitivity = DefaultSensitivity;

    [Tooltip("패드 조종 감도(1~100). 오른쪽 스틱에 적용됨.\n" +
             "50: 스틱 기울기 그대로 / 100: 1/4만 밀어도 최대 / 1: 끝까지 밀어도 최대의 1/4")]
    [Range(MinSensitivity, MaxSensitivity)]
    [SerializeField] private int padLookSensitivity = DefaultSensitivity;

    [Tooltip("패드 스틱 응답 곡선. 스틱을 살짝 민 구간을 얼마나 둔하게 만들지.\n" +
             "1 = 곡선 없음(기울기 그대로 — 살짝만 밀어도 확 움직임)\n" +
             "2 = 권장. 중앙 근처가 둔해져 미세 조준이 쉬워짐\n" +
             "3 = 아주 둔함. 끝으로 갈수록 급격해짐\n" +
             "끝까지 밀었을 때의 최대치는 값과 무관하게 그대로임 — 최대 선회 속도는 안 깎임.")]
    [Range(1f, 3f)]
    [SerializeField] private float padLookCurve = 2f;

    public const int MinSensitivity = 1;
    public const int MaxSensitivity = 100;
    public const int DefaultSensitivity = 50;

    // 감도 슬라이더 값 → 실제 계수 환산.
    //
    // 선형으로 하지 않는 이유: 50을 현재 감도로 잡고 선형으로 펴면 1이 현재의 1/50이 되어
    // 조종간을 끝까지 꺾는 데 25,000px이 필요해짐 — 아래쪽 절반이 통째로 못 쓰게 됨.
    // 배율로 두면 25칸마다 두 배씩 변해서 1~100이 고르게 쓰임(양끝 16배 차이).
    private const float SensitivityAtMid = 0.002f;  // 슬라이더 50일 때의 계수
    private const float SensitivityRange = 4f;      // 50→100이 4배, 50→1이 1/4배

    /// <summary>옵션 UI용 마우스 조종 감도(1~100). 슬라이더 초기값에 씀.</summary>
    public int MouseLookSensitivity => mouseLookSensitivity;

    /// <summary>옵션 UI용 패드 조종 감도(1~100). 슬라이더 초기값에 씀.</summary>
    public int PadLookSensitivity => padLookSensitivity;

    /// <summary>옵션 UI에서 호출. 범위를 벗어난 값은 잘라내고 기록까지 함.</summary>
    public void SetMouseLookSensitivity(int value)
    {
        mouseLookSensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
        WriteSettings();
    }

    /// <summary>옵션 UI에서 호출. 범위를 벗어난 값은 잘라내고 기록까지 함.</summary>
    public void SetPadLookSensitivity(int value)
    {
        padLookSensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
        WriteSettings();
    }

    // =====================================================================
    // 설정 저장 / 복원
    //
    // SoundSettingsUI와 같은 방식 — 값이 바뀌면 즉시 PlayerPrefs에 기록하고,
    // 실제 디스크 확정(PlayerPrefs.Save)은 세팅창을 닫을 때 한 번만 한다.
    // 슬라이더를 끄는 동안 매 프레임 디스크에 쓰면 렉이 걸리기 때문.
    //
    // 항목이 늘어도 이 코드는 안 늘어남 — InputSettings에 필드만 추가하면 됨.
    // =====================================================================
    private const string SettingsKey = "Input.Settings";

    /// <summary>현재 설정을 PlayerPrefs에 기록. 디스크 확정은 CommitSettings()가 함.</summary>
    public void WriteSettings()
    {
        PlayerPrefs.SetString(SettingsKey, JsonUtility.ToJson(BuildSettings()));
    }

    /// <summary>디스크에 확정 저장. 세팅창을 닫을 때 한 번 호출할 것.</summary>
    public void CommitSettings()
    {
        WriteSettings();
        PlayerPrefs.Save();
    }

    /// <summary>저장된 설정을 불러와 적용. 저장값이 없으면 프리팹 값을 그대로 씀(첫 실행).</summary>
    public void LoadSettings()
    {
        if (!PlayerPrefs.HasKey(SettingsKey))
        {
            return;
        }

        InputSettings s = JsonUtility.FromJson<InputSettings>(PlayerPrefs.GetString(SettingsKey));
        if (s == null)
        {
            return;
        }

        ApplySettings(s);
    }

    // 현재 상태 → 저장용 묶음
    private InputSettings BuildSettings()
    {
        InputSettings s = new InputSettings
        {
            mouseLookSensitivity = mouseLookSensitivity,
            padLookSensitivity   = padLookSensitivity,
            padLookCurve         = padLookCurve,

            mouseInvertLookY  = keyboardMouseConfig.invertLookY,
            padInvertLookY    = gamepadConfig.invertRStickY,
            mobileInvertLookY = mobileConfig.invertLookY,

            mouseBoostToggle  = keyboardMouseConfig.boostToggle,
            padBoostToggle    = gamepadConfig.boostToggle,
            mobileBoostToggle = mobileConfig.boostToggle,
        };

        // 키 배정은 enum이라 int로 눕혀서 저장 — 순서가 곧 항목이므로 아래 Apply와 순서가 같아야 함.
        s.keyboardBindings = CollectKeyboardBindings();
        s.gamepadBindings  = CollectGamepadBindings();

        if (comboBindings != null)
        {
            for (int i = 0; i < comboBindings.Count; i++)
            {
                ComboBinding c = comboBindings[i];
                if (c == null)
                {
                    continue;
                }

                SavedCombo sc = new SavedCombo
                {
                    name               = c.name,
                    action             = (int)c.action,
                    holdAction         = c.holdAction,
                    suppressSingleKeys = c.suppressSingleKeys,
                    useStick           = c.useStick,
                    stick              = (int)c.stick,
                    stickDirection     = (int)c.stickDirection,
                    stickThreshold     = c.stickThreshold,
                };

                if (c.buttons != null)
                {
                    for (int b = 0; b < c.buttons.Count; b++)
                    {
                        sc.buttons.Add((int)c.buttons[b]);
                    }
                }
                s.combos.Add(sc);
            }
        }

        return s;
    }

    // 저장 묶음 → 현재 상태
    private void ApplySettings(InputSettings s)
    {
        mouseLookSensitivity = Mathf.Clamp(s.mouseLookSensitivity, MinSensitivity, MaxSensitivity);
        padLookSensitivity   = Mathf.Clamp(s.padLookSensitivity,   MinSensitivity, MaxSensitivity);
        padLookCurve         = Mathf.Clamp(s.padLookCurve, 1f, 3f);

        keyboardMouseConfig.invertLookY  = s.mouseInvertLookY;
        gamepadConfig.invertRStickY      = s.padInvertLookY;
        mobileConfig.invertLookY         = s.mobileInvertLookY;

        keyboardMouseConfig.boostToggle  = s.mouseBoostToggle;
        gamepadConfig.boostToggle        = s.padBoostToggle;
        mobileConfig.boostToggle         = s.mobileBoostToggle;

        ApplyKeyboardBindings(s.keyboardBindings);
        ApplyGamepadBindings(s.gamepadBindings);

        // 조합키는 통째로 교체 — 저장된 목록이 곧 사용자가 만든 목록임.
        comboBindings = new List<ComboBinding>();
        if (s.combos == null)
        {
            return;
        }

        for (int i = 0; i < s.combos.Count; i++)
        {
            SavedCombo sc = s.combos[i];
            ComboBinding c = new ComboBinding
            {
                name               = sc.name,
                action             = (INPUT_ACTION)sc.action,
                holdAction         = sc.holdAction,
                suppressSingleKeys = sc.suppressSingleKeys,
                useStick           = sc.useStick,
                stick              = (GAMEPAD_STICK)sc.stick,
                stickDirection     = (STICK_DIR)sc.stickDirection,
                stickThreshold     = sc.stickThreshold,
                buttons            = new List<GAMEPAD_BUTTON>(),
            };

            if (sc.buttons != null)
            {
                for (int b = 0; b < sc.buttons.Count; b++)
                {
                    c.buttons.Add((GAMEPAD_BUTTON)sc.buttons[b]);
                }
            }
            comboBindings.Add(c);
        }
    }

    // 키 배정 저장/복원.
    //
    // ⚠ Collect와 Apply의 순서가 반드시 같아야 함 — 목록에 순서로만 담기므로
    //   한쪽에만 항목을 추가하면 그 아래 키들이 통째로 밀려서 엉뚱하게 배정됨.
    //   항목을 추가할 때는 두 메서드의 '같은 자리'에 같이 넣을 것.
    //   개수가 안 맞으면(설정 항목이 늘어난 구버전 저장값 등) 복원을 통째로 건너뛰어
    //   프리팹 기본값을 쓰게 함 — 어긋난 채로 적용하는 것보다 안전함.
    // 키 배정 저장/복원 — 어떤 행동인가(INPUT_ACTION)를 열쇠로 씀.
    //
    // 순서로 저장하지 않는 이유: 항목을 중간에 하나 끼워넣으면 그 아래가 전부 밀려서
    // 저장해둔 키가 엉뚱한 행동에 들어감. 행동 값으로 짝지으면 항목이 늘든 순서가 바뀌든 안전하고,
    // 저장값에 없는 항목은 프리팹 기본값 그대로 남음.
    //
    // 아래 두 표(Collect/Apply)에 같은 행동이 들어 있어야 그 키가 저장·복원됨.
    // 새 조작을 추가하면 INPUT_ACTION에 항목을 만들고 두 표에 한 줄씩 넣으면 됨.
    private List<SavedBind> CollectKeyboardBindings()
    {
        var km = keyboardMouseConfig;
        return new List<SavedBind>
        {
            Bind(INPUT_ACTION.MoveForward,  (int)km.moveForward),
            Bind(INPUT_ACTION.MoveBack,     (int)km.moveBack),
            Bind(INPUT_ACTION.MoveLeft,     (int)km.moveLeft),
            Bind(INPUT_ACTION.MoveRight,    (int)km.moveRight),
            Bind(INPUT_ACTION.MoveUp,       (int)km.moveUp),
            Bind(INPUT_ACTION.MoveDown,     (int)km.moveDown),
            Bind(INPUT_ACTION.RollLeft,     (int)km.rollLeft),
            Bind(INPUT_ACTION.RollRight,    (int)km.rollRight),
            Bind(INPUT_ACTION.Boost,        (int)km.boost),
            Bind(INPUT_ACTION.Dodge,        (int)km.dodge),
            Bind(INPUT_ACTION.FireBullet,   (int)km.fireBullet),
            Bind(INPUT_ACTION.FireMissile,  (int)km.fireMissile),
            Bind(INPUT_ACTION.MissilePrev,  (int)km.missilePrev),
            Bind(INPUT_ACTION.MissileNext,  (int)km.missileNext),
            Bind(INPUT_ACTION.SwitchConsumable, (int)km.switchConsumable),
            Bind(INPUT_ACTION.UseConsumable,    (int)km.useConsumable),
            Bind(INPUT_ACTION.SwitchSkillSlot,  (int)km.switchSkillSlot),
            Bind(INPUT_ACTION.UseSkill,         (int)km.useSkill),
            Bind(INPUT_ACTION.FuelGaugeToggle,  (int)km.fuelGaugeToggle),
            Bind(INPUT_ACTION.InventoryToggle,  (int)km.inventoryToggle),
            Bind(INPUT_ACTION.PauseMenu,        (int)km.pauseMenu),
            Bind(INPUT_ACTION.MapToggle,        (int)km.mapToggle),
            Bind(INPUT_ACTION.Interact,         (int)km.interAct),
            Bind(INPUT_ACTION.ToggleClusterLockMode, (int)km.toggleClusterLockMode),
        };
    }

    private void ApplyKeyboardBindings(List<SavedBind> list)
    {
        if (list == null)
        {
            return;
        }

        var km = keyboardMouseConfig;
        for (int i = 0; i < list.Count; i++)
        {
            KeyCode code = (KeyCode)list[i].code;
            switch ((INPUT_ACTION)list[i].action)
            {
                case INPUT_ACTION.MoveForward:  km.moveForward = code; break;
                case INPUT_ACTION.MoveBack:     km.moveBack = code; break;
                case INPUT_ACTION.MoveLeft:     km.moveLeft = code; break;
                case INPUT_ACTION.MoveRight:    km.moveRight = code; break;
                case INPUT_ACTION.MoveUp:       km.moveUp = code; break;
                case INPUT_ACTION.MoveDown:     km.moveDown = code; break;
                case INPUT_ACTION.RollLeft:     km.rollLeft = code; break;
                case INPUT_ACTION.RollRight:    km.rollRight = code; break;
                case INPUT_ACTION.Boost:        km.boost = code; break;
                case INPUT_ACTION.Dodge:        km.dodge = code; break;
                case INPUT_ACTION.FireBullet:   km.fireBullet = code; break;
                case INPUT_ACTION.FireMissile:  km.fireMissile = code; break;
                case INPUT_ACTION.MissilePrev:  km.missilePrev = code; break;
                case INPUT_ACTION.MissileNext:  km.missileNext = code; break;
                case INPUT_ACTION.SwitchConsumable: km.switchConsumable = code; break;
                case INPUT_ACTION.UseConsumable:    km.useConsumable = code; break;
                case INPUT_ACTION.SwitchSkillSlot:  km.switchSkillSlot = code; break;
                case INPUT_ACTION.UseSkill:         km.useSkill = code; break;
                case INPUT_ACTION.FuelGaugeToggle:  km.fuelGaugeToggle = code; break;
                case INPUT_ACTION.InventoryToggle:  km.inventoryToggle = code; break;
                case INPUT_ACTION.PauseMenu:        km.pauseMenu = code; break;
                case INPUT_ACTION.MapToggle:        km.mapToggle = code; break;
                case INPUT_ACTION.Interact:         km.interAct = code; break;
                case INPUT_ACTION.ToggleClusterLockMode: km.toggleClusterLockMode = code; break;
            }
        }
    }

    private List<SavedBind> CollectGamepadBindings()
    {
        var gp = gamepadConfig;
        return new List<SavedBind>
        {
            Bind(INPUT_ACTION.RollLeft,     (int)gp.rollLeft),
            Bind(INPUT_ACTION.RollRight,    (int)gp.rollRight),
            Bind(INPUT_ACTION.Boost,        (int)gp.boost),
            Bind(INPUT_ACTION.Dodge,        (int)gp.dodge),
            Bind(INPUT_ACTION.MoveUp,       (int)gp.moveUp),
            Bind(INPUT_ACTION.MoveDown,     (int)gp.moveDown),
            Bind(INPUT_ACTION.FireBullet,   (int)gp.fireBullet),
            Bind(INPUT_ACTION.FireMissile,  (int)gp.fireMissile),
            Bind(INPUT_ACTION.MissilePrev,  (int)gp.missilePrev),
            Bind(INPUT_ACTION.MissileNext,  (int)gp.missileNext),
            Bind(INPUT_ACTION.SwitchConsumable, (int)gp.switchConsumable),
            Bind(INPUT_ACTION.UseConsumable,    (int)gp.useConsumable),
            Bind(INPUT_ACTION.SwitchSkillSlot,  (int)gp.switchSkillSlot),
            Bind(INPUT_ACTION.UseSkill,         (int)gp.useSkill),
            Bind(INPUT_ACTION.ToggleClusterLockMode, (int)gp.toggleClusterLockMode),
            Bind(INPUT_ACTION.LockOnPrev,   (int)gp.lockOnPrev),
            Bind(INPUT_ACTION.LockOnNext,   (int)gp.lockOnNext),
            Bind(INPUT_ACTION.FuelGaugeToggle,  (int)gp.fuelGaugeToggle),
            Bind(INPUT_ACTION.InventoryToggle,  (int)gp.inventoryToggle),
            Bind(INPUT_ACTION.PauseMenu,        (int)gp.pauseMenu),
            Bind(INPUT_ACTION.MapToggle,        (int)gp.mapToggle),
            Bind(INPUT_ACTION.Interact,         (int)gp.interAct),
        };
    }

    private void ApplyGamepadBindings(List<SavedBind> list)
    {
        if (list == null)
        {
            return;
        }

        var gp = gamepadConfig;
        for (int i = 0; i < list.Count; i++)
        {
            GAMEPAD_BUTTON b = (GAMEPAD_BUTTON)list[i].code;
            switch ((INPUT_ACTION)list[i].action)
            {
                case INPUT_ACTION.RollLeft:     gp.rollLeft = b; break;
                case INPUT_ACTION.RollRight:    gp.rollRight = b; break;
                case INPUT_ACTION.Boost:        gp.boost = b; break;
                case INPUT_ACTION.Dodge:        gp.dodge = b; break;
                case INPUT_ACTION.MoveUp:       gp.moveUp = b; break;
                case INPUT_ACTION.MoveDown:     gp.moveDown = b; break;
                case INPUT_ACTION.FireBullet:   gp.fireBullet = b; break;
                case INPUT_ACTION.FireMissile:  gp.fireMissile = b; break;
                case INPUT_ACTION.MissilePrev:  gp.missilePrev = b; break;
                case INPUT_ACTION.MissileNext:  gp.missileNext = b; break;
                case INPUT_ACTION.SwitchConsumable: gp.switchConsumable = b; break;
                case INPUT_ACTION.UseConsumable:    gp.useConsumable = b; break;
                case INPUT_ACTION.SwitchSkillSlot:  gp.switchSkillSlot = b; break;
                case INPUT_ACTION.UseSkill:         gp.useSkill = b; break;
                case INPUT_ACTION.ToggleClusterLockMode: gp.toggleClusterLockMode = b; break;
                case INPUT_ACTION.LockOnPrev:   gp.lockOnPrev = b; break;
                case INPUT_ACTION.LockOnNext:   gp.lockOnNext = b; break;
                case INPUT_ACTION.FuelGaugeToggle:  gp.fuelGaugeToggle = b; break;
                case INPUT_ACTION.InventoryToggle:  gp.inventoryToggle = b; break;
                case INPUT_ACTION.PauseMenu:        gp.pauseMenu = b; break;
                case INPUT_ACTION.MapToggle:        gp.mapToggle = b; break;
                case INPUT_ACTION.Interact:         gp.interAct = b; break;
            }
        }
    }

    // 저장 한 줄 만들기 — 위 표를 짧게 쓰기 위한 도우미.
    private static SavedBind Bind(INPUT_ACTION action, int code)
    {
        return new SavedBind { action = (int)action, code = code };
    }
    // 슬라이더 값(1~100) → 50 기준 배율. 50이면 1배, 25칸마다 2배.
    private static float SensitivityScale(int value)
    {
        return Mathf.Pow(SensitivityRange, (value - DefaultSensitivity) / 50f);
    }

    // 마우스 이동량(픽셀)에 곱해지는 계수.
    // 픽셀을 조종간 기울기로 환산해야 해서 기준 계수가 따로 필요함.
    private float MouseSensitivityFactor
    {
        get { return SensitivityAtMid * SensitivityScale(mouseLookSensitivity); }
    }

    // 패드 스틱 기울기에 곱해지는 배율.
    // 스틱은 이미 -1~1로 들어오므로 픽셀 환산이 필요 없고 배율만 곱함 —
    // 50이면 스틱 그대로, 크면 덜 밀어도 최대에 닿고, 작으면 끝까지 밀어도 최대에 못 미침.
    private float PadLookFactor
    {
        get { return SensitivityScale(padLookSensitivity); }
    }

    // 스틱 응답 곡선 — 기울기 '크기'에만 지수를 먹임. 방향은 그대로 유지.
    //
    // 축별로 따로 먹이면 대각선에서 크기가 줄어 방향이 틀어짐(원이 사각형처럼 찌그러짐).
    // 크기에만 적용하면 방향이 보존되고, 크기 1(끝까지 밀기)은 1의 거듭제곱이라 그대로 1 —
    // 즉 최대 선회 속도는 곡선을 아무리 세게 줘도 안 깎이고, 중앙 근처만 둔해짐.
    private Vector2 ApplyLookCurve(Vector2 stick)
    {
        float magnitude = stick.magnitude;
        if (magnitude <= 0.0001f || padLookCurve <= 1f)
        {
            return stick;
        }

        float curved = Mathf.Pow(Mathf.Min(magnitude, 1f), padLookCurve);
        return stick / magnitude * curved;
    }

    [Tooltip("마우스를 멈췄을 때 조종간이 중앙으로 돌아오는 속도(초당 기울기량).\n" +
             "0이면 안 돌아옴(엘리트 방식 — 직접 되돌려야 멈춤). 1~2면 손 떼면 서서히 수평 복귀.")]
    public float mouseStickAutoCenter = 0f;

    // 마우스로 누적한 가상 조종간 기울기(-1~1). 마우스 전용 내부값임.
    // UI에 조종간 위치를 그릴 때는 이걸 쓰면 안 됨 — 패드로 조종할 땐 0에 머물러서 안 움직임.
    // 화면 표시는 아래 LookStick(장치 병합 결과)을 쓸 것.
    public Vector2 MouseStick => _mouseStick;

    /// <summary>
    /// 현재 조종간 기울기(-1~1). 마우스든 패드든 실제로 조종에 쓰이는 최종값.
    /// 크로스헤어처럼 '지금 어디를 가리키고 있나'를 그리는 UI는 이 값을 읽을 것.
    /// </summary>
    public Vector2 LookStick => lookInput;
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


    // === 키마+패드 자동 병합/전환 ===
    // 마지막으로 실제 입력한 장치. UI가 이 값을 구독해 프롬프트(키/패드) 아이콘을 바꾸면 됨.
    public INPUT_CONTROL_TYPE LastUsedDevice { get; private set; } = INPUT_CONTROL_TYPE.KEYBOARD_MOUSE;

    // 패드가 하나라도 연결돼 있는지(자동 인식용).
    public bool IsGamepadConnected
    {
        get { return Gamepad.current != null; }
    }


    // 마우스 시야/휠 축 이름(Unity 기본값 — 안 바뀜).
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

            // 저장해둔 입력 설정 복원. 없으면 프리팹 값을 그대로 씀(첫 실행).
            LoadSettings();
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

        // 이번 프레임 시야 입력 여부는 매 프레임 새로 판정 — 두 장치가 각자 세움
        _lookInputActive = false;

        ReadKeyboardMouse();  // 키보드/마우스 값으로 먼저 채움

        // 패드가 연결됐을 때만 오버레이 — 연결 안 됐으면 패드 축을 읽지 않음.
        // (패드용 커스텀 축이 Project Settings에 없으면 GetAxisRaw가 예외를 던지므로, 키보드만 쓸 땐 아예 스킵)
        // VR 실행 중에는 스킵 — VR 컨트롤러가 조이스틱으로 잡혀 패드 축에 엉뚱하게 걸리면
        // 트리거가 계속 눌린 것처럼 들어와 총이 멈추지 않는다.
        _padActive = false;
        if (IsGamepadConnected && !(ignoreGamepadDuringXR && XRRuntimeManager.IsRunning))
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

        // 조종간 자동 복귀 — 마우스도 패드도 손을 뗀 상태일 때만.
        // 두 장치를 다 읽은 뒤에 해야 패드로 밀고 있는 중에 중앙으로 끌려가지 않음.
        ApplyStickAutoCenter();

        // 부스트 토글 자동 해제 — 전진을 놓으면 켜둔 토글도 같이 풀린다.
        // 병합이 끝난 뒤에 봐야 함(키마로 놓고 패드로 밀고 있는 경우까지 합쳐진 최종 입력 기준).
        // 이게 없으면 전진을 놓아도 토글이 계속 켜져 있다가, 다시 밀 때 갑자기 부스트로 튀어나감.
        ReleaseBoostToggleIfNotThrusting();

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
                    for (int k = 0; k < combo.buttons.Count; k++)
                    {
                        ApplyAction(FindActionForPadButton(combo.buttons[k]), false);
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

    // 등록된 조건(버튼 + 스틱 방향)이 전부 성립하는지.
    //
    // 조건이 2개 미만이면 조합키가 아니므로 성립 안 시킴 —
    // 1개짜리를 허용하면 단독 버튼 설정과 겹쳐서 어느 쪽이 이겼는지 알 수 없게 됨.
    // 스틱 조건도 조건 1개로 셈함(버튼 1개 + 스틱 1개면 조합으로 인정).
    private bool IsComboHeld(ComboBinding combo)
    {
        if (Gamepad.current == null || combo.buttons == null)
        {
            return false;
        }

        int conditionCount = combo.buttons.Count + (combo.useStick ? 1 : 0);
        if (conditionCount < 2)
        {
            return false;
        }

        for (int i = 0; i < combo.buttons.Count; i++)
        {
            if (!GetPad(combo.buttons[i]))
            {
                return false;
            }
        }

        if (combo.useStick && !IsStickPushed(combo.stick, combo.stickDirection, combo.stickThreshold, combo.action))
        {
            return false;
        }

        return true;
    }

    // 스틱이 지정한 방향으로 밀려 있는지.
    //
    // [우세축 방식]
    // X와 Y 중 더 많이 민 쪽 하나만 방향으로 인정함. 십자키처럼 한 번에 한 방향만 성립함.
    // 성분을 따로 보면 대각선(↗)에서 Up과 Right가 동시에 성립해서, 같은 버튼에 방향별
    // 조합을 걸어두면 두 행동이 같이 나가버림.
    // 크기도 같이 봐서 살짝 기울인 것은 무시함(성분만 보면 거의 안 밀어도 걸림).
    //
    // [반전 연동]
    // 오른쪽 스틱 상하 반전(invertRStickY)을 켜면 기체 조종이 뒤집히므로, 상하이동 조합도
    // 같은 손놀림이 되도록 같이 뒤집음 — 배정은 그대로 두고 '밀어야 하는 방향'만 반대가 됨.
    //   예) L1+Up → MoveUp 배정 시   반전 OFF=위로 밀기 / 반전 ON=아래로 밀기
    // 상하이동(MoveUp/MoveDown)에만 적용함. 스킬 같은 다른 행동까지 뒤집으면
    // 인스펙터에 적힌 방향과 손이 따로 놀아 헷갈리기만 함.
    private bool IsStickPushed(GAMEPAD_STICK stick, STICK_DIR dir, float threshold, INPUT_ACTION action)
    {
        Gamepad pad = Gamepad.current;
        if (pad == null)
        {
            return false;
        }

        Vector2 v = stick == GAMEPAD_STICK.Left
            ? pad.leftStick.ReadValue()
            : pad.rightStick.ReadValue();

        bool invertVertical = gamepadConfig.invertRStickY
                           && stick == GAMEPAD_STICK.Right
                           && (action == INPUT_ACTION.MoveUp || action == INPUT_ACTION.MoveDown);
        if (invertVertical)
        {
            v.y = -v.y;
        }

        // 살짝 기울인 것은 방향으로 안 봄
        if (v.sqrMagnitude < threshold * threshold)
        {
            return false;
        }

        // 더 많이 민 축으로만 판정 — 부호 반전은 크기를 안 바꾸므로 우세축 결과에 영향 없음
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
        {
            return dir == (v.x > 0f ? STICK_DIR.Right : STICK_DIR.Left);
        }
        return dir == (v.y > 0f ? STICK_DIR.Up : STICK_DIR.Down);
    }

    // 이 패드 버튼에 원래 물려 있던 단독 행동을 찾음. 없으면 None.
    // 조합키가 성립했을 때 그 단독 기능을 꺼주기 위한 역참조라, 패드 설정만 보면 됨.
    private INPUT_ACTION FindActionForPadButton(GAMEPAD_BUTTON b)
    {
        // None끼리 비교되면 미배정 항목에 전부 걸려버리므로 먼저 걸러냄
        if (b == GAMEPAD_BUTTON.None)
        {
            return INPUT_ACTION.None;
        }

        var gp = gamepadConfig;

        if (b == gp.moveUp)    return INPUT_ACTION.MoveUp;
        if (b == gp.moveDown)  return INPUT_ACTION.MoveDown;
        if (b == gp.rollLeft)  return INPUT_ACTION.RollLeft;
        if (b == gp.rollRight) return INPUT_ACTION.RollRight;
        if (b == gp.boost)     return INPUT_ACTION.Boost;
        if (b == gp.dodge)     return INPUT_ACTION.Dodge;

        if (b == gp.fireBullet)  return INPUT_ACTION.FireBullet;
        if (b == gp.fireMissile) return INPUT_ACTION.FireMissile;

        if (b == gp.missilePrev) return INPUT_ACTION.MissilePrev;
        if (b == gp.missileNext) return INPUT_ACTION.MissileNext;

        if (b == gp.lockOnPrev) return INPUT_ACTION.LockOnPrev;
        if (b == gp.lockOnNext) return INPUT_ACTION.LockOnNext;

        if (b == gp.switchConsumable) return INPUT_ACTION.SwitchConsumable;
        if (b == gp.useConsumable)    return INPUT_ACTION.UseConsumable;

        if (b == gp.switchSkillSlot) return INPUT_ACTION.SwitchSkillSlot;
        if (b == gp.useSkill)        return INPUT_ACTION.UseSkill;

        if (b == gp.fuelGaugeToggle) return INPUT_ACTION.FuelGaugeToggle;
        if (b == gp.inventoryToggle) return INPUT_ACTION.InventoryToggle;
        if (b == gp.pauseMenu)       return INPUT_ACTION.PauseMenu;
        if (b == gp.mapToggle)       return INPUT_ACTION.MapToggle;

        if (b == gp.interAct)              return INPUT_ACTION.Interact;
        if (b == gp.toggleClusterLockMode) return INPUT_ACTION.ToggleClusterLockMode;

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
        // 토글로 켜둔 부스트도 같이 해제 — 메뉴 열었다 닫으면 계속 부스트가 걸려 있게 되므로
        _kbBoostLatch = false;
        _padBoostLatch = false;
        _mobileBoostLatch = false;
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
            // 마우스 이동량은 장치에서 직접(픽셀 단위) 읽음.
            // 레거시 GetAxisRaw("Mouse X")는 Project Settings의 축 sensitivity(0.1)가 한 번 더
            // 곱해져서 감도 손잡이가 두 군데로 갈라짐 — 그쪽을 건드리면 이 감도값 의미가 통째로 바뀜.
            // 장치에서 바로 읽으면 곱하는 곳이 여기 하나뿐이라 값이 흔들리지 않음.
            Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            // 상하 반전은 누적 단계에서 적용 — 조종간(크로스헤어) 위치까지 같이 뒤집혀야
            // 손이 미는 방향과 화면에 보이는 조종간이 어긋나지 않음.
            if (km.invertLookY)
            {
                mouseDelta.y = -mouseDelta.y;
            }
            _mouseStick += mouseDelta * MouseSensitivityFactor;
            // 원형으로 제한 — 대각선이 축별로 잘려서 더 빨라지는 현상 방지
            _mouseStick = Vector2.ClampMagnitude(_mouseStick, 1f);

            if (mouseDelta.sqrMagnitude > 0f)
            {
                _lookInputActive = true;
            }
        }
        lookInput = _mouseStick;
        // 자동 복귀는 여기서 하지 않음 — 패드를 읽기 전이라, 패드로 밀고 있는 중에도
        // 중앙으로 끌어당겨 서로 싸우게 됨. 두 장치를 다 읽은 뒤 ApplyStickAutoCenter()가 처리함.

        // 부스트 / 회피
        isBoosting = ResolveBoost(km.boostToggle,
                                  Input.GetKey(km.boost),
                                  Input.GetKeyDown(km.boost),
                                  ref _kbBoostLatch);
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

        // --- 아날로그: 패드가 데드존 넘게 들어오면 키마 값을 덮어씀(패드 우선) ---
        // 이동 (왼쪽 스틱) — 스틱 크기가 그대로 반영돼 살짝=살살 / 확=빠르게(MovingByInput이 크기 보존).
        // 스틱은 장치에서 바로 읽음 — 왼쪽=이동, 오른쪽=시야로 고정.
        // (예전엔 어느 축을 쓸지 인스펙터에서 고르게 했는데, 이제 패드 종류와 무관하게
        //  '왼쪽 스틱'이라는 개념 자체로 읽히므로 고를 이유가 없음)
        Vector2 lStick = Gamepad.current.leftStick.ReadValue();
        // 상하이동은 배정한 버튼으로 — 키보드(Mouse4/Mouse3)와 같은 디지털 입력임.
        // 배정 안 하면(None) 양쪽 다 false라 0이 되어 아무 일도 안 일어남.
        float padVertical = (GetPad(gp.moveUp) ? 1f : 0f) + (GetPad(gp.moveDown) ? -1f : 0f);
        Vector3 padMove = new Vector3
        (
            lStick.x,
            padVertical,
            lStick.y
        );
        bool padMoving = padMove.sqrMagnitude > StickActiveDeadzone * StickActiveDeadzone;
        if (padMoving)
        {
            moveInput = padMove;
        }

        // 시야 (오른쪽 스틱) — 마우스와 같은 '가상 조종간 누적' 방식.
        //
        // 스틱 위치를 조종간에 바로 대입하면 끝까지 미는 순간 한 프레임에 최대가 돼서
        // 마우스(500px 밀어야 최대)와 감각이 완전히 달라짐.
        // 그래서 스틱 기울기를 '조종간이 움직이는 속도'로 쓰고 같은 _mouseStick에 누적함 —
        // 밀고 있으면 계속 기울다가 최대에서 멈추고, 놓으면 자동 복귀가 중앙으로 되돌림.
        // 응답 곡선은 그 속도에 걸림(중앙 근처에서 천천히 = 미세 조준).
        Vector2 rStick = ApplyLookCurve(Gamepad.current.rightStick.ReadValue());
        if (gp.invertRStickY)
        {
            rStick.y = -rStick.y;
        }

        bool padLooking = rStick.sqrMagnitude > StickActiveDeadzone * StickActiveDeadzone;
        if (padLooking)
        {
            _mouseStick += rStick * (PadStickRateAtMid * PadLookFactor) * Time.deltaTime;
            _mouseStick = Vector2.ClampMagnitude(_mouseStick, 1f);
            lookInput = _mouseStick;
            _lookInputActive = true;
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
        bool padBoost    = ResolveBoost(gp.boostToggle,
                                        GetPad(gp.boost),
                                        GetPadDown(gp.boost),
                                        ref _padBoostLatch);
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

    }

    // =====================================================================
    // 게임패드 버튼 판정 — GAMEPAD_BUTTON을 패드의 실제 컨트롤로 바꿔서 읽음.
    // 트리거·십자키도 전부 버튼처럼 눌림 판정이 되므로 축으로 따로 읽거나
    // 직전 프레임과 비교해 '누른 순간'을 직접 계산할 필요가 없음.
    // =====================================================================

    // 누르는 동안 true
    private bool GetPad(GAMEPAD_BUTTON b)
    {
        ButtonControl c = GetPadControl(b);
        return c != null && c.isPressed;
    }

    // 누른 순간 한 프레임만 true
    private bool GetPadDown(GAMEPAD_BUTTON b)
    {
        ButtonControl c = GetPadControl(b);
        return c != null && c.wasPressedThisFrame;
    }

    // GAMEPAD_BUTTON → 실제 패드 컨트롤.
    //
    // 번호(JoystickButton0~)가 아니라 '자리'로 잡는다:
    //   buttonSouth = 아래쪽 버튼 = PS ✕ = Xbox A
    //   buttonWest  = 왼쪽 버튼   = PS □ = Xbox X
    // 이렇게 하면 DualSense/Xbox처럼 내부 버튼 번호가 다른 패드를 꽂아도
    // 같은 자리 버튼이 같은 기능으로 잡힌다(번호로 잡으면 패드마다 어긋남).
    //
    // 트리거·D패드도 여기서 같이 처리됨 — 새 입력 시스템에서는 둘 다 버튼처럼 눌림 판정이 되므로
    // 예전처럼 축으로 따로 읽고 직접 엣지 판정할 필요가 없다.
    private ButtonControl GetPadControl(GAMEPAD_BUTTON b)
    {
        Gamepad pad = Gamepad.current;
        if (pad == null)
        {
            return null;
        }

        switch (b)
        {
            case GAMEPAD_BUTTON.PsCross_XboxA:     return pad.buttonSouth;
            case GAMEPAD_BUTTON.PsCircle_XboxB:    return pad.buttonEast;
            case GAMEPAD_BUTTON.PsSquare_XboxX:    return pad.buttonWest;
            case GAMEPAD_BUTTON.PsTriangle_XboxY:  return pad.buttonNorth;
            case GAMEPAD_BUTTON.PsL1_XboxLB:        return pad.leftShoulder;
            case GAMEPAD_BUTTON.PsR1_XboxRB:        return pad.rightShoulder;
            case GAMEPAD_BUTTON.PsL2_XboxLT:        return pad.leftTrigger;
            case GAMEPAD_BUTTON.PsR2_XboxRT:        return pad.rightTrigger;
            case GAMEPAD_BUTTON.PsL3_XboxLS:        return pad.leftStickButton;
            case GAMEPAD_BUTTON.PsR3_XboxRS:        return pad.rightStickButton;
            case GAMEPAD_BUTTON.PsOptions_XboxMenu:   return pad.startButton;
            case GAMEPAD_BUTTON.PsShare_XboxView:     return pad.selectButton;
            case GAMEPAD_BUTTON.DpadUp:    return pad.dpad.up;
            case GAMEPAD_BUTTON.DpadDown:  return pad.dpad.down;
            case GAMEPAD_BUTTON.DpadLeft:  return pad.dpad.left;
            case GAMEPAD_BUTTON.DpadRight: return pad.dpad.right;

            // 터치패드 클릭은 듀얼쇽/듀얼센스에만 있음. 다른 패드면 null(안 눌림).
            case GAMEPAD_BUTTON.TouchpadClick:
                DualShockGamepad ds = pad as DualShockGamepad;
                return ds != null ? ds.touchpadButton : null;

            // PS 버튼(홈). 패드에 따라 컨트롤 이름이 없을 수 있어 이름으로 조회하고, 없으면 null.
            // ⚠ OS(스팀/윈도우 게임바)가 이 버튼을 가로채는 경우가 많아 게임까지 안 오는 일이 흔함 —
            //    중요한 기능을 여기 배정하지 말 것.
            case GAMEPAD_BUTTON.PsButton:
                // 인덱서(pad["..."])는 없는 이름이면 예외를 던지므로 Try 계열로 조회함.
                return pad.TryGetChildControl<ButtonControl>("systemButton");

            default: return null;
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
