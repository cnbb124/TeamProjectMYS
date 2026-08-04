using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [임시 테스트용] F7키로 색약 보조 표시를 켜고 끈다.
///
/// F8은 CockpitViewSwitcher의 VR 좌석 재정렬 키로 이미 쓰이고 있어 피했다.
///
/// 정식 설정 UI가 만들어지기 전까지, 게임을 돌리면서 켠 상태와 끈 상태를
/// 바로 비교하기 위한 도구다. 씬에 아무 오브젝트에나 붙이거나,
/// 아래 자동 생성이 있으므로 그냥 두어도 동작한다.
///
/// 설정 UI가 생기면 이 파일은 지워도 된다.
/// </summary>
public sealed class ColorBlindQuickToggle : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        GameObject toggleObject = new GameObject(nameof(ColorBlindQuickToggle));
        toggleObject.AddComponent<ColorBlindQuickToggle>();
        DontDestroyOnLoad(toggleObject);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.f7Key.wasPressedThisFrame)
        {
            return;
        }

        ColorBlindSettings.Enabled = !ColorBlindSettings.Enabled;
        Debug.Log(
            $"[색약모드] {(ColorBlindSettings.Enabled ? "켜짐" : "꺼짐")} " +
            "— F7로 전환");
    }
}
