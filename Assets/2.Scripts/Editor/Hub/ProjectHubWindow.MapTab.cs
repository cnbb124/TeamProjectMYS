using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================
// ProjectHubWindow — 맵 배치 탭.
//
// 왼쪽에서 놓을 프리팹을 고르고, 오른쪽 구 표면을 클릭해서 놓음.
// 우주 맵은 바닥이 없어 좌표를 손으로 넣으면 깊이가 제멋대로가 되므로,
// 배치 거리를 구 반경으로 고정해 두고 방향만 고르게 함.
//
// 배치는 전부 Undo에 등록됨(Ctrl+Z로 되돌아감).
// 부모 컨테이너를 지정하면 그 아래로 모아서 나중에 통째로 지우기 쉬움.
//
// ※ 프리팹 인스턴스로 놓기 때문에 원본 프리팹을 고치면 배치된 것들도 같이 바뀜.
// =====================================================================
public partial class ProjectHubWindow
{
	// 팔레트로 훑을 폴더. 맵 오브젝트가 다른 곳에도 있으면 여기 추가할 것
	private static readonly string[] MapPaletteRoots =
	{
		"Assets/3.Prefabs/Map",
		"Assets/3.Prefabs/Units/Enemies",
	};

	// 팔레트 칸 하나의 폭(픽셀). 아래 목록 높이는 분할선을 끌어 조절함
	private const float PaletteCellWidth = 110f;
	private float _paletteHeight = 260f;

	private readonly List<GameObject> _palette = new List<GameObject>();
	private GameObject _paletteSelected;
	private string _paletteFilter = "";
	private Vector2 _paletteScroll;
	private Vector2 _mapOptionScroll;

	private string _containerName = "MapObjects";

	// 놓을 때 같이 먹이는 랜덤화
	private bool _randomYaw = true;
	private bool _randomFullRotation;
	private Vector2 _randomScaleRange = new Vector2(1f, 1f);

	private bool _mapRuleOpen;
	private bool _mapSceneViewOpen;
	// 씬이 바뀌면 그 씬의 경계 반경을 다시 읽으려고 마지막 씬 이름을 들고 있음
	private string _mapLastSceneName;

	private void MapTabOnEnable()
	{
		RefreshPalette();
	}

	private void DrawMapTab()
	{
		DrawMapHeader();
		DrawPlaceOptions();
		// 분할선을 위아래로 끌면 아래 목록이 커지고 작아짐.
		// 창이 작을 때 최대치가 최소치보다 작아지면 값이 튀므로 아래쪽을 보장해 둠
		float maxPaletteHeight = Mathf.Max(160f, position.height - 200f);
		_paletteHeight = DrawHorizontalSplitter(_paletteHeight, 90f, maxPaletteHeight);
		DrawPalette();
	}

	// 어느 씬을 고치고 있는지 항상 보이게. 씬을 착각하고 배치하는 사고를 막는 게 목적임
	private void DrawMapHeader()
	{
		UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

		// 씬이 바뀌었으면 그 씬의 경계 반경으로 다시 맞춤 — 창을 열어둔 채 씬만 갈아타는 경우가 많음
		if (_mapLastSceneName != scene.name)
		{
			_mapLastSceneName = scene.name;
			PullBoundaryRadius();
		}

		EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
		EditorGUILayout.LabelField("작업 중인 씬", EditorStyles.miniBoldLabel, GUILayout.Width(76f));
		HubStyles.ColoredLabel(string.IsNullOrEmpty(scene.name) ? "(저장 안 된 씬)" : scene.name,
			HubStyles.Ok, EditorStyles.miniBoldLabel, GUILayout.Width(160f));

		GUILayout.FlexibleSpace();

		GameObject container = string.IsNullOrWhiteSpace(_containerName) ? null : GameObject.Find(_containerName);
		int placedCount = container != null ? container.transform.childCount : 0;
		EditorGUILayout.LabelField($"놓인 것 {placedCount}개", EditorStyles.miniLabel, GUILayout.Width(90f));

		if (GUILayout.Button("경계 다시 읽기", EditorStyles.toolbarButton, GUILayout.Width(90f)))
		{
			PullBoundaryRadius();
		}
		EditorGUILayout.EndHorizontal();
	}

