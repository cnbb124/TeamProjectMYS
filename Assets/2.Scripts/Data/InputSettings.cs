using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// InputSettings — 입력 관련 사용자 설정 묶음.
//
// [저장 방식]
//   JsonUtility로 직렬화해 PlayerPrefs 한 칸에 넣는다.
//   진행 데이터(SaveData)는 파일, 환경 설정(사운드 볼륨 등)은 PlayerPrefs —
//   입력 설정은 환경 설정이므로 PlayerPrefs 쪽을 따른다.
//
//   항목마다 PlayerPrefs 키를 따로 두지 않는 이유:
//   키 배정만 40여 개라 항목이 늘 때마다 키/저장/복원 코드가 같이 늘어난다.
//   묶음 하나로 두면 필드만 추가하면 되고 저장 코드는 그대로다.
//
// [항목 추가/삭제 시 주의]
//   JsonUtility는 저장된 JSON에 없는 필드를 기본값으로 남긴다(크래시 없음).
//   다만 필드 이름을 바꾸면 그 설정만 조용히 초기화된다 — SaveData와 같은 제약.
//
// ⚠ JsonUtility는 Dictionary를 직렬화하지 못함. 목록은 반드시 List로 둘 것.
// =====================================================================
[System.Serializable]
public class InputSettings
{
    // ── 조종 감도 (1~100) ──
    public int mouseLookSensitivity = InputManager.DefaultSensitivity;
    public int padLookSensitivity   = InputManager.DefaultSensitivity;
    public float padLookCurve = 2f;

    // ── 상하 반전 ──
    public bool mouseInvertLookY;
    public bool padInvertLookY;
    public bool mobileInvertLookY;

    // ── 부스트 토글 (누르는 동안 / 켜고 끄기) ──
    public bool mouseBoostToggle;
    public bool padBoostToggle;
    public bool mobileBoostToggle;

    // ── 키 배정 ──
    // 순서가 아니라 '어떤 행동인가'(INPUT_ACTION)로 짝지어 저장한다.
    // 순서로 저장하면 항목을 중간에 하나 끼워넣는 순간 그 아래가 전부 밀려서 키가 뒤섞임.
    // 행동 값으로 찾으면 항목이 늘든 순서가 바뀌든 영향이 없고, 없는 항목은 기본값으로 남는다.
    public List<SavedBind> keyboardBindings = new List<SavedBind>();
    public List<SavedBind> gamepadBindings = new List<SavedBind>();

    // ── 패드 조합키 ──
    public List<SavedCombo> combos = new List<SavedCombo>();
}

// 키 배정 한 줄. "이 행동은 이 키/버튼" 짝.
// code에는 키보드면 KeyCode, 패드면 GAMEPAD_BUTTON을 int로 눕혀 담는다.
[System.Serializable]
public class SavedBind
{
    public int action;   // INPUT_ACTION
    public int code;     // KeyCode 또는 GAMEPAD_BUTTON
}

// 조합키 한 줄. ComboBinding에서 저장이 필요한 값만 옮겨 담는다
// (wasActive 같은 실행 중 상태는 저장 대상이 아님).
[System.Serializable]
public class SavedCombo
{
    public string name;
    public List<int> buttons = new List<int>();
    public int action;
    public bool holdAction;
    public bool suppressSingleKeys;
    public bool useStick;
    public int stick;
    public int stickDirection;
    public float stickThreshold;
}
