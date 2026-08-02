using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================
// ProjectHubWindow — 데이터 탭.
//
// 프로젝트의 ScriptableObject 에셋을 종류별로 모아 목록/검색/생성/편집함.
// 지금까지 에셋 하나 고치려면 폴더를 뒤져 찾아 클릭해야 했던 것을 한 창으로 합친 것.
//
// 오른쪽 편집 영역은 그 에셋의 기본 인스펙터를 그대로 임베드함(Editor.CreateEditor) —
// PartDataEditor처럼 커스텀 인스펙터가 있는 타입도 그 인스펙터가 그대로 나옴.
// =====================================================================
public partial class ProjectHubWindow
{
	// 탐색 대상 루트. 프로젝트 전체를 훑으면 패키지 에셋까지 걸려서 범위를 좁힘
	private static readonly string[] DataSearchRoots =
	{
		"Assets/8.Data",
		"Assets/Data",
		"Assets/2.Scripts",
	};

	// 왼쪽 목록에 뜨는 타입 그룹. 표시명 → 실제 타입명(어셈블리 한정 없이 클래스명만)
	private static readonly (string label, string typeName)[] DataGroups =
	{
		("전체", ""),
		("파츠", "PartData"),
		("소비품", "ConsumableData"),
		("총알", "BulletData"),
		("미사일", "MissileData"),
		("클러스터", "ClusterMisslleData"),
		("스킬", "SkillData"),
		("레벨 스탯", "LevelStatData"),
		("시작 데이터", "GameStartData"),
		("기본 장착(레거시)", "DefaultLoadout"),
		("자원", "ResourceData"),
		("맵 리스트","MapListData"),
		("적 웨이브", "WaveData")
	};

	private int _dataGroupIndex;
	private float _dataListWidth = 300f;
	private string _dataFilter = "";
	private Vector2 _dataListScroll;
	private Vector2 _dataInspectorScroll;

	private readonly List<ScriptableObject> _dataAssets = new List<ScriptableObject>();
	private ScriptableObject _dataSelected;
	private Editor _dataSelectedEditor;

	private void DataTabOnEnable()
	{
		RefreshDataAssets();
	}

	private void DataTabOnDisable()
	{
		if (_dataSelectedEditor != null)
		{
			DestroyImmediate(_dataSelectedEditor);
			_dataSelectedEditor = null;
		}
	}

	private void DrawDataTab()
	{
		DrawDataToolbar();

		EditorGUILayout.BeginHorizontal();
		DrawDataList();
		// 분할선을 잡고 끌면 목록 폭이 바뀜 — 타입 이름이 잘려 보이는 걸 사용자가 직접 넓힐 수 있게 함
		_dataListWidth = DrawVerticalSplitter(_dataListWidth, 160f, 640f);
		DrawDataInspector();
		EditorGUILayout.EndHorizontal();
	}

