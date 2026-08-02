using UnityEditor;
using UnityEngine;

// =====================================================================
// ProjectHubWindow — 씬 상세.
//
// 흐름도에서 카드를 고르면 그 씬의 설정을 사람 말로 보여주고 고치게 함.
// 개발 용어(category / overrideFlags / keepsPlayerShip)는 화면에 안 띄우고,
// 체크박스 묶음은 '고급'으로 접어둠 — 평소엔 씬 종류와 배경음만 고르면 됨.
// =====================================================================
public partial class ProjectHubWindow
{
	// 씬 관리 탭에서 설정 화면들이 창 폭에서 차지할 비율. 여기 숫자만 고치면 전부 같이 줄어듦.
	// 흐름도는 여기 안 걸림 — 씬이 늘면 옆으로 길어져야 하므로 자기 좌우 스크롤을 씀
	private const float SceneContentWidthRatio = 0.65f;

	// 너무 좁으면 씬 종류 버튼 세 칸의 글자가 잘려서 아래쪽은 보장해 둠
	private float SceneContentWidth => Mathf.Max(420f, position.width * SceneContentWidthRatio);

	private bool _detailAdvancedOpen;
	// 상세 패널 모드. false=선택한 씬 수정, true=새 씬 만들기
	private bool _detailCreateMode;

	// 편집 중인 값(초안). [수정하기]를 눌러야 실제 표에 들어감 —
	// 버튼을 잘못 눌러도 바로 반영되지 않게 하기 위함.
	private SCENE_TYPE _draftFor = SCENE_TYPE.UNKNOWN;
	private SCENE_CATEGORY _draftCategory;
	private SOUND_TYPE _draftBgm;
	private bool _draftOverride;
	private bool[] _draftFlags = new bool[0];

	// 씬 종류 고르기용. SCENE_CATEGORY 순서와 무관하게 화면에 보일 순서로 둠.
	private static readonly SCENE_CATEGORY[] CategoryChoices =
	{
		SCENE_CATEGORY.STATION,
		SCENE_CATEGORY.HANGAR,
		SCENE_CATEGORY.BATTLE,
		SCENE_CATEGORY.TRANSIT,
		SCENE_CATEGORY.LOADING,
		SCENE_CATEGORY.OTHER,
	};

	private static string CategoryLabel(SCENE_CATEGORY category)
	{
		switch (category)
		{
			case SCENE_CATEGORY.STATION: return "마을 · 거점";
			case SCENE_CATEGORY.HANGAR:  return "격납고";
			case SCENE_CATEGORY.BATTLE:  return "전투 스테이지";
			case SCENE_CATEGORY.TRANSIT: return "거쳐가는 씬(매칭룸,맵선택등)";
			case SCENE_CATEGORY.LOADING: return "로딩";
			default:                     return "메뉴 · 기타";
		}
	}

	// 그 종류를 고르면 실제로 어떻게 되는지 사람 말로.
	private static string CategoryPlain(SCENE_CATEGORY category)
	{
		switch (category)
		{
			case SCENE_CATEGORY.STATION:
				return "함선 없이 걸어다니는 곳. 여기서만 저장 가능.";
			case SCENE_CATEGORY.HANGAR:
				return "파츠 변경 혹은 수리등을 하는 격납고 씬";
			case SCENE_CATEGORY.BATTLE:
				return "실제 전투용 씬. Stage등";
			case SCENE_CATEGORY.TRANSIT:
				return "멀티나 맵선택등 거쳐가는 경유씬.";
			case SCENE_CATEGORY.LOADING:
				return "로딩 씬";
			default:
				return "메뉴 화면.";
		}
	}

