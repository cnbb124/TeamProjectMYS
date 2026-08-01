using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// =====================================================================
// ProjectHubWindow — 씬 탭.
//
// 1) SCENE_TYPE ↔ 실제 씬 파일 ↔ 빌드 세팅, 세 곳을 한 표로 대조함.
//    이 셋 중 하나만 어긋나도 조용히 실패함 —
//    SCENE_TYPE 이름은 씬 파일명과 같아야 GameManager가 씬을 찾고(LoadScene/BGM/curSceneType),
//    빌드 세팅에 없으면 런타임 로드가 실패함.
//
// 2) 현재 열려 있는 씬에 필요한 오브젝트가 있는지 진단함.
//    매니저 다수가 DDOL이라 앞 씬에서 넘어오는 게 정상이므로, 여기서 없다고 곧 오류는 아님 —
//    "이 씬을 에디터에서 바로 재생할 수 있는 상태인가"를 보는 용도임.
// =====================================================================
public partial class ProjectHubWindow
{
	private class SceneRow
	{
		public string typeName;      // SCENE_TYPE 이름 (없으면 빈 문자열)
		public SCENE_TYPE sceneType = SCENE_TYPE.UNKNOWN;
		public string assetPath;     // 실제 씬 파일 경로 (없으면 빈 문자열)
		public bool inBuildSettings;
		public bool buildEnabled;
	}

	private readonly List<SceneRow> _sceneRows = new List<SceneRow>();
	private Vector2 _sceneScroll;
	private bool _sceneShowWorkScenes;

	private void SceneTabOnEnable()
	{
		RefreshSceneRows();
		EditorBuildSettings.sceneListChanged -= OnBuildSceneListChanged;
		EditorBuildSettings.sceneListChanged += OnBuildSceneListChanged;
	}

	private void SceneTabOnDisable()
	{
		EditorBuildSettings.sceneListChanged -= OnBuildSceneListChanged;
	}

	// 빌드 세팅이 어디서 바뀌든(빌드 세팅 창, 다른 스크립트) 표를 즉시 다시 읽음.
	private void OnBuildSceneListChanged()
	{
		RefreshSceneRows();
		Repaint();
	}

	// 씬 파일 추가·이름변경은 이벤트가 없어서, 창을 다시 잡을 때 훑음.
	private void SceneTabOnFocus()
	{
		RefreshSceneRows();
		Repaint();
	}

