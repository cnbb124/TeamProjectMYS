using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// =====================================================================
// ProjectHubWindow — 새 씬 만들기 섹션 (씬 탭 상단).
//
// 전투 씬 하나를 만들 때 손으로 하던 일을 한 번에 처리함:
//   씬 생성 → 필수 프리팹 배치 → 스폰 포인트 생성 → SpawnManager 배선(웨이브/보스/스폰포인트)
//   → 빌드 세팅 등록 → GameManager 프리팹의 씬-BGM 표에 등록 → 저장
//
// 씬 이름은 SCENE_TYPE 이름과 같아야 함 — GameManager가 이름으로 씬을 판별하기 때문
// (curSceneType 파싱 / BGM 조회 / 게임오버 분기 전부 이름 기준).
// =====================================================================
public partial class ProjectHubWindow
{
	private const string DefaultScenesFolder = "Assets/1.Scenes/BuildScene";

	private SceneTemplateData _template;
	// 프로젝트에 있는 템플릿 에셋을 드롭다운으로 고르게 함 — 에셋을 드래그해 오지 않아도 되게.
	// null이면 아직 안 읽은 상태라 다음 OnGUI에서 채움
	private SceneTemplateData[] _templateAssets;
	private string[] _templateChoices;
	private int _templateIndex;
	// 컨테이너 하위 프리팹의 배치까지 템플릿에 담을지. 새 스테이지는 백지에서 시작하는 게 보통이라 기본은 끔
	private bool _captureChildLayout;
	// 템플릿이 지정한 '새로 만들 빈 오브젝트'와 조명 설정. 템플릿을 불러올 때 채워짐
	private readonly List<string> _createEmptyObjects = new List<string>();
	private bool _createDirectionalLight = true;
	private Vector3 _createLightRotation = new Vector3(50f, -30f, 0f);
	private string _createSceneName = "STAGE2";
	// 이름에서 해석된 SCENE_TYPE. 이름이 표에 없으면 UNKNOWN이고 그때는 BGM 등록을 건너뜀
	private SCENE_TYPE _createSceneType = SCENE_TYPE.UNKNOWN;
	private string _createFolder = DefaultScenesFolder;

	private readonly List<GameObject> _createPrefabs = new List<GameObject>();
	private SOUND_TYPE _createBgm = SOUND_TYPE.SFX_NONE;
	private bool _createRegisterSettings = true;
	private bool _createAddToBuild = true;

	// GameManager 씬 설정표에 같이 등록할 값들. 평소엔 카테고리만 고르면 됨
	private SCENE_CATEGORY _createCategory = SCENE_CATEGORY.BATTLE;
	private bool _createOverrideFlags;
	// 속성 체크박스. 씬 수정 화면과 같은 UI를 쓰려고 SettingFlagNames 순서에 맞춘 배열로 둠
	private readonly bool[] _createFlags = new bool[SettingFlagNames.Length];

	// '추가 항목' 접이식 그룹. 평소엔 이름·종류·배경음만 정하면 되므로 기본은 전부 접음
	private bool _createExtraWave;
	private bool _createExtraMap;
	private bool _createExtraAdvanced;

	// 템플릿 자동 선택을 이미 끝낸 종류. 매 OnGUI마다 에셋을 다시 훑지 않으려고 기억해 둠.
	// 어느 SCENE_CATEGORY와도 안 겹치는 값이어야 첫 그리기에서 한 번 돌아감
	private SCENE_CATEGORY _templateAutoFor = (SCENE_CATEGORY)(-1);

	private readonly List<WaveData> _createWaves = new List<WaveData>();
	private WaveData _createBossWave;

	// 방금 만든 씬 경로. 맵 배치로 넘어가는 안내를 띄우는 데 씀
	private string _justCreatedScene;

	private bool _createRegisterToMap;
	private MapListData _createMapList;
	private int _createMapButtonIndex;

	private int _createDefaultSpawnCount = 8;
	private int _createFixedSpawnCount = 2;
	private float _createSpawnRingRadius = 400f;