	private void DrawPalette()
	{
		EditorGUILayout.BeginVertical();

		EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
		EditorGUILayout.LabelField("놓을 것 고르기", EditorStyles.miniBoldLabel, GUILayout.Width(90f));
		_paletteFilter = EditorGUILayout.TextField(_paletteFilter, EditorStyles.toolbarSearchField);
		if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(64f)))
		{
			RefreshPalette();
		}
		EditorGUILayout.EndHorizontal();

		_paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll, GUILayout.Height(_paletteHeight));

		// 창 폭에 맞춰 한 줄에 몇 칸 들어가는지 계산해서 접어 넣음
		float available = Mathf.Max(position.width - 30f, PaletteCellWidth);
		int columns = Mathf.Max(1, Mathf.FloorToInt(available / PaletteCellWidth));
		int drawn = 0;
		bool rowOpen = false;

		for (int i = 0; i < _palette.Count; i++)
		{
			GameObject prefab = _palette[i];
			if (prefab == null || !MatchesFilter(prefab.name, _paletteFilter))
			{
				continue;
			}

			if (drawn % columns == 0)
			{
				EditorGUILayout.BeginHorizontal();
				rowOpen = true;
			}

			DrawPaletteCell(prefab);
			drawn++;

			if (drawn % columns == 0)
			{
				EditorGUILayout.EndHorizontal();
				rowOpen = false;
			}
		}
		if (rowOpen)
		{
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}

		EditorGUILayout.EndScrollView();

		// 미리보기 이미지는 백그라운드에서 만들어져서, 다 될 때까지 계속 다시 그려야 채워짐
		if (AssetPreview.IsLoadingAssetPreviews())
		{
			Repaint();
		}

		if (_palette.Count == 0)
		{
			EditorGUILayout.HelpBox("놓을 수 있는 프리팹이 없음.\n탐색 경로: " + string.Join(", ", MapPaletteRoots), MessageType.Info);
		}

		EditorGUILayout.EndVertical();
	}

	// 칸 하나 = 프리팹 하나. 생긴 것(미리보기) + 구 위에서 쓰이는 색 + 이름.
	// 색은 이름에서 뽑으므로 여기 표시와 구 위 점 색이 항상 같음
	private void DrawPaletteCell(GameObject prefab)
	{
		bool isSelected = prefab == _paletteSelected;
		Color markerColor = PaletteColor(prefab.name);

		EditorGUILayout.BeginVertical(isSelected ? HubStyles.CardSelected : HubStyles.Card,
			GUILayout.Width(PaletteCellWidth - 8f));

		// 미리보기 — 아직 안 만들어졌으면 빈 칸으로 두고 다음 그리기에서 채워짐
		Rect thumb = GUILayoutUtility.GetRect(PaletteCellWidth - 20f, 64f);
		Texture2D preview = AssetPreview.GetAssetPreview(prefab);
		if (preview != null)
		{
			GUI.DrawTexture(thumb, preview, ScaleMode.ScaleToFit);
		}
		else
		{
			EditorGUI.LabelField(thumb, "…", EditorStyles.centeredGreyMiniLabel);
		}

		// 구 위에서 이 프리팹이 무슨 색으로 찍히는지
		EditorGUILayout.BeginHorizontal();
		Rect swatch = GUILayoutUtility.GetRect(12f, 12f, GUILayout.Width(12f), GUILayout.Height(12f));
		EditorGUI.DrawRect(swatch, markerColor);
		EditorGUILayout.LabelField(prefab.name, EditorStyles.miniLabel);
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button(isSelected ? "고름" : "고르기", EditorStyles.miniButton))
		{
			_paletteSelected = prefab;
			GUIUtility.keyboardControl = 0;
			EditorGUIUtility.editingTextField = false;
		}
		// 이게 뭔지 확인용 — 프로젝트 창에서 실물을 짚어줌
		if (GUILayout.Button("보기", EditorStyles.miniButton, GUILayout.Width(38f)))
		{
			EditorGUIUtility.PingObject(prefab);
			Selection.activeObject = prefab;
		}
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.EndVertical();
	}

	private void DrawPlaceOptions()
	{
		EditorGUILayout.BeginVertical();
		// 내용이 길어지면 아래가 잘려서 손을 못 대므로 오른쪽 전체를 스크롤에 담음
		_mapOptionScroll = EditorGUILayout.BeginScrollView(_mapOptionScroll);

		// ---- 무엇을 놓는가 ----
		EditorGUILayout.BeginVertical(HubStyles.Card);
		if (_paletteSelected == null)
		{
			HubStyles.ColoredLabel("왼쪽에서 놓을 것을 고르십시오.", HubStyles.Warn, HubStyles.IssueText);
		}
		else
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(_paletteSelected.name, HubStyles.SectionTitle);
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("선택 해제", GUILayout.Width(70f)))
			{
				_paletteSelected = null;
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(_paletteSelected), EditorStyles.miniLabel);
		}
		EditorGUILayout.EndVertical();

		// ---- 어디에 놓는가 ----
		DrawGlobeSection();

		// ---- 놓는 규칙 ----
		HubStyles.Separator(4f);
		_mapRuleOpen = EditorGUILayout.Foldout(_mapRuleOpen, "놓는 규칙 — 부모 · 회전 · 크기", true);
		if (_mapRuleOpen)
		{
			EditorGUI.indentLevel++;
			_containerName = EditorGUILayout.TextField(new GUIContent("부모 오브젝트 이름",
				"놓은 것들을 이 이름의 오브젝트 아래로 모음. 없으면 새로 만듦.\n" +
				"비워두면 씬 맨 위에 흩어져 놓임"), _containerName);

			_randomFullRotation = EditorGUILayout.Toggle(new GUIContent("랜덤 회전 (아무 방향)",
				"놓을 때마다 X·Y·Z 전부 무작위로 돌림. 소행성처럼 방향이 의미 없는 것에 씀"),
				_randomFullRotation);
			if (!_randomFullRotation)
			{
				_randomYaw = EditorGUILayout.Toggle(new GUIContent("랜덤 회전 (Y축만)",
					"위아래는 그대로 두고 좌우로만 무작위로 돌림.\n" +
					"세워둬야 하는 구조물이 뒤집히지 않게 할 때 씀"), _randomYaw);
			}

			_randomScaleRange = EditorGUILayout.Vector2Field(new GUIContent("크기 배율 (최소/최대)",
				"놓을 때마다 이 사이의 배율을 무작위로 골라 곱함.\n" +
				"둘 다 1이면 프리팹 원래 크기 그대로"), _randomScaleRange);
			if (_randomScaleRange.x <= 0f)
			{
				_randomScaleRange.x = 0.01f;
			}
			if (_randomScaleRange.y < _randomScaleRange.x)
			{
				_randomScaleRange.y = _randomScaleRange.x;
			}
			EditorGUI.indentLevel--;
		}

		// ---- 씬 뷰에서 놓기 ----
		HubStyles.Separator(4f);
		_mapSceneViewOpen = EditorGUILayout.Foldout(_mapSceneViewOpen, "씬 뷰에서 직접 놓기", true);
		if (_mapSceneViewOpen)
		{
			EditorGUI.indentLevel++;
			EditorGUILayout.LabelField("실제 크기를 보면서 놓고 싶을 때 씀. 위 구와 같은 규칙으로 놓임",
				EditorStyles.wordWrappedMiniLabel);
			DrawSpherePlaceSection();
			EditorGUI.indentLevel--;
		}

		EditorGUILayout.EndScrollView();
		EditorGUILayout.EndVertical();
	}

	// 컨테이너 이름이 비어 있으면 씬 루트에 놓음. 있으면 찾고, 없으면 만듦.
	private Transform ResolveContainer()
	{
		if (string.IsNullOrWhiteSpace(_containerName))
		{
			return null;
		}

		GameObject existing = GameObject.Find(_containerName);
		if (existing != null)
		{
			return existing.transform;
		}

		GameObject created = new GameObject(_containerName);
		Undo.RegisterCreatedObjectUndo(created, "Hub 컨테이너 생성");
		return created.transform;
	}

	private void RefreshPalette()
	{
		_palette.Clear();

		List<string> roots = new List<string>();
		for (int i = 0; i < MapPaletteRoots.Length; i++)
		{
			if (AssetDatabase.IsValidFolder(MapPaletteRoots[i]))
			{
				roots.Add(MapPaletteRoots[i]);
			}
		}
		if (roots.Count == 0)
		{
			return;
		}

		string[] guids = AssetDatabase.FindAssets("t:GameObject", roots.ToArray());
		for (int i = 0; i < guids.Length; i++)
		{
			string path = AssetDatabase.GUIDToAssetPath(guids[i]);
			if (!path.EndsWith(".prefab"))
			{
				continue;
			}
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (prefab != null)
			{
				_palette.Add(prefab);
			}
		}

		_palette.Sort(ComparePrefabName);
	}

	private static int ComparePrefabName(GameObject a, GameObject b)
	{
		if (a == null || b == null)
		{
			return 0;
		}
		return string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase);
	}
}
