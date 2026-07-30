using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================
// ProjectHubWindow — 맵 배치 탭.
//
// 프리팹 팔레트에서 하나 고르고 현재 씬에 배치함. 배치 방식 3가지:
//   단일    — 한 개만 놓음
//   그리드  — 행×열 격자로 놓음(구조물 줄세우기)
//   산포    — 구 반경 안에 랜덤으로 뿌림(소행성대)
//
// 배치는 전부 Undo에 등록됨(Ctrl+Z로 되돌아감).
// 부모 컨테이너를 지정하면 그 아래로 모아서 나중에 통째로 지우기 쉬움.
//
// ※ 프리팹 인스턴스로 놓기 때문에 원본 프리팹을 고치면 배치된 것들도 같이 바뀜.
// =====================================================================
public partial class ProjectHubWindow
{
	private enum PlaceMode
	{
		Single = 0,
		Grid = 1,
		Scatter = 2,
	}

	private static readonly string[] PlaceModeLabels = { "단일", "그리드", "산포" };

	// 팔레트로 훑을 폴더. 맵 오브젝트가 다른 곳에도 있으면 여기 추가할 것
	private static readonly string[] MapPaletteRoots =
	{
		"Assets/3.Prefabs/Map",
		"Assets/3.Prefabs/Units/Enemies",
	};

	private readonly List<GameObject> _palette = new List<GameObject>();
	private GameObject _paletteSelected;
	private string _paletteFilter = "";
	private Vector2 _paletteScroll;
	private float _paletteWidth = 280f;

	private PlaceMode _placeMode = PlaceMode.Single;
	private string _containerName = "MapObjects";
	private bool _placeAtSceneViewPivot = true;
	private Vector3 _placeOrigin = Vector3.zero;

	// 그리드
	private int _gridColumns = 3;
	private int _gridRows = 3;
	private float _gridSpacing = 50f;

	// 산포
	private int _scatterCount = 20;
	private float _scatterRadius = 200f;
	private float _scatterMinDistance = 20f;

	// 공통 랜덤화
	private bool _randomYaw = true;
	private bool _randomFullRotation;
	private Vector2 _randomScaleRange = new Vector2(1f, 1f);

	private void MapTabOnEnable()
	{
		RefreshPalette();
	}

	private void DrawMapTab()
	{
		EditorGUILayout.BeginHorizontal();
		DrawPalette();
		_paletteWidth = DrawVerticalSplitter(_paletteWidth, 160f, 640f);
		DrawPlaceOptions();
		EditorGUILayout.EndHorizontal();
	}

