using UnityEditor;
using UnityEngine;

// =====================================================================
// ProjectHubWindow — 프로젝트 총괄 작업창.
//
// 흩어져 있는 작업(SO 데이터 편집 / 프리팹 배선 검사 / 씬 관리 / 맵 오브젝트 배치)을
// 한 창에서 처리하려고 만든 것. 탭마다 파일이 나뉘어 있음(partial class).
//
//   Data   — ProjectHubWindow.DataTab.cs    ScriptableObject 목록/검색/생성/편집
//   Prefab — ProjectHubWindow.PrefabTab.cs  프리팹 필수 배선 검사(레이어/PhotonView/VFX 등)
//   Scene  — ProjectHubWindow.SceneTab.cs   SCENE_TYPE ↔ 실제 씬 파일 ↔ 빌드 세팅 대조
//   Map    — ProjectHubWindow.MapTab.cs     프리팹 팔레트에서 씬에 배치(그리드/랜덤 산포)
//
// 열기: 상단 메뉴 Tools > TeamProject Hub
// =====================================================================
public partial class ProjectHubWindow : EditorWindow
{
	private enum HubTab
	{
		Data = 0,
		Registry = 1,
		Prefab = 2,
		Scene = 3,
		Map = 4,
	}

	private static readonly string[] TabLabels = { "데이터", "SFX,VFX,오브젝트등록", "프리팹 검사", "씬 관리", "맵 배치" };

	private HubTab _tab = HubTab.Data;

	// 접기 상태. 플로팅 창이면 높이까지 줄이고, 도킹 상태면 내용만 숨김
	// (도킹된 창의 크기는 Unity가 관리해서 스크립트로 못 바꿈)
	private bool _collapsed;
	private Rect _expandedPosition;

	// 분할선 드래그 중인지. 한 번에 하나만 잡히면 되므로 창 단위 플래그 하나로 충분
	private bool _splitterDragging;

	[MenuItem("Tools/TeamProject Hub %#h")]
	private static void Open()
	{
		ProjectHubWindow window = GetWindow<ProjectHubWindow>();
		window.titleContent = new GUIContent("TeamProject Hub");
		window.minSize = new Vector2(720f, 420f);
		window.Show();
	}

	private void OnEnable()
	{
		// 탭별 초기화. 창을 닫았다 열어도 목록이 최신이어야 하므로 여기서 새로 읽음
		DataTabOnEnable();
		PrefabTabOnEnable();
		SceneTabOnEnable();
		MapTabOnEnable();
		SpherePlaceOnEnable();
	}

	private void OnDisable()
	{
		// 임베드한 인스펙터(Editor 인스턴스)를 안 버리면 누수됨
		DataTabOnDisable();
		// 씬 뷰 콜백을 안 떼면 창을 닫아도 계속 그려짐
		SpherePlaceOnDisable();
		SceneTabOnDisable();
	}

	// 빌드 세팅 창 등에서 고치고 돌아왔을 때 표가 옛날 값인 걸 막음.
	private void OnFocus()
	{
		SceneTabOnFocus();
	}

	private void OnGUI()
	{
		DrawTabBar();
		if (_collapsed)
		{
			return;
		}
		EditorGUILayout.Space(4f);

		switch (_tab)
		{
			case HubTab.Data:
				DrawDataTab();
				break;
			case HubTab.Registry:
				DrawRegistryTab();
				break;
			case HubTab.Prefab:
				DrawPrefabTab();
				break;
			case HubTab.Scene:
				DrawSceneTab();
				break;
			case HubTab.Map:
				DrawMapTab();
				break;
		}
	}

	private void DrawTabBar()
	{
		EditorGUILayout.BeginHorizontal();

		int selected = GUILayout.Toolbar((int)_tab, TabLabels, GUILayout.Height(24f));
		if (selected != (int)_tab)
		{
			_tab = (HubTab)selected;
			GUI.FocusControl(null);
		}

		if (GUILayout.Button(_collapsed ? "▼" : "▲", GUILayout.Width(28f), GUILayout.Height(24f)))
		{
			ToggleCollapsed();
		}

		EditorGUILayout.EndHorizontal();
	}