	private void DrawSceneDetail()
	{
		EditorGUILayout.BeginVertical(HubStyles.Card);

		// ---- 모드 전환 ----
		EditorGUILayout.BeginHorizontal();
		int mode = GUILayout.Toolbar(_detailCreateMode ? 1 : 0,
			new[] { "선택한 씬 수정", "새 씬 만들기" }, GUILayout.Height(22f), GUILayout.Width(260f));
		if ((mode == 1) != _detailCreateMode)
		{
			_detailCreateMode = mode == 1;
			GUI.FocusControl(null);
		}
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();

		HubStyles.Separator(4f);

		if (_detailCreateMode)
		{
			DrawSceneCreateSection();
			EditorGUILayout.EndVertical();
			HubStyles.Separator();
			return;
		}

		// 고른 씬이 없을 때와 그 씬 파일이 사라졌을 때를 한 갈래로 묶음.
		// 예전엔 여기서 _flowSelected를 UNKNOWN으로 바꿨는데, 그리는 도중에 상태를 바꾸면
		// Layout 패스와 Repaint 패스가 서로 다른 갈래를 타서 컨트롤 수가 어긋남
		// (씬을 지우면 나던 'Getting control N's position...' 예외가 이것). 정리는 표를 새로 읽을 때 함.
		SceneRow row = _flowSelected != SCENE_TYPE.UNKNOWN ? FindRow(_flowSelected) : null;
		if (row == null || string.IsNullOrEmpty(row.assetPath))
		{
			HubStyles.ColoredLabel("위 흐름도에서 씬을 고르면 여기서 설정을 바꿀 수 있습니다.",
				HubStyles.Muted, HubStyles.IssueText);
			EditorGUILayout.EndVertical();
			HubStyles.Separator();
			return;
		}

		string display = SceneDisplayNames.TryGetValue(_flowSelected, out string d)
			? d
			: _flowSelected.ToString();

		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField($"선택된 씬 : {display}  ({_flowSelected})", HubStyles.SectionTitle);
		GUILayout.FlexibleSpace();
		if (GUILayout.Button("씬 열기", GUILayout.Width(70f)))
		{
			string path = row.assetPath;
			EditorApplication.delayCall += () => OpenScene(path);
		}
		if (GUILayout.Button("선택 해제", GUILayout.Width(70f)))
		{
			_flowSelected = SCENE_TYPE.UNKNOWN;
		}
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.LabelField(row.assetPath, EditorStyles.miniLabel);

		DrawSceneSettingsBody(_flowSelected);

		EditorGUILayout.EndVertical();
		HubStyles.Separator();
	}

	// 씬 설정표 한 줄을 사람 말로 편집. 표의 [설정] 버튼과 흐름도 상세가 같이 씀.
	private void DrawSceneSettingsBody(SCENE_TYPE sceneType)
	{
		SerializedObject so = FindGameManagerSerialized(out GameObject prefab);
		SerializedProperty list = so != null ? so.FindProperty("_sceneSettings") : null;
		if (list == null || !list.isArray)
		{
			EditorGUILayout.HelpBox("GameManager 프리팹의 씬 설정표를 찾지 못했습니다.", MessageType.Warning);
			return;
		}

		SerializedProperty entry = FindEntry(list, sceneType);
		if (entry == null)
		{
			EditorGUILayout.HelpBox("이 씬의 설정이 아직 등록되지 않았습니다.", MessageType.Info);
			if (GUILayout.Button("설정 추가", GUILayout.Height(22f)))
			{
				SCENE_TYPE deferred = sceneType;
				EditorApplication.delayCall += () => AddSceneSettingsRow(deferred);
			}
			return;
		}

		// 다른 씬을 고르면 그 씬 값으로 초안을 새로 읽음.
		if (_draftFor != sceneType)
		{
			LoadDraft(entry, sceneType);
		}

		DrawCategoryBgmAdvanced(ref _draftCategory, ref _draftBgm, ref _draftOverride, _draftFlags);

		// ---- 적용 / 되돌리기 ----
		GUILayout.Space(6f);
		bool dirty = IsDraftDirty(entry);

		EditorGUILayout.BeginHorizontal();
		EditorGUI.BeginDisabledGroup(!dirty);
		if (GUILayout.Button("씬 수정하기", GUILayout.Height(24f)))
		{
			ApplyDraft(entry, so, prefab);
		}
		if (GUILayout.Button("되돌리기", GUILayout.Height(24f), GUILayout.Width(90f)))
		{
			LoadDraft(entry, sceneType);
			GUI.FocusControl(null);
		}
		EditorGUI.EndDisabledGroup();
		EditorGUILayout.EndHorizontal();

		if (dirty)
		{
			HubStyles.ColoredLabel("바뀐 내용이 있습니다. [씬 수정하기]를 눌러야 저장됩니다.",
				HubStyles.Warn, EditorStyles.miniLabel);
		}

		// 웨이브는 전투 스테이지에만 있음. 초안이 아니라 표에 적용된 종류를 기준으로 판단해야
		// 종류만 눌러보고 아직 적용 안 한 상태에서 엉뚱하게 웨이브 칸이 뜨지 않음
		SCENE_CATEGORY appliedCategory = (SCENE_CATEGORY)entry.FindPropertyRelative("category").intValue;
		if (appliedCategory == SCENE_CATEGORY.BATTLE)
		{
			HubStyles.Separator(4f);
			EditorGUILayout.LabelField("웨이브", HubStyles.SectionTitle);
			DrawSceneWaveSection(sceneType);
		}
	}