	private void DrawDataToolbar()
	{
		EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

		string[] labels = new string[DataGroups.Length];
		for (int i = 0; i < DataGroups.Length; i++)
		{
			labels[i] = DataGroups[i].label;
		}

		int newGroup = EditorGUILayout.Popup(_dataGroupIndex, labels, EditorStyles.toolbarPopup, GUILayout.Width(110f));
		if (newGroup != _dataGroupIndex)
		{
			_dataGroupIndex = newGroup;
			RefreshDataAssets();
		}

		string newFilter = EditorGUILayout.TextField(_dataFilter, EditorStyles.toolbarSearchField);
		if (newFilter != _dataFilter)
		{
			_dataFilter = newFilter;
		}

		if (GUILayout.Button("새로 생성", EditorStyles.toolbarButton, GUILayout.Width(64f)))
		{
			CreateNewDataAsset();
		}

		if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(64f)))
		{
			RefreshDataAssets();
		}

		if (GUILayout.Button("DB 전체 갱신", EditorStyles.toolbarButton, GUILayout.Width(90f)))
		{
			EditorApplication.delayCall += RebuildAllDatabases;
		}

		EditorGUILayout.LabelField($"{_dataAssets.Count}개", EditorStyles.miniLabel, GUILayout.Width(48f));
		EditorGUILayout.EndHorizontal();
	}

	// 프로젝트의 ItemDatabase / SkillDatabase 에셋을 전부 찾아 다시 스캔함
	private static void RebuildAllDatabases()
	{
		int items = RebuildDatabases<ItemDatabase, ItemData>("allItems",
			asset => (int)asset.id, asset => asset.id != ITEM_ID.NONE, "ITEM_ID");
		int skills = RebuildDatabases<SkillDatabase, SkillData>("allSkills",
			asset => (int)asset.id, asset => asset.id != SKILL_ID.NONE, "SKILL_ID");

		AssetDatabase.SaveAssets();
		Debug.Log($"[Hub] DB 갱신 완료 — ItemDatabase {items}종, SkillDatabase {skills}종 처리");
	}

	private static int RebuildDatabases<TDb, TAsset>(string arrayField,
		System.Func<TAsset, int> idOf, System.Func<TAsset, bool> hasId, string idName)
		where TDb : ScriptableObject
		where TAsset : ScriptableObject
	{
		List<TAsset> found = new List<TAsset>();
		List<string> missingId = new List<string>();

		string[] assetGuids = AssetDatabase.FindAssets($"t:{typeof(TAsset).Name}");
		for (int i = 0; i < assetGuids.Length; i++)
		{
			string path = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
			TAsset asset = AssetDatabase.LoadAssetAtPath<TAsset>(path);
			if (asset == null)
			{
				continue;
			}
			if (!hasId(asset))
			{
				missingId.Add(path);
				continue;
			}
			found.Add(asset);
		}
		found.Sort((a, b) => idOf(a).CompareTo(idOf(b)));

		string[] dbGuids = AssetDatabase.FindAssets($"t:{typeof(TDb).Name}");
		int dbCount = 0;
		for (int i = 0; i < dbGuids.Length; i++)
		{
			TDb db = AssetDatabase.LoadAssetAtPath<TDb>(AssetDatabase.GUIDToAssetPath(dbGuids[i]));
			if (db == null)
			{
				continue;
			}

			SerializedObject so = new SerializedObject(db);
			SerializedProperty prop = so.FindProperty(arrayField);
			if (prop == null || !prop.isArray)
			{
				continue;
			}
			prop.arraySize = found.Count;
			for (int j = 0; j < found.Count; j++)
			{
				prop.GetArrayElementAtIndex(j).objectReferenceValue = found[j];
			}
			so.ApplyModifiedProperties();
			EditorUtility.SetDirty(db);
			dbCount++;
		}

		if (dbCount == 0)
		{
			Debug.LogWarning($"[Hub] {typeof(TDb).Name} 에셋을 찾지 못함.");
		}
		if (missingId.Count > 0)
		{
			Debug.LogWarning($"[Hub] {idName}.NONE이라 제외된 항목 {missingId.Count}개:\n" + string.Join("\n", missingId));
		}
		return found.Count;
	}

	private void DrawDataList()
	{
		EditorGUILayout.BeginVertical(GUILayout.Width(_dataListWidth));
		_dataListScroll = EditorGUILayout.BeginScrollView(_dataListScroll);

		for (int i = 0; i < _dataAssets.Count; i++)
		{
			ScriptableObject asset = _dataAssets[i];
			if (asset == null)
			{
				continue;
			}
			if (!MatchesFilter(asset.name, _dataFilter))
			{
				continue;
			}

			bool isSelected = asset == _dataSelected;
			GUIStyle style = isSelected ? EditorStyles.helpBox : EditorStyles.label;
			EditorGUILayout.BeginHorizontal(style);
			if (GUILayout.Button(asset.name, EditorStyles.label))
			{
				SelectDataAsset(asset);
			}
			// 타입 칸은 목록 폭에 비례 — 좁혀도 이름이 먼저 보이고, 넓히면 타입까지 다 보임
			float typeWidth = Mathf.Clamp(_dataListWidth * 0.4f, 60f, 220f);
			EditorGUILayout.LabelField(asset.GetType().Name, EditorStyles.miniLabel, GUILayout.Width(typeWidth));
			EditorGUILayout.EndHorizontal();
		}

		EditorGUILayout.EndScrollView();

		if (_dataAssets.Count == 0)
		{
			EditorGUILayout.HelpBox("해당 종류의 에셋이 없음.\n탐색 경로: " + string.Join(", ", DataSearchRoots), MessageType.Info);
		}

		EditorGUILayout.EndVertical();
	}

	private void DrawDataInspector()
	{
		EditorGUILayout.BeginVertical(EditorStyles.helpBox);

		if (_dataSelected == null)
		{
			EditorGUILayout.HelpBox("왼쪽 목록에서 에셋을 고르면 여기서 바로 편집할 수 있음.", MessageType.None);
			EditorGUILayout.EndVertical();
			return;
		}

		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField(_dataSelected.name, EditorStyles.boldLabel);
		if (GUILayout.Button("프로젝트에서 보기", GUILayout.Width(120f)))
		{
			EditorGUIUtility.PingObject(_dataSelected);
		}
		if (GUILayout.Button("복제", GUILayout.Width(48f)))
		{
			DuplicateDataAsset(_dataSelected);
		}
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(_dataSelected), EditorStyles.miniLabel);
		EditorGUILayout.Space(4f);

		_dataInspectorScroll = EditorGUILayout.BeginScrollView(_dataInspectorScroll);
		if (_dataSelectedEditor != null)
		{
			using (new RichTextScope(true))
			{
				// 커스텀 인스펙터가 있으면 그것이, 없으면 기본 인스펙터가 그려짐
				_dataSelectedEditor.OnInspectorGUI();
			}
		}
		EditorGUILayout.EndScrollView();

		EditorGUILayout.EndVertical();
	}

	private void SelectDataAsset(ScriptableObject asset)
	{
		if (_dataSelected == asset)
		{
			return;
		}
		_dataSelected = asset;

		if (_dataSelectedEditor != null)
		{
			DestroyImmediate(_dataSelectedEditor);
			_dataSelectedEditor = null;
		}
		if (_dataSelected != null)
		{
			_dataSelectedEditor = Editor.CreateEditor(_dataSelected);
		}
		_dataInspectorScroll = Vector2.zero;
	}

	// 현재 고른 종류의 새 에셋을 만듦. '전체'에서는 만들 타입을 정할 수 없어 거부함.
	private void CreateNewDataAsset()
	{
		string typeName = DataGroups[_dataGroupIndex].typeName;
		if (string.IsNullOrEmpty(typeName))
		{
			Debug.LogWarning("[Hub] '전체'로는 만들 타입을 알 수 없음 — 종류를 먼저 고를 것.");
			return;
		}

		System.Type type = FindScriptableType(typeName);
		if (type == null)
		{
			Debug.LogWarning($"[Hub] 타입을 찾지 못함: {typeName}");
			return;
		}
		if (type.IsAbstract)
		{
			Debug.LogWarning($"[Hub] {typeName}은 추상 타입이라 직접 만들 수 없음 — 파생 타입을 고를 것.");
			return;
		}

		// 같은 타입 에셋이 이미 있으면 그 폴더를 기본 위치로 제안함
		string folder = "Assets";
		for (int i = 0; i < _dataAssets.Count; i++)
		{
			if (_dataAssets[i] != null && _dataAssets[i].GetType() == type)
			{
				folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(_dataAssets[i]));
				break;
			}
		}

		string path = EditorUtility.SaveFilePanelInProject($"새 {typeName}", $"New {typeName}", "asset", "", folder);
		if (string.IsNullOrEmpty(path))
		{
			return;
		}

		ScriptableObject created = ScriptableObject.CreateInstance(type);
		AssetDatabase.CreateAsset(created, path);
		AssetDatabase.SaveAssets();
		RefreshDataAssets();
		SelectDataAsset(created);
		EditorGUIUtility.PingObject(created);
	}

	// 클래스명으로 ScriptableObject 파생 타입을 찾음. TypeCache는 에디터가 미리 만들어둔 표라 빠름
	private static System.Type FindScriptableType(string typeName)
	{
		TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<ScriptableObject>();
		foreach (System.Type t in types)
		{
			if (t.Name == typeName)
			{
				return t;
			}
		}
		return null;
	}

	// 같은 폴더에 사본을 만들고 바로 선택. 변형 데이터(변형탄 등)를 만들 때 쓰는 흐름
	private void DuplicateDataAsset(ScriptableObject source)
	{
		string path = AssetDatabase.GetAssetPath(source);
		if (string.IsNullOrEmpty(path))
		{
			return;
		}
		string newPath = AssetDatabase.GenerateUniqueAssetPath(path);
		if (!AssetDatabase.CopyAsset(path, newPath))
		{
			Debug.LogWarning($"[Hub] 복제 실패: {path}");
			return;
		}
		AssetDatabase.Refresh();
		RefreshDataAssets();

		ScriptableObject copy = AssetDatabase.LoadAssetAtPath<ScriptableObject>(newPath);
		if (copy != null)
		{
			SelectDataAsset(copy);
			EditorGUIUtility.PingObject(copy);
		}
	}

	private void RefreshDataAssets()
	{
		_dataAssets.Clear();

		// 존재하는 경로만 넘김 — 없는 폴더를 FindAssets에 주면 예외가 남
		List<string> roots = new List<string>();
		for (int i = 0; i < DataSearchRoots.Length; i++)
		{
			if (AssetDatabase.IsValidFolder(DataSearchRoots[i]))
			{
				roots.Add(DataSearchRoots[i]);
			}
		}
		if (roots.Count == 0)
		{
			return;
		}

		string typeName = DataGroups[_dataGroupIndex].typeName;
		// 타입명이 비어 있으면(전체) ScriptableObject 전부를 훑고, 아니면 그 타입만 검색
		string query = string.IsNullOrEmpty(typeName) ? "t:ScriptableObject" : "t:" + typeName;

		string[] guids = AssetDatabase.FindAssets(query, roots.ToArray());
		for (int i = 0; i < guids.Length; i++)
		{
			string path = AssetDatabase.GUIDToAssetPath(guids[i]);
			ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
			if (asset == null)
			{
				continue;
			}
			// 에디터 스크립트 자신(ScriptableObject 파생 에디터 설정 등)이 섞이지 않게 .asset만 받음
			if (!path.EndsWith(".asset"))
			{
				continue;
			}
			_dataAssets.Add(asset);
		}

		_dataAssets.Sort(CompareAssetName);

		// 선택이 목록에서 사라졌으면 해제
		if (_dataSelected != null && !_dataAssets.Contains(_dataSelected))
		{
			SelectDataAsset(null);
		}
	}

	private static int CompareAssetName(ScriptableObject a, ScriptableObject b)
	{
		if (a == null || b == null)
		{
			return 0;
		}
		int typeCompare = string.Compare(a.GetType().Name, b.GetType().Name, System.StringComparison.OrdinalIgnoreCase);
		if (typeCompare != 0)
		{
			return typeCompare;
		}
		return string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase);
	}
}