	private void DrawSceneTab()
	{
		EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
		if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(64f)))
		{
			RefreshSceneRows();
		}
		_sceneShowWorkScenes = GUILayout.Toggle(_sceneShowWorkScenes, "작업씬도 보기", EditorStyles.toolbarButton, GUILayout.Width(96f));
		GUILayout.FlexibleSpace();
		EditorGUILayout.LabelField("현재 씬: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
			EditorStyles.miniLabel, GUILayout.Width(220f));
		EditorGUILayout.EndHorizontal();

		_sceneScroll = EditorGUILayout.BeginScrollView(_sceneScroll);

		DrawSceneCreateSection();

		SectionHeader("SCENE_TYPE ↔ 씬 파일 ↔ 빌드 세팅");
		DrawSceneTable();

		EditorGUILayout.Space(8f);
		SectionHeader("현재 열린 씬 진단");
		DrawCurrentSceneDiagnosis();

		EditorGUILayout.EndScrollView();
	}

	private void DrawSceneTable()
	{
		EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
		EditorGUILayout.LabelField("SCENE_TYPE", EditorStyles.miniBoldLabel, GUILayout.Width(150f));
		EditorGUILayout.LabelField("파일", EditorStyles.miniBoldLabel, GUILayout.Width(48f));
		EditorGUILayout.LabelField("빌드", EditorStyles.miniBoldLabel, GUILayout.Width(48f));
		EditorGUILayout.LabelField("경로", EditorStyles.miniBoldLabel);
		EditorGUILayout.EndHorizontal();

		for (int i = 0; i < _sceneRows.Count; i++)
		{
			SceneRow row = _sceneRows[i];
			bool isWorkScene = string.IsNullOrEmpty(row.typeName);
			if (isWorkScene && !_sceneShowWorkScenes)
			{
				continue;
			}

			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.LabelField(isWorkScene ? "(작업씬)" : row.typeName, GUILayout.Width(150f));

			bool hasFile = !string.IsNullOrEmpty(row.assetPath);
			DrawMark(hasFile, 48f);

			// 작업씬은 빌드에 없는 게 정상이라 표시만 하고 판정하지 않음
			if (isWorkScene)
			{
				EditorGUILayout.LabelField("-", GUILayout.Width(48f));
			}
			else
			{
				DrawBuildMark(row.inBuildSettings, row.buildEnabled, 48f);
			}

			if (hasFile)
			{
				if (GUILayout.Button(row.assetPath, EditorStyles.miniLabel))
				{
					OpenScene(row.assetPath);
				}
			}
			else
			{
				Color prev = GUI.color;
				GUI.color = new Color(1f, 0.6f, 0.6f);
				EditorGUILayout.LabelField($"'{row.typeName}' 이름의 씬 파일이 없음", EditorStyles.miniLabel);
				GUI.color = prev;
			}

			if (hasFile && !isWorkScene && row.inBuildSettings && !row.buildEnabled)
			{
				if (GUILayout.Button("활성화", GUILayout.Width(60f)))
				{
					EnableSceneInBuildSettings(row.assetPath);
					RefreshSceneRows();
				}
			}

			if (hasFile && !isWorkScene && !row.inBuildSettings)
			{
				if (GUILayout.Button("빌드에 추가", GUILayout.Width(84f)))
				{
					AddSceneToBuildSettings(row.assetPath);
					RefreshSceneRows();
				}
			}

			if (hasFile)
			{
				if (GUILayout.Button("삭제", GUILayout.Width(44f)))
				{
					// 씬 로드/모달이 끼므로 GUI가 끝난 뒤에 실행
					string deferredPath = row.assetPath;
					SCENE_TYPE deferredType = isWorkScene ? SCENE_TYPE.UNKNOWN : row.sceneType;
					EditorApplication.delayCall += () => DeleteScene(deferredPath, deferredType);
				}
			}

			EditorGUILayout.EndHorizontal();
		}
	}

	private static void DrawMark(bool ok, float width)
	{
		Color prev = GUI.color;
		GUI.color = ok ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.6f, 0.6f);
		EditorGUILayout.LabelField(ok ? "O" : "X", GUILayout.Width(width));
		GUI.color = prev;
	}

	// 빌드 열은 상태가 셋 — 목록에 없음(X) / 목록엔 있는데 체크 꺼짐(OFF) / 정상(O).
	// 둘을 같은 X로 묶으면 "왜 빌드에 넣었는데 X냐"가 됨.
	private static void DrawBuildMark(bool inList, bool enabled, float width)
	{
		string text;
		Color color;
		string tip;

		if (!inList)
		{
			text = "X";
			color = new Color(1f, 0.6f, 0.6f);
			tip = "빌드 세팅 목록에 없음 — 런타임 씬 로드가 실패함";
		}
		else if (!enabled)
		{
			text = "OFF";
			color = new Color(1f, 0.9f, 0.5f);
			tip = "목록엔 있지만 체크박스가 꺼져 있음 — 빌드에 포함되지 않음";
		}
		else
		{
			text = "O";
			color = new Color(0.6f, 1f, 0.6f);
			tip = "빌드 세팅에 포함됨";
		}

		Color prev = GUI.color;
		GUI.color = color;
		EditorGUILayout.LabelField(new GUIContent(text, tip), GUILayout.Width(width));
		GUI.color = prev;
	}

	// 목록엔 있는데 체크만 꺼진 씬을 켬.
	private static void EnableSceneInBuildSettings(string path)
	{
		EditorBuildSettingsScene[] list = EditorBuildSettings.scenes;
		for (int i = 0; i < list.Length; i++)
		{
			if (list[i].path == path)
			{
				list[i].enabled = true;
			}
		}
		EditorBuildSettings.scenes = list;
		Debug.Log($"[Hub] 빌드 세팅에서 활성화함: {path}");
	}

	// 현재 씬에 있어야 이 씬만 열고 바로 재생할 수 있는 것들.
	// 매니저는 대부분 DDOL이라 정식 진입(MAIN부터)에서는 앞 씬에서 넘어옴 — 그래서 경고 수준으로만 봄.
	private void DrawCurrentSceneDiagnosis()
	{
		ReportPresence<GameManager>("GameManager", true);
		ReportPresence<PoolManager>("PoolManager", true);
		ReportPresence<SoundManager>("SoundManager", true);
		ReportPresence<VFXManager>("VFXManager", true);
		ReportPresence<InputManager>("InputManager", true);
		ReportPresence<UnitManager>("UnitManager", true);
		ReportPresence<InventoryManager>("InventoryManager", true);

		EditorGUILayout.Space(4f);
		ReportPresence<SpawnManager>("SpawnManager (전투 씬 필수)", false);
		ReportPresence<UnityEngine.EventSystems.EventSystem>("EventSystem (UI 입력)", false);

		Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>(true);
		if (cameras.Length == 0)
		{
			ResultLine(CheckLevel.Fail, "카메라가 없음");
		}
		else
		{
			ResultLine(CheckLevel.Pass, $"카메라 {cameras.Length}개");
		}

		// Missing Script는 씬에 남아 있으면 콘솔 경고만 내고 조용히 방치되기 쉬움
		int missing = CountMissingScripts();
		if (missing > 0)
		{
			ResultLine(CheckLevel.Fail, $"Missing Script가 {missing}개 있음 → 삭제된 스크립트의 잔재");
		}
		else
		{
			ResultLine(CheckLevel.Pass, "Missing Script 없음");
		}
	}

	private void ReportPresence<T>(string label, bool ddolAllowed) where T : Component
	{
		T[] found = UnityEngine.Object.FindObjectsOfType<T>(true);
		if (found.Length == 1)
		{
			ResultLine(CheckLevel.Pass, label + " 있음", found[0]);
			return;
		}
		if (found.Length > 1)
		{
			ResultLine(CheckLevel.Warn, $"{label}가 {found.Length}개 있음 (싱글톤이면 중복)", found[0]);
			return;
		}
		// DDOL로 앞 씬에서 넘어오는 것은 이 씬에 없는 게 정상일 수 있음
		ResultLine(ddolAllowed ? CheckLevel.Warn : CheckLevel.Fail,
			label + " 없음" + (ddolAllowed ? " (앞 씬에서 넘어오는 구성이면 정상)" : ""));
	}

	private static int CountMissingScripts()
	{
		int count = 0;
		GameObject[] all = UnityEngine.Object.FindObjectsOfType<GameObject>(true);
		for (int i = 0; i < all.Length; i++)
		{
			Component[] components = all[i].GetComponents<Component>();
			for (int j = 0; j < components.Length; j++)
			{
				if (components[j] == null)
				{
					count++;
				}
			}
		}
		return count;
	}

	private void RefreshSceneRows()
	{
		_sceneRows.Clear();

		// 씬 파일 전수 수집 (이름 → 경로).
		// 이름 비교는 대소문자 무시 — SceneManager.LoadScene도 대소문자를 안 가림.
		// FindAssets가 지운 에셋을 잠시 더 돌려주므로 파일 존재를 직접 확인해 걸러낸다.
		Dictionary<string, string> sceneFiles =
			new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
		string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
		for (int i = 0; i < guids.Length; i++)
		{
			string path = AssetDatabase.GUIDToAssetPath(guids[i]);
			if (!SceneFileExists(path))
			{
				continue;
			}
			string name = System.IO.Path.GetFileNameWithoutExtension(path);
			if (!sceneFiles.ContainsKey(name))
			{
				sceneFiles.Add(name, path);
			}
		}

		// 빌드 세팅 상태 (경로 → 활성 여부)
		Dictionary<string, bool> buildScenes = new Dictionary<string, bool>();
		EditorBuildSettingsScene[] buildList = EditorBuildSettings.scenes;
		for (int i = 0; i < buildList.Length; i++)
		{
			if (!buildScenes.ContainsKey(buildList[i].path))
			{
				buildScenes.Add(buildList[i].path, buildList[i].enabled);
			}
		}

		HashSet<string> covered = new HashSet<string>();

		// SCENE_TYPE 기준 행
		foreach (SCENE_TYPE type in System.Enum.GetValues(typeof(SCENE_TYPE)))
		{
			if (type == SCENE_TYPE.UNKNOWN)
			{
				continue;
			}
			string typeName = type.ToString();
			SceneRow row = new SceneRow();
			row.typeName = typeName;
			row.sceneType = type;

			if (sceneFiles.TryGetValue(typeName, out string path))
			{
				row.assetPath = path;
				covered.Add(path);
				if (buildScenes.TryGetValue(path, out bool enabled))
				{
					row.inBuildSettings = true;
					row.buildEnabled = enabled;
				}
			}
			_sceneRows.Add(row);
		}

		// SCENE_TYPE에 없는 씬 파일(작업씬) 행
		foreach (KeyValuePair<string, string> kv in sceneFiles)
		{
			if (covered.Contains(kv.Value))
			{
				continue;
			}
			SceneRow row = new SceneRow();
			row.typeName = "";
			row.assetPath = kv.Value;
			if (buildScenes.TryGetValue(kv.Value, out bool enabled))
			{
				row.inBuildSettings = true;
				row.buildEnabled = enabled;
			}
			_sceneRows.Add(row);
		}
	}

	private static void OpenScene(string path)
	{
		// 저장 안 한 변경이 있으면 사용자에게 먼저 물어봄 — 그냥 열면 작업이 날아감
		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
		{
			return;
		}
		EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
	}

	private static void AddSceneToBuildSettings(string path)
	{
		List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].path == path)
			{
				return;
			}
		}
		list.Add(new EditorBuildSettingsScene(path, true));
		EditorBuildSettings.scenes = list.ToArray();
		Debug.Log($"[Hub] 빌드 세팅에 추가함: {path}");
	}
}
