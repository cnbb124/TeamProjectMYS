/*
 * [SensitivitySettingsUI]
 * 시스템 세팅 - 조작 탭. 마우스/패드 조종 감도 슬라이더를 InputManager와 연동.
 *
 * ★감도 시스템은 팀장(자연님)이 InputManager에 구현해둠. 이 UI는 읽고/쓰기만 함.
 *   - 값 범위는 1~100 정수 (0.1~0.2 소수 아님)
 *   - PlayerPrefs 저장은 InputManager.SetMouseLookSensitivity 내부에서 처리 → UI가 따로 저장 안 함
 *
 * [연동 API] (InputManager, 읽기/쓰기만)
 *   MouseLookSensitivity           (get)  : 현재 마우스 감도 1~100 — 슬라이더 초기값
 *   SetMouseLookSensitivity(int)   (set)  : 적용 + PlayerPrefs 저장까지
 *   PadLookSensitivity / SetPadLookSensitivity(int) : 패드용 (선택)
 *   MinSensitivity / MaxSensitivity : 1 / 100 (슬라이더 범위 참고용)
 *
 * [부착] SettingMenu의 조작(감도) 패널에 부착.
 *
 * [인스펙터 연결]
 * - Mouse Slider : Min 1 / Max 100 / Whole Numbers 체크
 * - (선택) Pad Slider : 패드 감도. 안 쓰면 비워둠
 * - (선택) 값 텍스트 : 연결하면 "50" 형식 표시
 * - (선택) InvertY Toggle : 마우스 상하 반전
 * - (선택) ControllerFlip Toggle : 패드 상하 반전 (gamepadConfig.invertRStickY 연동)
 * - 슬라이더/토글 OnValueChanged는 인스펙터에서 비워둘 것 — Start에서 자동 연결
 *
 * [상하 반전 저장 방식 차이]
 * - 마우스 : InputManager에 저장 API가 없던 시절의 임시(A안) → UI가 PlayerPrefs로 직접 저장
 * - 패드   : InputManager 정식 저장 경로 사용(gamepadConfig.invertRStickY가 BuildSettings에 포함)
 *           → OnControllerFlipChanged에서 값 적용 후 WriteSettings()만 호출하면 됨
 */

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SensitivitySettingsUI : MonoBehaviour
{
    [Header("Sliders (Min 1 / Max 100 / Whole Numbers 체크)")]
    [SerializeField] private Slider mouseSlider;
    [Tooltip("(선택) 게임패드 오른쪽 스틱 감도. 안 쓰면 비워둠")]
    [SerializeField] private Slider padSlider;

    [Header("값 텍스트 (선택)")]
    [SerializeField] private TMP_Text mouseText;
    [SerializeField] private TMP_Text padText;

    [Header("마우스 상하 반전 토글")]
    [Tooltip("켜면 마우스를 위로 밀 때 기수가 아래로 감(항공 스타일)")]
    [SerializeField] private Toggle invertYToggle;

    [Header("패드 상하 반전 토글 (ControllerFlip)")]
    [Tooltip("패드 전용. 켜면 오른쪽 스틱을 위로 밀 때 기수가 아래로 감(항공 스타일).\n" +
             "InputManager.gamepadConfig.invertRStickY 와 연동")]
    [SerializeField] private Toggle controllerFlipToggle;

    // ★임시(A안): InputManager에 상하반전 저장 API가 아직 없어서 UI가 직접 PlayerPrefs로 저장.
    //   나중에 팀장님이 SetInvertLookY(bool)/InvertLookY 같은 걸 만들면
    //   아래 GetInvertY()/SetInvertY() 두 곳만 그 API로 바꾸면 됨(다른 코드는 그대로).
    private const string KEY_INVERT_Y = "Ctrl_InvertLookY";

    private void Start()
    {
        // 슬라이더 범위를 InputManager 상수에 맞춰 강제 (인스펙터 실수 방지)
        SetupSlider(mouseSlider);
        SetupSlider(padSlider);

        // 리스너 자동 연결 (OnValueChanged는 인스펙터에서 비워둘 것)
        if (mouseSlider != null) mouseSlider.onValueChanged.AddListener(OnMouseChanged);
        if (padSlider   != null) padSlider.onValueChanged.AddListener(OnPadChanged);
        if (invertYToggle != null) invertYToggle.onValueChanged.AddListener(OnInvertYChanged);
        if (controllerFlipToggle != null) controllerFlipToggle.onValueChanged.AddListener(OnControllerFlipChanged);
    }

    private void OnEnable()
    {
        // 열 때마다 InputManager의 현재값으로 슬라이더 복원
        RefreshFromInputManager();
    }

    private void SetupSlider(Slider s)
    {
        if (s == null) return;
        s.minValue = InputManager.MinSensitivity;   // 1
        s.maxValue = InputManager.MaxSensitivity;   // 100
        s.wholeNumbers = true;                       // 정수 스냅
    }

    /// <summary>InputManager 현재 감도를 슬라이더/텍스트에 반영.</summary>
    public void RefreshFromInputManager()
    {
        if (InputManager.Instance == null) return;

        int mouse = InputManager.Instance.MouseLookSensitivity;
        int pad   = InputManager.Instance.PadLookSensitivity;

        // SetValueWithoutNotify: 리스너 중복 발동 없이 슬라이더 핸들 위치만 복원
        if (mouseSlider != null) mouseSlider.SetValueWithoutNotify(mouse);
        if (padSlider   != null) padSlider.SetValueWithoutNotify(pad);

        if (mouseText != null) mouseText.text = mouse.ToString();
        if (padText   != null) padText.text   = pad.ToString();

        // 마우스 상하 반전: 저장값을 InputManager에 적용 + 토글 위치 복원
        bool invertY = GetInvertY();
        ApplyInvertY(invertY);
        if (invertYToggle != null) invertYToggle.SetIsOnWithoutNotify(invertY);

        // 패드 상하 반전(ControllerFlip): InputManager가 이미 LoadSettings로 복원해둔 값을 읽어 토글에 반영
        if (controllerFlipToggle != null)
            controllerFlipToggle.SetIsOnWithoutNotify(InputManager.Instance.gamepadConfig.invertRStickY);
    }

    // ── 슬라이더 콜백: InputManager에 위임(적용+저장은 그쪽이 함) ──

    private void OnMouseChanged(float v)
    {
        int value = Mathf.RoundToInt(v);
        if (InputManager.Instance != null)
            InputManager.Instance.SetMouseLookSensitivity(value);

        if (mouseText != null) mouseText.text = value.ToString();
    }

    private void OnPadChanged(float v)
    {
        int value = Mathf.RoundToInt(v);
        if (InputManager.Instance != null)
            InputManager.Instance.SetPadLookSensitivity(value);

        if (padText != null) padText.text = value.ToString();
    }

    private void OnInvertYChanged(bool isOn)
    {
        ApplyInvertY(isOn);
        SetInvertY(isOn);   // 저장
    }

    // ── 패드 상하 반전(ControllerFlip) ──
    //   마우스와 달리 InputManager 정식 저장 경로가 이미 있음(gamepadConfig.invertRStickY가
    //   BuildSettings/ApplySettings에 포함). 그래서 별도 PlayerPrefs 키 없이 WriteSettings로 저장.
    private void OnControllerFlipChanged(bool isOn)
    {
        if (InputManager.Instance == null) return;

        InputManager.Instance.gamepadConfig.invertRStickY = isOn;  // 즉시 적용
        InputManager.Instance.WriteSettings();                     // 저장(디스크 확정은 세팅창 닫을 때 CommitSettings가 함)
    }

    // ── 상하 반전: 적용/읽기/저장을 여기 3곳에만 모아둠 ──
    //   팀장님이 InputManager에 저장 API를 만들면 이 3개 메서드만 그 API로 교체하면 됨.

    // InputManager에 반영 (현재는 config의 public bool을 직접 씀)
    private void ApplyInvertY(bool value)
    {
        if (InputManager.Instance != null)
            InputManager.Instance.keyboardMouseConfig.invertLookY = value;
    }

    // 저장값 읽기 (현재는 PlayerPrefs)
    private bool GetInvertY()
    {
        return PlayerPrefs.GetInt(KEY_INVERT_Y, 0) == 1;
    }

    // 저장 (현재는 PlayerPrefs)
    private void SetInvertY(bool value)
    {
        PlayerPrefs.SetInt(KEY_INVERT_Y, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