	// 씬 종류 → 배경음 → 고급 순서. 씬 수정과 새 씬 만들기가 같은 모양이 되도록 공용으로 씀.
	// 값을 필드로 직접 안 받고 ref로 받는 이유 = 수정 쪽은 초안(_draft*), 생성 쪽은 _create* 로 담는 곳이 달라서임.
	private void DrawCategoryBgmAdvanced(ref SCENE_CATEGORY category, ref SOUND_TYPE bgm,
		ref bool overrideFlags, bool[] flags)
	{
		// ---- 씬 종류 ----
		EditorGUILayout.LabelField("이 씬의 종류를 선택", EditorStyles.boldLabel);

		int currentIndex = System.Array.IndexOf(CategoryChoices, category);
		if (currentIndex < 0)
		{
			currentIndex = CategoryChoices.Length - 1;
		}

		string[] labels = new string[CategoryChoices.Length];
		for (int i = 0; i < CategoryChoices.Length; i++)
		{
			labels[i] = CategoryLabel(CategoryChoices[i]);
		}

		// 폭은 바깥 컨테이너(SceneContentWidth)가 이미 잡아줌 — 여기서 또 지정하면 두 군데를 고쳐야 함
		int pickedIndex = GUILayout.SelectionGrid(currentIndex, labels, 3, GUILayout.Height(44f));
		category = CategoryChoices[Mathf.Clamp(pickedIndex, 0, CategoryChoices.Length - 1)];
		HubStyles.ColoredLabel(CategoryPlain(category), HubStyles.Muted, HubStyles.IssueText);

		GUILayout.Space(4f);

		// ---- 배경음 ----
		bgm = (SOUND_TYPE)EditorGUILayout.EnumPopup("배경음", bgm);
		if (bgm == SOUND_TYPE.SFX_NONE)
		{
			HubStyles.ColoredLabel("배경음 없음 — 이전 씬 음악이 그대로 이어집니다.",
				HubStyles.Warn, EditorStyles.miniLabel);
		}

		// ---- 고급 ----
		GUILayout.Space(4f);
		_detailAdvancedOpen = EditorGUILayout.Foldout(_detailAdvancedOpen,
			"고급 — 직접 설정 (평소엔 건드릴 필요 없음)", true);

		if (_detailAdvancedOpen)
		{
			EditorGUI.indentLevel++;
			overrideFlags = EditorGUILayout.Toggle("위 종류 대신 직접 정하기", overrideFlags);

			EditorGUI.BeginDisabledGroup(!overrideFlags);
			for (int i = 0; i < flags.Length; i++)
			{
				flags[i] = EditorGUILayout.Toggle(SettingFlagLabels[i], flags[i]);
			}
			EditorGUI.EndDisabledGroup();
			EditorGUI.indentLevel--;
		}
	}

	private void LoadDraft(SerializedProperty entry, SCENE_TYPE sceneType)
	{
		_draftFor = sceneType;
		_draftCategory = (SCENE_CATEGORY)entry.FindPropertyRelative("category").intValue;
		_draftBgm = (SOUND_TYPE)entry.FindPropertyRelative("bgm").intValue;
		_draftOverride = entry.FindPropertyRelative("overrideFlags").boolValue;

		_draftFlags = new bool[SettingFlagNames.Length];
		for (int i = 0; i < SettingFlagNames.Length; i++)
		{
			SerializedProperty flag = entry.FindPropertyRelative(SettingFlagNames[i]);
			_draftFlags[i] = flag != null && flag.boolValue;
		}
	}

	private bool IsDraftDirty(SerializedProperty entry)
	{
		if (entry.FindPropertyRelative("category").intValue != (int)_draftCategory) return true;
		if (entry.FindPropertyRelative("bgm").intValue != (int)_draftBgm) return true;
		if (entry.FindPropertyRelative("overrideFlags").boolValue != _draftOverride) return true;

		for (int i = 0; i < _draftFlags.Length; i++)
		{
			SerializedProperty flag = entry.FindPropertyRelative(SettingFlagNames[i]);
			if (flag != null && flag.boolValue != _draftFlags[i])
			{
				return true;
			}
		}
		return false;
	}

	private void ApplyDraft(SerializedProperty entry, SerializedObject so, GameObject prefab)
	{
		entry.FindPropertyRelative("category").intValue = (int)_draftCategory;
		entry.FindPropertyRelative("bgm").intValue = (int)_draftBgm;
		entry.FindPropertyRelative("overrideFlags").boolValue = _draftOverride;

		for (int i = 0; i < _draftFlags.Length; i++)
		{
			SerializedProperty flag = entry.FindPropertyRelative(SettingFlagNames[i]);
			if (flag != null)
			{
				flag.boolValue = _draftFlags[i];
			}
		}

		so.ApplyModifiedProperties();
		EditorUtility.SetDirty(prefab);
		AssetDatabase.SaveAssets();
		GUI.FocusControl(null);
	}
}
