using UnityEngine;

/// <summary>
/// 색약 보조 표시 설정. 켜면 색에만 의존하던 UI가
/// 모양·두께·굵기로도 상태를 구분하도록 바뀐다.
///
/// 왜 필요한가:
///   기존 락온 표시는 후보=초록, 확정=빨강이었다.
///   적록색약에서는 이 두 색이 거의 같은 색으로 보여서 상태를 구분할 수 없다.
///   색을 바꾸는 것만으로는 부족하므로 모양과 두께도 함께 바꾼다.
///
/// 기본값은 꺼짐이라 켜지 않으면 기존 화면과 완전히 동일하다.
/// </summary>
public static class ColorBlindSettings
{
    private const string EnabledKey = "Accessibility.ColorBlindMode";

    private static bool _loaded;
    private static bool _enabled;

    /// <summary>설정이 바뀔 때 발생. UI가 즉시 다시 그리도록 구독한다.</summary>
    public static event System.Action Changed;

    public static bool Enabled
    {
        get
        {
            if (!_loaded)
            {
                _loaded = true;
                _enabled = PlayerPrefs.GetInt(EnabledKey, 0) == 1;
            }

            return _enabled;
        }
        set
        {
            if (Enabled == value)
            {
                return;
            }

            _enabled = value;
            _loaded = true;
            PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>옵션 UI의 토글에 연결해서 쓴다.</summary>
    public static void SetEnabled(bool value)
    {
        Enabled = value;
    }

    public static Color Pick(Color normal, Color accessible)
    {
        return Enabled ? accessible : normal;
    }
}