	private void DrawPalette()
	{
		EditorGUILayout.BeginVertical(GUILayout.Width(_paletteWidth));

		EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
		_paletteFilter = EditorGUILayout.TextField(_paletteFilter, EditorStyles.toolbarSearchField);
		if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(64f)))
		{
			RefreshPalette();
		}
		EditorGUILayout.EndHorizontal();

		_paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll);
		for (int i = 0; i < _palette.Count; i++)
		{
			GameObject prefab = _palette[i];
			if (prefab == null)
			{
				continue;
			}
			if (!MatchesFilter(prefab.name, _paletteFilter))
			{
				continue;
			}

			bool isSelected = prefab == _paletteSelected;
			EditorGUILayout.BeginHorizontal(isSelected ? EditorStyles.helpBox : EditorStyles.label);
			if (GUILayout.Button(prefab.name, EditorStyles.label))
			{
				_paletteSelected = prefab;
			}
			EditorGUILayout.EndHorizontal();
		}
		EditorGUILayout.EndScrollView();

		if (_palette.Count == 0)
		{
			EditorGUILayout.HelpBox("팔레트가 비었음.\n탐색 경로: " + string.Join(", ", MapPaletteRoots), MessageType.Info);
		}

		EditorGUILayout.EndVertical();
	}

	private void DrawPlaceOptions()
	{
		EditorGUILayout.BeginVertical(EditorStyles.helpBox);

		if (_paletteSelected == null)
		{
			EditorGUILayout.HelpBox("왼쪽 팔레트에서 프리팹을 고를 것.", MessageType.None);
			EditorGUILayout.EndVertical();
			return;
		}

		EditorGUILayout.LabelField("선택: " + _paletteSelected.name, EditorStyles.boldLabel);
		EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(_paletteSelected), EditorStyles.miniLabel);

		SectionHeader("배치 위치");
		_placeAtSceneViewPivot = EditorGUILayout.Toggle("씬 뷰 중심에 배치", _placeAtSceneViewPivot);
		if (!_placeAtSceneViewPivot)
		{
			_placeOrigin = EditorGUILayout.Vector3Field("좌표", _placeOrigin);
		}
		_containerName = EditorGUILayout.TextField("부모 컨테이너 이름", _containerName);
		EditorGUILayout.LabelField("비워두면 씬 루트에 놓임", EditorStyles.miniLabel);

		SectionHeader("배치 방식");
		_placeMode = (PlaceMode)GUILayout.Toolbar((int)_placeMode, PlaceModeLabels);

		switch (_placeMode)
		{
			case PlaceMode.Grid:
				_gridColumns = Mathf.Max(1, EditorGUILayout.IntField("열", _gridColumns));
				_gridRows = Mathf.Max(1, EditorGUILayout.IntField("행", _gridRows));
				_gridSpacing = EditorGUILayout.FloatField("간격", _gridSpacing);
				EditorGUILayout.LabelField($"총 {_gridColumns * _gridRows}개", EditorStyles.miniLabel);
				break;

			case PlaceMode.Scatter:
				_scatterCount = Mathf.Max(1, EditorGUILayout.IntField("개수", _scatterCount));
				_scatterRadius = EditorGUILayout.FloatField("반경", _scatterRadius);
				_scatterMinDistance = EditorGUILayout.FloatField("최소 간격", _scatterMinDistance);
				EditorGUILayout.LabelField("최소 간격을 크게 잡으면 자리를 못 찾아 개수가 줄어들 수 있음", EditorStyles.miniLabel);
				break;
		}

		SectionHeader("랜덤화");
		_randomFullRotation = EditorGUILayout.Toggle("완전 랜덤 회전", _randomFullRotation);
		if (!_randomFullRotation)
		{
			_randomYaw = EditorGUILayout.Toggle("Y축만 랜덤 회전", _randomYaw);
		}
		_randomScaleRange = EditorGUILayout.Vector2Field("스케일 범위 (min/max)", _randomScaleRange);
		if (_randomScaleRange.x <= 0f)
		{
			_randomScaleRange.x = 0.01f;
		}
		if (_randomScaleRange.y < _randomScaleRange.x)
		{
			_randomScaleRange.y = _randomScaleRange.x;
		}

		EditorGUILayout.Space(8f);
		if (GUILayout.Button("배치", GUILayout.Height(28f)))
		{
			Place();
		}

		// 씬 뷰에서 구 표면을 클릭해 놓는 방식. 좌표 입력 없이 보면서 배치할 때 씀
		DrawSpherePlaceSection();

		EditorGUILayout.EndVertical();
	}

	private void Place()
	{
		Vector3 origin = _placeOrigin;
		if (_placeAtSceneViewPivot)
		{
			if (SceneView.lastActiveSceneView != null)
			{
				origin = SceneView.lastActiveSceneView.pivot;
			}
			else
			{
				Debug.LogWarning("[Hub] 씬 뷰가 없어 좌표(0,0,0)에 배치함.");
			}
		}

		Transform parent = ResolveContainer();

		// Undo 그룹으로 묶어야 한 번의 Ctrl+Z로 이번 배치 전체가 되돌아감
		Undo.SetCurrentGroupName("Hub 맵 배치");
		int group = Undo.GetCurrentGroup();

		List<GameObject> placed = new List<GameObject>();
		switch (_placeMode)
		{
			case PlaceMode.Single:
				GameObject one = Spawn(origin, parent);
				if (one != null)
				{
					placed.Add(one);
				}
				break;

			case PlaceMode.Grid:
				PlaceGrid(origin, parent, placed);
				break;

			case PlaceMode.Scatter:
				PlaceScatter(origin, parent, placed);
				break;
		}

		Undo.CollapseUndoOperations(group);

		if (placed.Count > 0)
		{
			Selection.objects = placed.ToArray();
			Debug.Log($"[Hub] {_paletteSelected.name} {placed.Count}개 배치함.");
		}
	}

	private void PlaceGrid(Vector3 origin, Transform parent, List<GameObject> placed)
	{
		// 격자의 중심을 origin에 맞춤 — 놓고 나서 통째로 옮기는 수고를 줄임
		float width = (_gridColumns - 1) * _gridSpacing;
		float depth = (_gridRows - 1) * _gridSpacing;
		Vector3 corner = origin - new Vector3(width * 0.5f, 0f, depth * 0.5f);

		for (int r = 0; r < _gridRows; r++)
		{
			for (int c = 0; c < _gridColumns; c++)
			{
				Vector3 pos = corner + new Vector3(c * _gridSpacing, 0f, r * _gridSpacing);
				GameObject go = Spawn(pos, parent);
				if (go != null)
				{
					placed.Add(go);
				}
			}
		}
	}

	private void PlaceScatter(Vector3 origin, Transform parent, List<GameObject> placed)
	{
		List<Vector3> taken = new List<Vector3>();
		// 후보를 무한히 뽑지 않도록 시도 횟수에 상한을 둠 — 최소 간격이 크면 자리가 안 남
		int maxAttempts = _scatterCount * 30;
		int attempts = 0;

		while (placed.Count < _scatterCount && attempts < maxAttempts)
		{
			attempts++;
			Vector3 candidate = origin + Random.insideUnitSphere * _scatterRadius;

			bool tooClose = false;
			for (int i = 0; i < taken.Count; i++)
			{
				if (Vector3.Distance(taken[i], candidate) < _scatterMinDistance)
				{
					tooClose = true;
					break;
				}
			}
			if (tooClose)
			{
				continue;
			}

			GameObject go = Spawn(candidate, parent);
			if (go != null)
			{
				placed.Add(go);
				taken.Add(candidate);
			}
		}

		if (placed.Count < _scatterCount)
		{
			Debug.LogWarning($"[Hub] 최소 간격({_scatterMinDistance}) 때문에 {_scatterCount}개 중 {placed.Count}개만 배치함. " +
							 "반경을 키우거나 최소 간격을 줄일 것.");
		}
	}

	private GameObject Spawn(Vector3 position, Transform parent)
	{
		GameObject instance = PrefabUtility.InstantiatePrefab(_paletteSelected) as GameObject;
		if (instance == null)
		{
			Debug.LogWarning($"[Hub] 프리팹 인스턴스 생성 실패: {_paletteSelected.name}");
			return null;
		}

		Undo.RegisterCreatedObjectUndo(instance, "Hub 맵 배치");

		if (parent != null)
		{
			instance.transform.SetParent(parent, true);
		}
		instance.transform.position = position;

		if (_randomFullRotation)
		{
			instance.transform.rotation = Random.rotation;
		}
		else if (_randomYaw)
		{
			instance.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
		}

		if (!Mathf.Approximately(_randomScaleRange.x, 1f) || !Mathf.Approximately(_randomScaleRange.y, 1f))
		{
			float scale = Random.Range(_randomScaleRange.x, _randomScaleRange.y);
			instance.transform.localScale = Vector3.one * scale;
		}

		return instance;
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