	private void DrawSceneCreateSection()
	{
		// ---- 이름 / 저장 위치 ----
		// 씬 수정 화면이 맨 위에 '선택된 씬'을 보여주는 것과 같은 자리 — 여기선 만들 이름을 정함
		EditorGUILayout.LabelField("만들 씬의 이름", HubStyles.SectionTitle);
		_createSceneName = EditorGUILayout.TextField("씬 이름", _createSceneName);
		_createFolder = EditorGUILayout.TextField("저장 폴더", _createFolder);

		// 이름이 SCENE_TYPE에 있는지 — 씬 생성 자체는 이름만으로 되지만,
		// GameManager가 씬을 식별하는 근거(curSceneType/BGM 조회)는 SCENE_TYPE이라 없으면 그 기능만 빠짐.
		// 대소문자는 안 가림(SceneManager.LoadScene도 안 가림).
		bool typeExists = System.Enum.TryParse(_createSceneName, true, out SCENE_TYPE resolved)
						  && System.Enum.IsDefined(typeof(SCENE_TYPE), resolved);
		_createSceneType = typeExists ? resolved : SCENE_TYPE.UNKNOWN;

		if (string.IsNullOrWhiteSpace(_createSceneName))
		{
			EditorGUILayout.HelpBox("씬 이름을 입력할 것.", MessageType.Info);
		}
		else if (typeExists)
		{
			EditorGUILayout.HelpBox($"SCENE_TYPE.{resolved} 로 인식됨 — BGM 등록/씬 판별이 정상 동작함.", MessageType.None);
		}
		else
		{
			EditorGUILayout.HelpBox($"SCENE_TYPE에 '{_createSceneName}'이 없음.\n" +
									"씬은 지금 바로 만들 수 있지만, 추가하지 않으면 GameManager가 이 씬을 식별하지 못해\n" +
									"BGM 자동 재생과 curSceneType 판별이 빠짐.", MessageType.Warning);
			EditorGUI.BeginDisabledGroup(HubEnumEditor.IsCompiling);
			if (GUILayout.Button($"SCENE_TYPE에 '{_createSceneName}' 추가 (코드 수정 + 컴파일)", GUILayout.Height(22f)))
			{
				// 코드 수정 + 리임포트가 끼므로 GUI가 끝난 뒤에 실행
				string deferredName = _createSceneName;
				EditorApplication.delayCall += () => AddSceneTypeMember(deferredName);
			}
			EditorGUI.EndDisabledGroup();
		}

		string scenePath = $"{_createFolder}/{_createSceneName}.unity";
		bool alreadyExists = SceneFileExists(scenePath);
		if (alreadyExists)
		{
			EditorGUILayout.HelpBox($"이미 존재함: {scenePath}\n덮어쓰지 않음. 다른 종류를 고르거나 기존 씬을 열 것.", MessageType.Warning);
		}

		HubStyles.Separator(4f);

		// ---- 씬 종류 / 배경음 / 고급 ----
		// 씬 수정 화면과 같은 UI를 그대로 씀. 종류를 바꾸면 그 종류의 템플릿이 딸려 옴
		SCENE_CATEGORY beforeCategory = _createCategory;
		DrawCategoryBgmAdvanced(ref _createCategory, ref _createBgm, ref _createOverrideFlags, _createFlags);

		if (beforeCategory != _createCategory || _templateAutoFor != _createCategory)
		{
			SelectTemplateForCategory(_createCategory);
		}
		DrawTemplateNotice();

		if (_createSceneType == SCENE_TYPE.UNKNOWN && !string.IsNullOrWhiteSpace(_createSceneName))
		{
			HubStyles.ColoredLabel("SCENE_TYPE이 없어 위 종류·배경음은 저장되지 않습니다. 먼저 SCENE_TYPE에 추가할 것.",
				HubStyles.Warn, EditorStyles.miniLabel);
		}

		HubStyles.Separator(4f);

		// ---- 추가 항목 ----
		// 씬 안을 채우는 값(프리팹·스폰 포인트·조명)은 전부 템플릿이 들고 오므로 여기 안 띄움.
		// 남기는 건 종류만으로 정할 수 없는 것 = 전투 씬의 웨이브와 맵 배정뿐임
		EditorGUILayout.LabelField("추가 항목", HubStyles.SectionTitle);

		// 라벨 칸 폭. 기본값(약 150)이라 긴 라벨이 '...'으로 잘려서 이 구간 동안만 넓혀 씀.
		// EditorGUIUtility는 전역 상태라 구간이 끝나면 반드시 원래대로 되돌려야 다른 창에 영향이 안 감.
		float prevLabelWidth = EditorGUIUtility.labelWidth;
		EditorGUIUtility.labelWidth = 220f;

		if (_createCategory == SCENE_CATEGORY.BATTLE)
		{
			DrawCreateWaveGroup();
			DrawCreateMapGroup();
		}
		else
		{
			HubStyles.ColoredLabel("전투 스테이지를 고르면 웨이브와 맵 선택 배정이 여기 나옵니다.",
				HubStyles.Muted, EditorStyles.miniLabel);
		}
		DrawCreateAdvancedGroup();

		EditorGUIUtility.labelWidth = prevLabelWidth;

		// ---- 생성 ----
		EditorGUILayout.Space(8f);
		EditorGUI.BeginDisabledGroup(alreadyExists || string.IsNullOrWhiteSpace(_createSceneName));
		if (GUILayout.Button("씬 생성", GUILayout.Height(30f)))
		{
			// OnGUI 안에서 씬을 새로 열거나 모달을 띄우면 GUI 레이아웃 스택이 깨져
			// 'EndLayoutGroup: BeginLayoutGroup must be called first' 가 뜸.
			// delayCall로 GUI가 끝난 뒤에 실행시킴.
			string deferredPath = scenePath;
			EditorApplication.delayCall += () => CreateScene(deferredPath);
		}
		EditorGUI.EndDisabledGroup();

		if (!string.IsNullOrEmpty(_justCreatedScene))
		{
			EditorGUILayout.Space(4f);
			EditorGUILayout.HelpBox($"생성 완료: {_justCreatedScene}\n이 씬이 열려 있으니 바로 맵을 배치할 수 있음.", MessageType.Info);
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("맵 배치 탭으로 이동", GUILayout.Height(22f)))
			{
				_tab = HubTab.Map;
				_justCreatedScene = null;
				GUI.FocusControl(null);
			}
			if (GUILayout.Button("닫기", GUILayout.Width(60f), GUILayout.Height(22f)))
			{
				_justCreatedScene = null;
			}
			EditorGUILayout.EndHorizontal();
		}
	}

	// 접이식 그룹 머리. 접힌 상태에서도 뭐가 들었는지 보이라고 요약을 제목 뒤에 붙임.
	// Foldout은 GUILayoutOption을 못 받아 가로줄에 라벨을 따로 붙이면 밀려나므로 제목 문자열에 합침.
	private static bool CreateGroupHeader(bool open, string title, string summary)
	{
		string label = open || string.IsNullOrEmpty(summary)
			? title
			: $"{title}     ({summary})";
		return EditorGUILayout.Foldout(open, label, true);
	}

	// 고른 종류에 맞는 템플릿을 찾아 값을 채움.
	// 1순위 = 템플릿 에셋이 스스로 밝힌 종류, 2순위 = 에셋 이름에 종류 이름이 들어간 것(기존 에셋 무수정 호환).
	private void SelectTemplateForCategory(SCENE_CATEGORY category)
	{
		_templateAutoFor = category;

		if (_templateChoices == null)
		{
			RefreshTemplateChoices();
		}

		SceneTemplateData match = null;
		for (int i = 0; i < _templateAssets.Length; i++)
		{
			if (_templateAssets[i] != null && _templateAssets[i].category == category)
			{
				match = _templateAssets[i];
				break;
			}
		}

		if (match == null)
		{
			string keyword = category.ToString();
			for (int i = 0; i < _templateAssets.Length; i++)
			{
				if (_templateAssets[i] != null &&
					_templateAssets[i].name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
				{
					match = _templateAssets[i];
					break;
				}
			}
		}

		// 맞는 게 없으면 이전 종류의 프리팹이 남아 엉뚱한 씬이 만들어지므로 비움
		if (match == null)
		{
			_template = null;
			_createPrefabs.Clear();
			_createEmptyObjects.Clear();
			return;
		}

		if (match == _template)
		{
			return;
		}

		_template = match;
		_templateIndex = System.Array.IndexOf(_templateAssets, match);
		ApplyTemplate(_template);
	}

	// 어떤 템플릿이 붙었고 그래서 뭐가 같이 만들어지는지 한 줄로 알림
	private void DrawTemplateNotice()
	{
		if (_template == null)
		{
			HubStyles.ColoredLabel("이 종류에 맞는 템플릿이 없어 빈 씬으로 만들어집니다 — 아래 [고급 · 도구]에서 만들 수 있습니다.",
				HubStyles.Warn, EditorStyles.miniLabel);
			return;
		}

		int spawnCount = _createDefaultSpawnCount + _createFixedSpawnCount;
		HubStyles.ColoredLabel($"템플릿 : {_template.name} — 프리팹 {_createPrefabs.Count}개 · 스폰 포인트 {spawnCount}개가 같이 만들어집니다.",
			HubStyles.Ok, EditorStyles.miniLabel);

		if (_template.notReproducible.Count > 0)
		{
			EditorGUILayout.HelpBox("이 템플릿에 재현 불가 항목이 있음:\n  " +
									string.Join("\n  ", _template.notReproducible) +
									"\n프리팹으로 만든 뒤 다시 캡처하면 사라짐.", MessageType.Warning);
		}
	}

	private void DrawCreateWaveGroup()
	{
		string summary = _createBossWave != null
			? $"웨이브 {_createWaves.Count}개 · 보스 있음"
			: $"웨이브 {_createWaves.Count}개";
		_createExtraWave = CreateGroupHeader(_createExtraWave, "웨이브", summary);
		if (!_createExtraWave)
		{
			return;
		}

		EditorGUILayout.LabelField("여기서 짠 웨이브는 새 씬의 SpawnManager에 그대로 배선됩니다.", EditorStyles.miniLabel);
		DrawWaveListEditor(_createWaves, ref _createBossWave, _createSceneName);
	}

	private void DrawCreateMapGroup()
	{
		// 버튼 번호는 사람이 세는 대로 1번부터 보여주고, 배열에 넣을 때만 0부터로 되돌림
		string summary = _createRegisterToMap ? $"{_createMapButtonIndex + 1}번 버튼에 배정" : "배정 안 함";
		_createExtraMap = CreateGroupHeader(_createExtraMap, "맵 선택 화면 배정", summary);
		if (!_createExtraMap)
		{
			return;
		}

		EditorGUI.indentLevel++;
		_createRegisterToMap = EditorGUILayout.Toggle("맵 선택 버튼에 배정", _createRegisterToMap);
		if (_createRegisterToMap)
		{
			_createMapList = (MapListData)EditorGUILayout.ObjectField("맵 목록(MapListData)", _createMapList, typeof(MapListData), false);
			int shownNumber = Mathf.Max(1, EditorGUILayout.IntField("버튼 번호 (첫 칸이 1번)", _createMapButtonIndex + 1));
			_createMapButtonIndex = shownNumber - 1;
			EditorGUILayout.LabelField("이 씬을 MapListData의 해당 버튼 자리에 배정함(에셋만 수정)", EditorStyles.miniLabel);
		}
		EditorGUI.indentLevel--;
	}

	// 평소엔 볼 일 없는 것들. 템플릿 만들기·저장 위치·등록 여부처럼 종류만으로 정해지는 값들이라 접어둠
	private void DrawCreateAdvancedGroup()
	{
		_createExtraAdvanced = CreateGroupHeader(_createExtraAdvanced, "고급 · 도구", "템플릿 만들기 / 저장 위치 / 등록 여부");
		if (!_createExtraAdvanced)
		{
			return;
		}

		EditorGUI.indentLevel++;

		_createFolder = EditorGUILayout.TextField("저장 폴더", _createFolder);

		EditorGUILayout.Space(4f);
		EditorGUILayout.LabelField("템플릿", EditorStyles.boldLabel);

		if (_templateChoices == null)
		{
			RefreshTemplateChoices();
		}

		EditorGUILayout.BeginHorizontal();
		if (_templateChoices.Length == 0)
		{
			EditorGUILayout.LabelField("템플릿 없음 — 아래에서 하나 만들 것", EditorStyles.miniLabel);
		}
		else
		{
			// 자동 선택이 틀렸을 때만 쓰는 수동 지정. 여기서 고르면 그 종류의 자동 선택보다 우선함
			int picked = EditorGUILayout.Popup("직접 고르기", _templateIndex, _templateChoices);
			if (picked != _templateIndex)
			{
				_templateIndex = picked;
				_template = _templateAssets[_templateIndex];
				ApplyTemplate(_template);
			}
		}
		if (GUILayout.Button("새로고침", GUILayout.Width(72f)))
		{
			RefreshTemplateChoices();
		}
		EditorGUILayout.EndHorizontal();

		// 같은 높이 버튼끼리 한 줄에 — 폭이 남는데 줄을 나누면 빈 공간만 늘어남
		EditorGUILayout.BeginHorizontal();
		EditorGUI.BeginDisabledGroup(_template == null);
		if (GUILayout.Button("템플릿 값 다시 불러오기", GUILayout.Height(22f)))
		{
			ApplyTemplate(_template);
		}
		EditorGUI.EndDisabledGroup();
		if (GUILayout.Button("현재 열린 씬을 템플릿으로 저장", GUILayout.Height(22f)))
		{
			// 저장 대화상자(모달)도 OnGUI 중에 띄우면 레이아웃이 깨지므로 미뤄서 실행함
			EditorApplication.delayCall += CaptureCurrentSceneAsTemplate;
		}
		EditorGUILayout.EndHorizontal();

		_captureChildLayout = EditorGUILayout.Toggle("하위 배치까지 기록", _captureChildLayout);
		EditorGUILayout.LabelField("끄면 컨테이너(MapObject 등)만 만들어짐. 켜면 그 안의 프리팹 위치까지 그대로 복제됨\n" +
								   "새 전투 스테이지는 끄고, 마을씬처럼 배치가 내용인 씬은 켤 것",
								   EditorStyles.wordWrappedMiniLabel);
		EditorGUILayout.LabelField("만든 템플릿에 '어느 씬 종류용인가'를 지정해두면 위에서 그 종류를 고를 때 자동으로 붙음",
								   EditorStyles.wordWrappedMiniLabel);

		EditorGUILayout.Space(4f);
		EditorGUILayout.LabelField("등록", EditorStyles.boldLabel);
		_createRegisterSettings = EditorGUILayout.Toggle("GameManager 씬 설정표에 등록", _createRegisterSettings);
		_createAddToBuild = EditorGUILayout.Toggle("빌드 세팅에 추가", _createAddToBuild);
		EditorGUILayout.LabelField("둘 다 켜두는 게 정상임. 끄면 위에서 고른 종류·배경음이 저장되지 않고,\n" +
								   "빌드 세팅에 없는 씬은 게임에서 로드가 실패함",
								   EditorStyles.wordWrappedMiniLabel);

		EditorGUILayout.Space(4f);
		if (GUILayout.Button("게임매니저 씬 설정 자동 정리 - 필요시 수동확인", GUILayout.Height(22f)))
		{
			EditorApplication.delayCall += FillSceneSettingsWithDefaults;
		}
		EditorGUILayout.LabelField("빠진 SCENE_TYPE 행을 만들고, 모든 행의 씬 종류를 이름으로 추측해 넣음.\n" +
								   "BGM은 건드리지 않음. '속성 직접 지정'은 전부 해제되니 예외 씬은 다시 찍을 것",
								   EditorStyles.wordWrappedMiniLabel);

		EditorGUI.indentLevel--;
	}

	private void CreateScene(string scenePath)
	{
		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
		{
			return;
		}
		if (!AssetDatabase.IsValidFolder(_createFolder))
		{
			Debug.LogError($"[Hub] 폴더가 없음: {_createFolder}");
			return;
		}

		UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		// 1) 프리팹 배치
		SpawnManager spawnManager = null;
		for (int i = 0; i < _createPrefabs.Count; i++)
		{
			if (_createPrefabs[i] == null)
			{
				continue;
			}
			GameObject instance = PrefabUtility.InstantiatePrefab(_createPrefabs[i]) as GameObject;
			if (instance == null)
			{
				continue;
			}
			if (spawnManager == null)
			{
				spawnManager = instance.GetComponentInChildren<SpawnManager>(true);
			}
		}

		// 1-0) 환경 설정 — 새 씬은 기본값으로 시작하므로 템플릿 값으로 덮어씀.
		// 이걸 안 하면 스카이박스(배경)가 기본 하늘로 나옴.
		if (_template != null && _template.skybox != null)
		{
			RenderSettings.skybox = _template.skybox;
			RenderSettings.ambientMode = _template.ambientMode;
			RenderSettings.ambientIntensity = _template.ambientIntensity;
			RenderSettings.ambientSkyColor = _template.ambientSkyColor;
			Debug.Log($"[Hub] 환경 설정 적용: skybox={_template.skybox.name}");
		}

		// 1-1) 템플릿이 '새로 만들기'로 기록한 것들 — 빈 컨테이너와 조명.
		// 프리팹으로 안 묶는 쪽이 나은 것들이라 매번 새로 만듦(씬마다 값이 달라야 정상인 것들).
		for (int i = 0; i < _createEmptyObjects.Count; i++)
		{
			if (string.IsNullOrWhiteSpace(_createEmptyObjects[i]))
			{
				continue;
			}
			new GameObject(_createEmptyObjects[i]);
		}
		if (_createDirectionalLight)
		{
			GameObject lightObject = new GameObject("Directional Light");
			Light light = lightObject.AddComponent<Light>();
			light.type = LightType.Directional;
			lightObject.transform.rotation = Quaternion.Euler(_createLightRotation);
		}

		// 템플릿이 배치까지 들고 있으면 그대로 복원. 부모 경로가 없으면 만들어가며 채움
		if (_template != null)
		{
			for (int i = 0; i < _template.placedObjects.Count; i++)
			{
				SceneTemplateData.PlacedObject placed = _template.placedObjects[i];
				if (placed == null || placed.prefab == null)
				{
					continue;
				}
				GameObject instance = PrefabUtility.InstantiatePrefab(placed.prefab) as GameObject;
				if (instance == null)
				{
					continue;
				}
				Transform parent = ResolvePath(placed.parentPath);
				if (parent != null)
				{
					instance.transform.SetParent(parent, false);
				}
				instance.transform.localPosition = placed.localPosition;
				instance.transform.localRotation = Quaternion.Euler(placed.localEuler);
				instance.transform.localScale = placed.localScale;
			}
			if (_template.placedObjects.Count > 0)
			{
				Debug.Log($"[Hub] 템플릿 배치 {_template.placedObjects.Count}개 복원함.");
			}
		}

		// 1-2) 씬 안에서만 성립하는 참조를 자동 연결.
		// 프리팹은 씬 오브젝트를 참조할 수 없어서 이런 칸은 프리팹 상태에서 항상 비어 있음 —
		// 배치 직후 코드로 채워야 새 씬마다 손으로 잇는 일이 없어짐.
		WireSceneOnlyReferences();

		// 2) 스폰 포인트 생성 — 원형 균등 배치
		Transform[] defaultPoints = CreateSpawnPoints("SpawnPoints_Default", _createDefaultSpawnCount, _createSpawnRingRadius);
		Transform[] fixedPoints = CreateSpawnPoints("SpawnPoints_Fixed", _createFixedSpawnCount, _createSpawnRingRadius * 0.5f);

		// 3) SpawnManager 배선 — 프리팹 인스턴스의 값을 덮어써서 씬 오버라이드로 남김
		if (spawnManager != null)
		{
			Undo.RecordObject(spawnManager, "Hub 씬 생성");
			spawnManager.waves = _createWaves.FindAll(w => w != null).ToArray();
			spawnManager.bossWave = _createBossWave;
			spawnManager.defaultSpawnPoints = defaultPoints;
			spawnManager.fixedSpawnPoints = fixedPoints;
			EditorUtility.SetDirty(spawnManager);
		}
		else if (_createWaves.Count > 0 || _createBossWave != null)
		{
			Debug.LogWarning("[Hub] 배치한 프리팹 중 SpawnManager가 없어 웨이브를 배선하지 못함. " +
							 "SpawnManager 프리팹을 [배치할 프리팹] 목록에 넣을 것.");
		}

		// 4) 저장
		if (!EditorSceneManager.SaveScene(scene, scenePath))
		{
			Debug.LogError($"[Hub] 씬 저장 실패: {scenePath}");
			return;
		}

		// 5) 빌드 세팅
		if (_createAddToBuild)
		{
			AddSceneToBuildSettings(scenePath);
		}

		// 6) GameManager 프리팹의 씬 설정표 — SCENE_TYPE으로 식별되는 씬만 등록 가능함.
		// BGM이 없어도 속성(함선 유지/저장 가능 등) 때문에 행은 만들어야 함.
		if (_createRegisterSettings)
		{
			if (_createSceneType == SCENE_TYPE.UNKNOWN)
			{
				Debug.LogWarning($"[Hub] '{_createSceneName}'이 SCENE_TYPE에 없어 씬 설정 등록을 건너뜀. " +
								 "SCENE_TYPE에 추가한 뒤 다시 등록할 것.");
			}
			else
			{
				RegisterSceneSettings(_createSceneType, _createBgm);
			}
		}

		// 7) 맵 선택 화면 배정 — MapListData 에셋에 씬 이름을 써 넣음(씬/프리팹은 안 건드림)
		if (_createRegisterToMap && _createMapList != null)
		{
			RegisterToMapList(_createMapList, _createMapButtonIndex, _createSceneName);
		}

		AssetDatabase.SaveAssets();
		RefreshSceneRows();
		// 만든 씬이 열린 상태이므로 맵 배치로 바로 이어질 수 있게 표시함
		_justCreatedScene = scenePath;
		Debug.Log($"[Hub] 씬 생성 완료: {scenePath}");
	}

	// MapListData의 버튼 인덱스 자리에 씬 이름을 배정. 배열이 짧으면 늘려서 채움.
	private static void RegisterToMapList(MapListData mapList, int index, string sceneName)
	{
		SerializedObject so = new SerializedObject(mapList);
		SerializedProperty maps = so.FindProperty("maps");
		if (maps == null || !maps.isArray)
		{
			Debug.LogWarning("[Hub] MapListData.maps를 찾지 못함 — 배정을 건너뜀.");
			return;
		}

		// 인덱스까지 배열을 늘림
		while (maps.arraySize <= index)
		{
			maps.InsertArrayElementAtIndex(maps.arraySize);
		}

		SerializedProperty entry = maps.GetArrayElementAtIndex(index);
		SerializedProperty scenePropName = entry.FindPropertyRelative("sceneName");
		SerializedProperty displayName = entry.FindPropertyRelative("displayName");
		if (scenePropName != null)
		{
			scenePropName.stringValue = sceneName;
		}
		// 표시 이름이 비어 있으면 씬 이름으로 채워둠(버튼 라벨 참고용)
		if (displayName != null && string.IsNullOrWhiteSpace(displayName.stringValue))
		{
			displayName.stringValue = sceneName;
		}
		so.ApplyModifiedProperties();
		EditorUtility.SetDirty(mapList);
		Debug.Log($"[Hub] 맵 선택 {index}번 버튼 ← {sceneName} 배정");
	}

	// 컨테이너 하위의 프리팹 배치를 재귀로 기록.
	// 프리팹 인스턴스를 만나면 그 지점에서 멈춤 — 프리팹 내부 구조는 프리팹이 이미 들고 있으므로
	// 더 파고들면 같은 것을 중복 기록하게 됨.
	private static void CaptureChildLayout(Transform parent, string parentPath, SceneTemplateData template)
	{
		for (int i = 0; i < parent.childCount; i++)
		{
			Transform child = parent.GetChild(i);

			if (PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
			{
				GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject);
				if (source == null)
				{
					continue;
				}
				SceneTemplateData.PlacedObject placed = new SceneTemplateData.PlacedObject();
				placed.prefab = source;
				placed.parentPath = parentPath;
				placed.localPosition = child.localPosition;
				placed.localEuler = child.localEulerAngles;
				placed.localScale = child.localScale;
				template.placedObjects.Add(placed);
				continue;
			}

			// 프리팹이 아닌 중간 컨테이너면 경로를 이어가며 더 내려감
			if (child.GetComponents<Component>().Length == 1)
			{
				CaptureChildLayout(child, $"{parentPath}/{child.name}", template);
			}
		}
	}

	// 경로(A/B/C)를 따라 Transform을 찾고 없는 단계는 빈 오브젝트로 만들어 채움
	private static Transform ResolvePath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}

		string[] parts = path.Split('/');
		Transform current = null;
		for (int i = 0; i < parts.Length; i++)
		{
			Transform next = null;
			if (current == null)
			{
				GameObject found = GameObject.Find(parts[i]);
				next = found != null ? found.transform : null;
			}
			else
			{
				next = current.Find(parts[i]);
			}

			if (next == null)
			{
				GameObject created = new GameObject(parts[i]);
				if (current != null)
				{
					created.transform.SetParent(current, false);
				}
				next = created.transform;
			}
			current = next;
		}
		return current;
	}

	// 프로젝트 전체에서 SceneTemplateData 에셋을 찾아 드롭다운 목록을 구성함
	private void RefreshTemplateChoices()
	{
		string[] guids = AssetDatabase.FindAssets("t:SceneTemplateData", new[] { "Assets" });
		List<SceneTemplateData> found = new List<SceneTemplateData>();
		for (int i = 0; i < guids.Length; i++)
		{
			string path = AssetDatabase.GUIDToAssetPath(guids[i]);
			SceneTemplateData asset = AssetDatabase.LoadAssetAtPath<SceneTemplateData>(path);
			if (asset != null)
			{
				found.Add(asset);
			}
		}

		_templateAssets = found.ToArray();
		_templateChoices = new string[_templateAssets.Length];
		for (int i = 0; i < _templateAssets.Length; i++)
		{
			_templateChoices[i] = _templateAssets[i].name;
		}

		// 이전 선택을 유지. 목록에서 사라졌으면 0번으로
		_templateIndex = 0;
		if (_template != null)
		{
			for (int i = 0; i < _templateAssets.Length; i++)
			{
				if (_templateAssets[i] == _template)
				{
					_templateIndex = i;
					break;
				}
			}
		}
	}

	// 템플릿 값을 생성 폼에 채움. 채운 뒤 사용자가 원하는 대로 고칠 수 있게 그냥 복사만 함
	private void ApplyTemplate(SceneTemplateData template)
	{
		if (template == null)
		{
			return;
		}

		_createPrefabs.Clear();
		for (int i = 0; i < template.prefabs.Count; i++)
		{
			if (template.prefabs[i] != null)
			{
				_createPrefabs.Add(template.prefabs[i]);
			}
		}

		_createEmptyObjects.Clear();
		_createEmptyObjects.AddRange(template.emptyObjectNames);

		_createDirectionalLight = template.createDirectionalLight;
		_createLightRotation = template.lightRotation;
		_createDefaultSpawnCount = template.battle.defaultSpawnPointCount;
		_createFixedSpawnCount = template.battle.fixedSpawnPointCount;
		_createSpawnRingRadius = template.battle.spawnRingRadius;

		if (template.bgm != SOUND_TYPE.SFX_NONE)
		{
			_createBgm = template.bgm;
		}
		if (!string.IsNullOrWhiteSpace(template.targetFolder))
		{
			_createFolder = template.targetFolder;
		}

		Debug.Log($"[Hub] 템플릿 불러옴: {template.name} (프리팹 {_createPrefabs.Count}개)");
	}

	// 현재 열린 씬의 루트 구성을 템플릿으로 저장.
	// 프리팹 인스턴스는 원본 프리팹을 기록하고, 순수 빈 오브젝트는 이름만 기록함.
	// 프리팹이 아니면서 컴포넌트를 가진 것은 새 씬에서 재현할 방법이 없으므로 '재현 불가'로 남겨 알림.
	private void CaptureCurrentSceneAsTemplate()
	{
		UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
		if (!scene.IsValid())
		{
			Debug.LogError("[Hub] 열린 씬이 없음.");
			return;
		}

		string path = EditorUtility.SaveFilePanelInProject("템플릿 저장", $"Template_{scene.name}", "asset",
			"이 씬의 구성을 템플릿으로 저장함", "Assets");
		if (string.IsNullOrEmpty(path))
		{
			return;
		}

		SceneTemplateData template = ScriptableObject.CreateInstance<SceneTemplateData>();
		template.description = $"{scene.name} 씬에서 뽑음";
		template.createDirectionalLight = false;
		template.targetFolder = System.IO.Path.GetDirectoryName(scene.path).Replace("\\", "/");

		// 환경 설정은 씬마다 따로 저장되므로 여기서 같이 담아야 새 씬에 배경이 딸려옴
		template.skybox = RenderSettings.skybox;
		template.ambientMode = RenderSettings.ambientMode;
		template.ambientIntensity = RenderSettings.ambientIntensity;
		template.ambientSkyColor = RenderSettings.ambientSkyColor;

		GameObject[] roots = scene.GetRootGameObjects();
		for (int i = 0; i < roots.Length; i++)
		{
			GameObject root = roots[i];

			if (IsExcludedFromTemplate(root))
			{
				continue;
			}

			if (PrefabUtility.IsAnyPrefabInstanceRoot(root))
			{
				GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(root);
				if (source != null && !template.prefabs.Contains(source))
				{
					template.prefabs.Add(source);
				}
				continue;
			}

			// 조명은 프리팹으로 안 묶는 쪽이 나아서 '새로 만들기'로 기록함
			Light light = root.GetComponent<Light>();
			if (light != null && light.type == LightType.Directional)
			{
				template.createDirectionalLight = true;
				template.lightRotation = root.transform.eulerAngles;
				continue;
			}

			// Transform만 있는 순수 컨테이너는 이름만 기록하면 재현됨.
			// 하위에 놓인 프리팹 배치는 별도 옵션 — 새 스테이지는 백지에서 시작하는 게 보통이므로 기본은 끔.
			Component[] components = root.GetComponents<Component>();
			if (components.Length == 1)
			{
				template.emptyObjectNames.Add(root.name);
				if (_captureChildLayout)
				{
					CaptureChildLayout(root.transform, root.name, template);
				}
				continue;
			}

			// 그 외 = 프리팹도 아니고 컴포넌트를 가진 것 → 자동 재현 불가
			List<string> typeNames = new List<string>();
			for (int j = 0; j < components.Length; j++)
			{
				if (components[j] == null)
				{
					typeNames.Add("Missing Script");
					continue;
				}
				if (components[j] is Transform)
				{
					continue;
				}
				typeNames.Add(components[j].GetType().Name);
			}
			template.notReproducible.Add($"{root.name} ({string.Join(", ", typeNames)})");
		}

		// 스폰 포인트 개수는 씬의 SpawnManager에서 그대로 가져옴
		SpawnManager spawner = Object.FindObjectOfType<SpawnManager>(true);
		if (spawner != null)
		{
			template.battle.defaultSpawnPointCount = spawner.defaultSpawnPoints != null ? spawner.defaultSpawnPoints.Length : 0;
			template.battle.fixedSpawnPointCount = spawner.fixedSpawnPoints != null ? spawner.fixedSpawnPoints.Length : 0;
		}

		AssetDatabase.CreateAsset(template, path);
		AssetDatabase.SaveAssets();
		_template = template;
		ApplyTemplate(template);
		EditorGUIUtility.PingObject(template);

		if (template.notReproducible.Count > 0)
		{
			Debug.LogWarning($"[Hub] 템플릿 저장됨 — 다만 재현 불가 항목 {template.notReproducible.Count}개 있음:\n  " +
							 string.Join("\n  ", template.notReproducible));
		}
		else
		{
			Debug.Log($"[Hub] 템플릿 저장 완료: {path} (프리팹 {template.prefabs.Count}개)");
		}
	}

	// 템플릿/생성 대상에서 빼는 오브젝트. 계측용이거나 씬마다 새로 두는 게 맞는 것들.
	private static bool IsExcludedFromTemplate(GameObject go)
	{
		// PerfRecorder는 성능 계측용이라 실제 씬 구성이 아님
		if (go.GetComponent<PerfRecorder>() != null)
		{
			return true;
		}
		return false;
	}

	// 씬 전용 참조 자동 연결.
	// 프리팹은 다른 씬 오브젝트를 참조할 수 없어서 이런 칸은 프리팹 상태에서 항상 비어 있음.
	// 연결 대상이 늘면 여기에 항목을 추가할 것.
	private static void WireSceneOnlyReferences()
	{
		WireWorldBoundaryVignette();
		WireCameraManagerVirtualCamera();
	}

	// CameraManager._virtualCamera ← 씬의 CinemachineVirtualCamera.
	// Awake에 FindObjectOfType 폴백이 있지만, vCam이 여러 개면 엉뚱한 걸 잡으므로 명시로 꽂아둠.
	private static void WireCameraManagerVirtualCamera()
	{
		CameraManager[] managers = Object.FindObjectsOfType<CameraManager>(true);
		if (managers.Length == 0)
		{
			return;
		}

		Cinemachine.CinemachineVirtualCamera[] cams =
			Object.FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>(true);

		for (int i = 0; i < managers.Length; i++)
		{
			SerializedObject so = new SerializedObject(managers[i]);
			SerializedProperty prop = so.FindProperty("_virtualCamera");
			if (prop == null || prop.objectReferenceValue != null)
			{
				continue;
			}
			if (cams.Length == 0)
			{
				Debug.LogWarning("[Hub] 씬에 CinemachineVirtualCamera가 없어 CameraManager._virtualCamera를 못 채움.");
				continue;
			}
			if (cams.Length > 1)
			{
				Debug.LogWarning($"[Hub] CinemachineVirtualCamera가 {cams.Length}개임 — 첫 번째({cams[0].name})를 연결함. 확인할 것.");
			}
			prop.objectReferenceValue = cams[0];
			so.ApplyModifiedProperties();
			Debug.Log($"[Hub] CameraManager._virtualCamera ← {cams[0].name} 자동 연결");
		}
	}

	// WorldBoundary.vignetteImage ← 경계 밖 화면효과용 Image.
	private static void WireWorldBoundaryVignette()
	{
		WorldBoundary[] boundaries = Object.FindObjectsOfType<WorldBoundary>(true);
		if (boundaries.Length == 0)
		{
			return;
		}

		// 화면 전체를 덮는 UI Image를 후보로 봄. 여러 개면 이름에 vignette가 든 것을 우선함
		UnityEngine.UI.Image[] images = Object.FindObjectsOfType<UnityEngine.UI.Image>(true);
		UnityEngine.UI.Image target = null;
		for (int i = 0; i < images.Length; i++)
		{
			if (images[i].name.IndexOf("vignette", System.StringComparison.OrdinalIgnoreCase) >= 0)
			{
				target = images[i];
				break;
			}
		}
		if (target == null && images.Length == 1)
		{
			target = images[0];
		}

		for (int i = 0; i < boundaries.Length; i++)
		{
			SerializedObject so = new SerializedObject(boundaries[i]);
			SerializedProperty prop = so.FindProperty("vignetteImage");
			if (prop == null)
			{
				continue;
			}
			if (prop.objectReferenceValue != null)
			{
				continue;
			}
			if (target == null)
			{
				Debug.LogWarning("[Hub] WorldBoundary의 vignetteImage에 꽂을 Image를 씬에서 못 찾음 — 직접 연결할 것.");
				continue;
			}
			prop.objectReferenceValue = target;
			so.ApplyModifiedProperties();
			Debug.Log($"[Hub] WorldBoundary.vignetteImage ← {target.name} 자동 연결");
		}
	}

	// 씬 파일이 실제로 있는지. AssetDatabase.AssetPathToGUID는 지운 에셋의 GUID를 한동안 계속 돌려주므로
	// 파일 시스템을 직접 본다.
	// 프로젝트에 실제로 있는 씬 파일 이름 모음. SCENE_TYPE에만 남아 있고 씬은 없는 이름을 걸러내는 데 씀.
	private static HashSet<string> CollectExistingSceneNames()
	{
		HashSet<string> names = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
		string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
		for (int i = 0; i < guids.Length; i++)
		{
			string path = AssetDatabase.GUIDToAssetPath(guids[i]);
			if (SceneFileExists(path))
			{
				names.Add(System.IO.Path.GetFileNameWithoutExtension(path));
			}
		}
		return names;
	}

	private static bool SceneFileExists(string assetPath)
	{
		if (string.IsNullOrWhiteSpace(assetPath))
		{
			return false;
		}
		return System.IO.File.Exists(System.IO.Path.GetFullPath(assetPath));
	}

	// SCENE_TYPE에 새 멤버를 추가함.
	// 번호는 5 단위로 띄워 붙이고, GAME_OVER(999)는 끝값이라 계산에서 제외됨.
	private static void AddSceneTypeMember(string name)
	{
		HubEnumEditor.AddMember(typeof(SCENE_TYPE), name, 5, 999);
	}

	// 씬 삭제 — 씬 파일 / 빌드 세팅 / BGM 표 / 맵 선택 배정을 되돌림.
	// SCENE_TYPE enum 멤버는 남김: 참조 코드가 있으면 컴파일이 깨지고,
	// 같은 이름으로 다시 만들 때 그 값을 그대로 재사용할 수 있음.
	private void DeleteScene(string scenePath, SCENE_TYPE sceneType)
	{
		if (!SceneFileExists(scenePath))
		{
			Debug.LogWarning($"[Hub] 없는 씬임: {scenePath}");
			return;
		}

		string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

		string typeNote = sceneType != SCENE_TYPE.UNKNOWN
			? $"\n· GameManager 씬-BGM 표에서 {sceneType} 항목 제거"
			: "";
		bool ok = EditorUtility.DisplayDialog("씬 삭제",
			$"{scenePath}\n\n아래를 되돌립니다.\n· 씬 파일 삭제\n· 빌드 세팅에서 제거{typeNote}" +
			"\n· 맵 선택 목록에서 이 씬을 가리키는 버튼 비움\n\n" +
			"SCENE_TYPE enum 항목은 남습니다(참조 코드가 깨질 수 있어서).\n" +
			"같은 이름으로 다시 만들면 그 값을 그대로 씁니다.\n\n되돌릴 수 없습니다.",
			"삭제", "취소");
		if (!ok)
		{
			return;
		}

		// 지우려는 씬이 열려 있으면 먼저 빈 씬으로 옮겨야 함 — 열린 씬을 지우면 에디터가 불안정해짐
		if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == scenePath)
		{
			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
		}

		RemoveSceneFromBuildSettings(scenePath);
		if (sceneType != SCENE_TYPE.UNKNOWN)
		{
			UnregisterSceneBgm(sceneType);
		}
		UnregisterFromMapLists(sceneName);

		if (!AssetDatabase.DeleteAsset(scenePath))
		{
			Debug.LogError($"[Hub] 씬 파일 삭제 실패: {scenePath}");
			return;
		}

		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		RefreshSceneRows();
		Debug.Log($"[Hub] 씬 삭제 완료: {scenePath}");
	}

	// 모든 MapListData에서 이 씬을 가리키는 칸을 비움.
	// 항목을 지우지 않고 이름만 비우는 이유는 버튼 인덱스가 밀리면 다른 버튼의 배정이 바뀌기 때문임.
	// 이름이 비면 MapSelectorUI가 그 버튼을 잠근다.
	private static void UnregisterFromMapLists(string sceneName)
	{
		if (string.IsNullOrWhiteSpace(sceneName))
		{
			return;
		}

		string[] guids = AssetDatabase.FindAssets("t:MapListData", new[] { "Assets" });
		for (int g = 0; g < guids.Length; g++)
		{
			string path = AssetDatabase.GUIDToAssetPath(guids[g]);
			MapListData mapList = AssetDatabase.LoadAssetAtPath<MapListData>(path);
			if (mapList == null)
			{
				continue;
			}

			SerializedObject so = new SerializedObject(mapList);
			SerializedProperty maps = so.FindProperty("maps");
			if (maps == null || !maps.isArray)
			{
				continue;
			}

			int cleared = 0;
			for (int i = 0; i < maps.arraySize; i++)
			{
				SerializedProperty entry = maps.GetArrayElementAtIndex(i);
				SerializedProperty nameProp = entry.FindPropertyRelative("sceneName");
				if (nameProp == null)
				{
					continue;
				}
				// 씬 이름 비교는 대소문자 무시 — SceneManager.LoadScene이 대소문자를 안 가림
				if (!string.Equals(nameProp.stringValue, sceneName, System.StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				nameProp.stringValue = "";
				SerializedProperty displayProp = entry.FindPropertyRelative("displayName");
				if (displayProp != null && string.Equals(displayProp.stringValue, sceneName, System.StringComparison.OrdinalIgnoreCase))
				{
					displayProp.stringValue = "";
				}
				cleared++;
			}

			if (cleared > 0)
			{
				so.ApplyModifiedProperties();
				EditorUtility.SetDirty(mapList);
				Debug.Log($"[Hub] {mapList.name}에서 '{sceneName}' 배정 {cleared}개 비움.");
			}
		}
	}

	// 경로로 찾아 제거 — 인덱스가 아니라 값 기준이라 등록/삭제 순서가 달라도 문제없음
	private static void RemoveSceneFromBuildSettings(string scenePath)
	{
		List<EditorBuildSettingsScene> kept = new List<EditorBuildSettingsScene>();
		EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
		bool removed = false;
		for (int i = 0; i < current.Length; i++)
		{
			if (current[i].path == scenePath)
			{
				removed = true;
				continue;
			}
			kept.Add(current[i]);
		}
		if (!removed)
		{
			return;
		}
		EditorBuildSettings.scenes = kept.ToArray();
		Debug.Log($"[Hub] 빌드 세팅에서 제거함: {scenePath}");
	}

	// GameManager 프리팹의 씬 설정표에서 해당 씬 항목을 제거.
	// 인덱스가 아니라 scene 값으로 찾고, 뒤에서부터 순회함 —
	// 앞에서부터 지우면 인덱스가 밀려 다음 항목을 건너뜀. 중복 항목이 있어도 전부 지워짐.
	private static void UnregisterSceneBgm(SCENE_TYPE sceneType)
	{
		string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/3.Prefabs" });
		for (int i = 0; i < guids.Length; i++)
		{
			string path = AssetDatabase.GUIDToAssetPath(guids[i]);
			if (!path.EndsWith(".prefab"))
			{
				continue;
			}
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (prefab == null)
			{
				continue;
			}
			GameManager gm = prefab.GetComponentInChildren<GameManager>(true);
			if (gm == null)
			{
				continue;
			}

			SerializedObject so = new SerializedObject(gm);
			SerializedProperty list = so.FindProperty("_sceneSettings");
			if (list == null || !list.isArray)
			{
				return;
			}

			int removed = 0;
			for (int j = list.arraySize - 1; j >= 0; j--)
			{
				SerializedProperty sceneProp = list.GetArrayElementAtIndex(j).FindPropertyRelative("scene");
				if (sceneProp != null && sceneProp.intValue == (int)sceneType)
				{
					list.DeleteArrayElementAtIndex(j);
					removed++;
				}
			}

			if (removed > 0)
			{
				so.ApplyModifiedProperties();
				EditorUtility.SetDirty(prefab);
				Debug.Log($"[Hub] 씬-BGM 표에서 {sceneType} 항목 {removed}개 제거함.");
			}
			return;
		}
	}

	private static Transform[] CreateSpawnPoints(string groupName, int count, float radius)
	{
		if (count <= 0)
		{
			return new Transform[0];
		}

		GameObject group = new GameObject(groupName);
		Transform[] result = new Transform[count];
		for (int i = 0; i < count; i++)
		{
			float angle = (360f / count) * i * Mathf.Deg2Rad;
			Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
			GameObject point = new GameObject($"{groupName}_{i}");
			point.transform.SetParent(group.transform, false);
			point.transform.position = pos;
			result[i] = point.transform;
		}
		return result;
	}

	// GameManager 프리팹의 씬 설정표에 한 줄을 추가하거나 갱신함.
	// 프리팹 하나에 모여 있는 프로젝트 전역 표라, 씬을 만들 때 같이 채워야 조회가 됨.
	private void RegisterSceneSettings(SCENE_TYPE sceneType, SOUND_TYPE bgm)
	{
		string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/3.Prefabs" });
		for (int i = 0; i < guids.Length; i++)
		{
			string path = AssetDatabase.GUIDToAssetPath(guids[i]);
			if (!path.EndsWith(".prefab"))
			{
				continue;
			}
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (prefab == null)
			{
				continue;
			}
			GameManager gm = prefab.GetComponentInChildren<GameManager>(true);
			if (gm == null)
			{
				continue;
			}

			SerializedObject so = new SerializedObject(gm);
			SerializedProperty list = so.FindProperty("_sceneSettings");
			if (list == null || !list.isArray)
			{
				Debug.LogWarning("[Hub] GameManager에서 씬 설정표를 찾지 못함 — 등록을 건너뜀.");
				return;
			}

			// 같은 씬 항목이 이미 있으면 값만 바꿈
			for (int j = 0; j < list.arraySize; j++)
			{
				SerializedProperty entry = list.GetArrayElementAtIndex(j);
				SerializedProperty sceneProp = entry.FindPropertyRelative("scene");
				if (sceneProp != null && sceneProp.intValue == (int)sceneType)
				{
					WriteSceneSettings(entry, sceneType, bgm);
					so.ApplyModifiedProperties();
					EditorUtility.SetDirty(prefab);
					Debug.Log($"[Hub] 씬 설정 갱신: {sceneType} (BGM {bgm})");
					return;
				}
			}

			list.InsertArrayElementAtIndex(list.arraySize);
			WriteSceneSettings(list.GetArrayElementAtIndex(list.arraySize - 1), sceneType, bgm);
			so.ApplyModifiedProperties();
			EditorUtility.SetDirty(prefab);
			Debug.Log($"[Hub] 씬 설정 등록: {sceneType} (BGM {bgm})");
			return;
		}

		Debug.LogWarning("[Hub] GameManager 프리팹을 찾지 못해 씬 설정 등록을 건너뜀.");
	}

	// 이름으로 씬 종류를 추측함. 어디까지나 표를 처음 채울 때 쓰는 추측이고,
	// 최종값은 사람이 인스펙터에서 고른 카테고리임. 틀리면 드롭다운만 고치면 됨.
	private static SCENE_CATEGORY GuessCategory(SCENE_TYPE type)
	{
		string name = type.ToString();
		if (name.StartsWith("STAGE", System.StringComparison.OrdinalIgnoreCase))
		{
			return SCENE_CATEGORY.BATTLE;
		}
		// BASE_STATION_1F 처럼 뒤에 붙여 늘어나도 코드를 안 고치도록 접두사로 판정함.
		if (name.StartsWith("BASE_STATION", System.StringComparison.OrdinalIgnoreCase))
		{
			return SCENE_CATEGORY.STATION;
		}
		if (name.StartsWith("BASE_HANGAR", System.StringComparison.OrdinalIgnoreCase))
		{
			return SCENE_CATEGORY.HANGAR;
		}
		switch (type)
		{
			case SCENE_TYPE.MAP_SELECT:
			case SCENE_TYPE.MULTIPLAYER:
				return SCENE_CATEGORY.TRANSIT;
			case SCENE_TYPE.LOADING_SEQUENCE:
				return SCENE_CATEGORY.LOADING;
			default:
				return SCENE_CATEGORY.OTHER;
		}
	}

	// 모든 SCENE_TYPE에 대해 행을 보장하고 씬 종류를 추측해 넣음.
	// BGM 열은 손대지 않음 — 그건 사람이 고른 값이라 추측할 근거가 없음.
	private static void FillSceneSettingsWithDefaults()
	{
		SerializedObject so = FindGameManagerSerialized(out GameObject prefab);
		if (so == null)
		{
			Debug.LogWarning("[Hub] GameManager 프리팹을 찾지 못해 씬 설정표를 채우지 못함.");
			return;
		}

		SerializedProperty list = so.FindProperty("_sceneSettings");
		if (list == null || !list.isArray)
		{
			Debug.LogWarning("[Hub] GameManager에서 씬 설정표를 찾지 못함.");
			return;
		}

		HashSet<string> sceneNames = CollectExistingSceneNames();

		// SCENE_TYPE에서 사라진 값(주석 처리 등)이 남아 있으면 인스펙터에서 깨져 보임
		int removed = 0;
		for (int i = list.arraySize - 1; i >= 0; i--)
		{
			SerializedProperty sceneProp = list.GetArrayElementAtIndex(i).FindPropertyRelative("scene");
			if (sceneProp == null)
			{
				continue;
			}
			bool definedType = System.Enum.IsDefined(typeof(SCENE_TYPE), sceneProp.intValue);
			// 씬 파일이 없는 행은 지움. 씬을 지워도 SCENE_TYPE 멤버는 남기는 정책이라,
			// enum만 보고 채우면 없는 씬 행이 계속 생김.
			bool hasSceneFile = definedType
				&& sceneNames.Contains(((SCENE_TYPE)sceneProp.intValue).ToString());
			if (!definedType || !hasSceneFile)
			{
				list.DeleteArrayElementAtIndex(i);
				removed++;
			}
		}

		int added = 0;
		int updated = 0;
		foreach (SCENE_TYPE type in System.Enum.GetValues(typeof(SCENE_TYPE)))
		{
			if (type == SCENE_TYPE.UNKNOWN || !sceneNames.Contains(type.ToString()))
			{
				continue;
			}

			SerializedProperty entry = FindEntry(list, type);
			if (entry == null)
			{
				list.InsertArrayElementAtIndex(list.arraySize);
				entry = list.GetArrayElementAtIndex(list.arraySize - 1);
				SetInt(entry, "scene", (int)type);
				SetInt(entry, "bgm", (int)SOUND_TYPE.SFX_NONE);
				added++;
			}
			else
			{
				updated++;
			}

			// 카테고리만 정하고 개별 체크박스는 끔 — 속성은 런타임이 카테고리에서 풀어 씀.
			SetInt(entry, "category", (int)GuessCategory(type));
			SetBool(entry, "overrideFlags", false);
			SetBool(entry, "keepsPlayerShip", false);
			SetBool(entry, "canSave", false);
			SetBool(entry, "isBattleScene", false);
			SetBool(entry, "isStationScene", false);
			SetBool(entry, "shipControlDisabled", false);
			SetBool(entry, "shipHidden", false);
			SetBool(entry, "otherShipsHidden", false);
		}

		so.ApplyModifiedProperties();
		EditorUtility.SetDirty(prefab);
		AssetDatabase.SaveAssets();
		Debug.Log($"[Hub] 씬 설정표 정리 — 추가 {added} / 갱신 {updated} / 삭제 {removed}");
	}

	private static SerializedProperty FindEntry(SerializedProperty list, SCENE_TYPE type)
	{
		for (int i = 0; i < list.arraySize; i++)
		{
			SerializedProperty entry = list.GetArrayElementAtIndex(i);
			SerializedProperty sceneProp = entry.FindPropertyRelative("scene");
			if (sceneProp != null && sceneProp.intValue == (int)type)
			{
				return entry;
			}
		}
		return null;
	}

	// GameManager가 붙은 프리팹을 찾아 SerializedObject로 돌려줌. 못 찾으면 null.
	private static SerializedObject FindGameManagerSerialized(out GameObject prefab)
	{
		prefab = null;
		string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/3.Prefabs" });
		for (int i = 0; i < guids.Length; i++)
		{
			string path = AssetDatabase.GUIDToAssetPath(guids[i]);
			if (!path.EndsWith(".prefab"))
			{
				continue;
			}
			GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (candidate == null)
			{
				continue;
			}
			GameManager gm = candidate.GetComponentInChildren<GameManager>(true);
			if (gm == null)
			{
				continue;
			}
			prefab = candidate;
			return new SerializedObject(gm);
		}
		return null;
	}

	// 표 한 줄에 값 쓰기. 필드가 없으면(구버전 GameManager) 조용히 건너뜀.
	private void WriteSceneSettings(SerializedProperty entry, SCENE_TYPE sceneType, SOUND_TYPE bgm)
	{
		SetInt(entry, "scene", (int)sceneType);
		SetInt(entry, "bgm", (int)bgm);
		SetInt(entry, "category", (int)_createCategory);
		SetBool(entry, "overrideFlags", _createOverrideFlags);
		for (int i = 0; i < SettingFlagNames.Length; i++)
		{
			SetBool(entry, SettingFlagNames[i], _createFlags[i]);
		}
	}

	private static void SetInt(SerializedProperty entry, string name, int value)
	{
		SerializedProperty prop = entry.FindPropertyRelative(name);
		if (prop != null)
		{
			prop.intValue = value;
		}
	}

	private static void SetBool(SerializedProperty entry, string name, bool value)
	{
		SerializedProperty prop = entry.FindPropertyRelative(name);
		if (prop != null)
		{
			prop.boolValue = value;
		}
	}
}