	// 접기/펴기. 플로팅 창이면 높이까지 줄여 탭바만 남김.
	// 도킹된 창은 position 변경이 무시되므로 내용 숨김만 적용됨.
	private void ToggleCollapsed()
	{
		_collapsed = !_collapsed;
		if (_collapsed)
		{
			_expandedPosition = position;
			Rect shrunk = position;
			shrunk.height = 46f;
			position = shrunk;
		}
		else if (_expandedPosition.height > 46f)
		{
			position = _expandedPosition;
		}
		GUI.FocusControl(null);
	}

	// 드래그로 폭을 조절하는 세로 분할선. 갱신된 폭을 반환함
	private float DrawVerticalSplitter(float width, float min, float max)
	{
		Rect rect = GUILayoutUtility.GetRect(5f, 5f, GUILayout.Width(5f), GUILayout.ExpandHeight(true));
		EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f, 1f));
		EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

		Event e = Event.current;
		if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
		{
			_splitterDragging = true;
			e.Use();
		}
		if (_splitterDragging)
		{
			if (e.type == EventType.MouseDrag)
			{
				width += e.delta.x;
				Repaint();
			}
			else if (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp)
			{
				_splitterDragging = false;
			}
		}

		return Mathf.Clamp(width, min, max);
	}

	// =================================================================
	// 탭 공용 유틸
	// =================================================================

	// 제목 + 밑줄. 탭마다 섹션을 같은 모양으로 뽑기 위한 것
	private static void SectionHeader(string title)
	{
		EditorGUILayout.Space(2f);
		EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
		Rect line = EditorGUILayout.GetControlRect(false, 1f);
		EditorGUI.DrawRect(line, new Color(0.35f, 0.35f, 0.35f, 1f));
		EditorGUILayout.Space(2f);
	}

	// 결과 줄 하나. 통과/경고/실패를 색으로 구분해서 훑기 쉽게 함
	private enum CheckLevel
	{
		Pass,
		Warn,
		Fail,
	}

	private static void ResultLine(CheckLevel level, string message, Object context = null)
	{
		Color prev = GUI.color;
		string prefix;
		switch (level)
		{
			case CheckLevel.Pass:
				GUI.color = new Color(0.6f, 1f, 0.6f);
				prefix = "OK   ";
				break;
			case CheckLevel.Warn:
				GUI.color = new Color(1f, 0.9f, 0.5f);
				prefix = "주의 ";
				break;
			default:
				GUI.color = new Color(1f, 0.6f, 0.6f);
				prefix = "실패 ";
				break;
		}

		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField(prefix + message);
		GUI.color = prev;
		if (context != null)
		{
			if (GUILayout.Button("선택", GUILayout.Width(48f)))
			{
				Selection.activeObject = context;
				EditorGUIUtility.PingObject(context);
			}
		}
		EditorGUILayout.EndHorizontal();
		GUI.color = prev;
	}

	// 남의 창에서는 Header의 <size=> 태그가 문자로 나오므로 그리는 동안만 richText를 켠다.
	// EditorStyles는 공용이라 Dispose에서 되돌린다.
	private struct RichTextScope : System.IDisposable
	{
		private readonly bool _prevBold;
		private readonly bool _prevLabel;

		public RichTextScope(bool enable)
		{
			_prevBold = EditorStyles.boldLabel.richText;
			_prevLabel = EditorStyles.label.richText;
			EditorStyles.boldLabel.richText = enable;
			EditorStyles.label.richText = enable;
		}

		public void Dispose()
		{
			EditorStyles.boldLabel.richText = _prevBold;
			EditorStyles.label.richText = _prevLabel;
		}
	}

	// 검색 문자열이 비어 있거나 대상 이름에 포함돼 있으면 통과. 대소문자 무시
	private static bool MatchesFilter(string name, string filter)
	{
		if (string.IsNullOrWhiteSpace(filter))
		{
			return true;
		}
		return name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
	}
}
