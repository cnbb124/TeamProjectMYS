using UnityEditor;
using UnityEngine;

// 허브 창 공용 스타일·색. 매 OnGUI마다 new GUIStyle을 만들면 GC가 계속 도므로 한 번만 만들어 재사용함.
public static class HubStyles
{
    // 상태 색. 에디터 다크/라이트 어느 쪽에서도 읽히게 채도를 낮춰 잡음.
    public static readonly Color Ok = new Color(0.44f, 0.80f, 0.50f);
    public static readonly Color Warn = new Color(0.95f, 0.75f, 0.30f);
    public static readonly Color Error = new Color(0.90f, 0.45f, 0.45f);
    public static readonly Color Muted = new Color(0.60f, 0.60f, 0.60f);

    private static GUIStyle _cardStyle;
    private static GUIStyle _cardTitle;
    private static GUIStyle _cardSub;
    private static GUIStyle _sectionTitle;
    private static GUIStyle _badge;
    private static GUIStyle _arrow;
    private static GUIStyle _issueText;
    private static Texture2D _cardBg;
    private static Texture2D _cardBgSelected;

    public static GUIStyle Card => _cardStyle ??= new GUIStyle(GUI.skin.box)
    {
        padding = new RectOffset(10, 10, 8, 8),
        margin = new RectOffset(2, 2, 2, 2),
        normal = { background = CardBg },
        alignment = TextAnchor.UpperLeft,
    };

    public static GUIStyle CardSelected => _cardBgSelected == null || _cardStyle == null
        ? BuildSelected()
        : _selectedStyle;

    private static GUIStyle _selectedStyle;

    private static GUIStyle BuildSelected()
    {
        _selectedStyle = new GUIStyle(Card)
        {
            normal = { background = CardBgSelected },
        };
        return _selectedStyle;
    }

    public static GUIStyle CardTitle => _cardTitle ??= new GUIStyle(EditorStyles.boldLabel)
    {
        fontSize = 12,
        alignment = TextAnchor.MiddleCenter,
        wordWrap = false,
    };

    public static GUIStyle CardSub => _cardSub ??= new GUIStyle(EditorStyles.miniLabel)
    {
        alignment = TextAnchor.MiddleCenter,
        wordWrap = false,
    };

    public static GUIStyle SectionTitle => _sectionTitle ??= new GUIStyle(EditorStyles.boldLabel)
    {
        fontSize = 13,
        margin = new RectOffset(0, 0, 8, 4),
    };

    public static GUIStyle Badge => _badge ??= new GUIStyle(EditorStyles.miniLabel)
    {
        alignment = TextAnchor.MiddleCenter,
        fontStyle = FontStyle.Bold,
    };

    public static GUIStyle Arrow => _arrow ??= new GUIStyle(EditorStyles.label)
    {
        alignment = TextAnchor.MiddleCenter,
        fontSize = 16,
        normal = { textColor = Muted },
    };

    public static GUIStyle IssueText => _issueText ??= new GUIStyle(EditorStyles.label)
    {
        wordWrap = true,
        alignment = TextAnchor.MiddleLeft,
    };

    private static Texture2D CardBg => _cardBg != null
        ? _cardBg
        : _cardBg = SolidTexture(EditorGUIUtility.isProSkin
            ? new Color(0.24f, 0.24f, 0.26f)
            : new Color(0.86f, 0.86f, 0.88f));

    private static Texture2D CardBgSelected => _cardBgSelected != null
        ? _cardBgSelected
        : _cardBgSelected = SolidTexture(EditorGUIUtility.isProSkin
            ? new Color(0.20f, 0.32f, 0.44f)
            : new Color(0.72f, 0.82f, 0.94f));

    private static Texture2D SolidTexture(Color color)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }

    /// <summary>가로 구분선.</summary>
    public static void Separator(float space = 6f)
    {
        GUILayout.Space(space);
        Rect rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, EditorGUIUtility.isProSkin ? 0.35f : 0.15f));
        GUILayout.Space(space);
    }

    /// <summary>색 점 하나. 카드 상태 표시용.</summary>
    public static void StatusDot(Rect rect, Color color)
    {
        EditorGUI.DrawRect(rect, color);
    }

    /// <summary>지정 색으로 라벨 하나 그리기.</summary>
    public static void ColoredLabel(string text, Color color, GUIStyle style, params GUILayoutOption[] options)
    {
        Color prev = GUI.color;
        GUI.color = color;
        EditorGUILayout.LabelField(text, style, options);
        GUI.color = prev;
    }
}
