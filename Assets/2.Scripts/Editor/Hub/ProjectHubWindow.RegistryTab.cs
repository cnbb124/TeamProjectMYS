using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================
// ProjectHubWindow — 등록소 탭.
//
// SoundManager / VFXManager / PoolManager 처럼 "enum 값마다 프리팹/클립을 등록해 두는" 매니저를
// 한 창에서 관리함. 각 매니저는 배열에 등록된 항목만 재생/스폰하므로, enum에는 있는데 배열에
// 등록이 빠진 값은 런타임에 조용히 실패함(풀 없음 로그). 그 '누락'을 눈으로 잡는 게 이 탭의 핵심.
//
// 등록 항목 추가는 매니저 컴포넌트의 직렬화 배열에 직접 씀(프리팹 수정).
// enum 값·필드명이 매니저마다 달라서 RegistryTarget으로 추상화함 — 새 매니저를 붙이려면
// _targets에 한 줄 추가하면 됨.
// =====================================================================
public partial class ProjectHubWindow
{
	// 한 매니저의 '등록 배열'을 다루기 위한 명세.
	private class RegistryTarget
	{
		public string label;            // 탭에 표시할 이름
		public System.Type managerType; // 컴포넌트 타입 (SoundManager 등)
		public string arrayFieldName;   // 등록 배열 필드명 (_soundList 등)
		public string enumFieldName;    // 배열 원소 안의 enum 필드명 (type/effectType/poolType)
		public System.Type enumType;    // 그 enum 타입 (SOUND_TYPE 등)
		public string prefabFieldName;  // 원소 안의 단일 오브젝트 참조 필드명(프리팹 등). 없으면 null
		public System.Type prefabAssetType = typeof(GameObject);  // 그 필드가 받는 에셋 타입
		// 원소 안의 '에셋 배열' 필드명(사운드 clips[] 등). 없으면 null.
		// 단일 필드와 달리 여러 개를 넣을 수 있어서 별도로 다룸.
		public string arrayAssetFieldName;
		public System.Type arrayAssetType = typeof(Object);

		// 새 enum 항목을 만들 때 쓰는 규칙
		public string namePrefix;       // 이름 앞에 강제로 붙일 접두사 (SFX_ / VFX_ 등). null이면 안 붙임
		public int numberStep = 1;      // 번호 간격
		public int sentinelThreshold = int.MaxValue;  // 이 값 이상은 '끝값'으로 보고 번호 계산 제외

		// 런타임에 찾아 캐싱
		public Component managerComponent;
		public string prefabPath;
		public bool foldout = true;
	}

	private List<RegistryTarget> _targets;
	private Vector2 _registryScroll;

	// 인라인 추가 입력값(타겟별)
	private readonly Dictionary<string, int> _addEnumValue = new Dictionary<string, int>();
	private readonly Dictionary<string, Object> _addAsset = new Dictionary<string, Object>();
	// 배열형 에셋(사운드 clips[])은 여러 개를 담을 수 있어 리스트로 모아둔 뒤 한 번에 등록함
	private readonly Dictionary<string, List<Object>> _addAssetList = new Dictionary<string, List<Object>>();
	// 등록된 항목 중 지금 펼쳐서 편집 중인 인덱스(타겟별). -1이면 접힘
	private readonly Dictionary<string, int> _editIndex = new Dictionary<string, int>();
	// enum에 새로 만들 종류의 이름 입력값(타겟별)
	private readonly Dictionary<string, string> _newTypeName = new Dictionary<string, string>();

	private void EnsureRegistryTargets()
	{
		if (_targets != null)
		{
			return;
		}

		_targets = new List<RegistryTarget>
		{
			new RegistryTarget
			{
				label = "사운드 (SoundManager)",
				managerType = typeof(SoundManager),
				arrayFieldName = "_soundList",
				enumFieldName = "type",
				enumType = typeof(SOUND_TYPE),
				prefabFieldName = null,               // 사운드는 단일 참조가 없고 clips[] 배열을 씀
				arrayAssetFieldName = "clips",
				arrayAssetType = typeof(AudioClip),
				// SFX_NONE = 9999가 끝값이라 번호 계산에서 제외함
				namePrefix = "SFX_",
				sentinelThreshold = 9999,
			},
			new RegistryTarget
			{
				label = "이펙트 (VFXManager)",
				managerType = typeof(VFXManager),
				arrayFieldName = "vfxConfigs",
				enumFieldName = "effectType",
				enumType = typeof(EFFECT_TYPE),
				prefabFieldName = "prefab",
				namePrefix = "VFX_",
				sentinelThreshold = 9999,
			},
			new RegistryTarget
			{
				label = "풀 (PoolManager)",
				managerType = typeof(PoolManager),
				arrayFieldName = "poolConfigs",
				enumFieldName = "poolType",
				enumType = typeof(POOL_TYPE),
				prefabFieldName = "prefab",
				namePrefix = null,   // POOL_TYPE은 접두사 규칙이 여러 갈래(PROJECTILE_/ENEMY_/ITEM_)라 강제하지 않음
			},
		};

		for (int i = 0; i < _targets.Count; i++)
		{
			LocateManager(_targets[i]);
		}
	}

	// 매니저 컴포넌트가 붙은 프리팹을 찾음. 씬이 아니라 프리팹을 기준으로 함(등록은 프리팹에 저장돼야 영구적).
	private static void LocateManager(RegistryTarget target)
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
			Component found = prefab.GetComponentInChildren(target.managerType, true);
			if (found != null)
			{
				target.managerComponent = found;
				target.prefabPath = path;
				return;
			}
		}
		target.managerComponent = null;
		target.prefabPath = null;
	}

	private void DrawRegistryTab()
	{
		EnsureRegistryTargets();

		EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
		if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(64f)))
		{
			_targets = null;
			EnsureRegistryTargets();
		}
		EditorGUILayout.LabelField("각 매니저의 등록 현황 — enum엔 있는데 매니저에 등록하지 않은 미사용or누락 목록 확인가능", EditorStyles.miniLabel);
		EditorGUILayout.EndHorizontal();

		// 라벨 칸이 기본값(약 150)이면 '종류'/'클립 0' 같은 라벨 뒤 컨트롤이 좁아져서 조금 넓힘.
		// 전역 상태이므로 탭이 끝나면 되돌림.
		float prevLabelWidth = EditorGUIUtility.labelWidth;
		EditorGUIUtility.labelWidth = 160f;

		// 이 탭은 매니저의 직렬화 필드를 그대로 그리므로, 필드에 붙은 리치 텍스트 태그가
		// 문자로 노출되지 않게 탭 전체를 감싼다.
		using (new RichTextScope(true))
		{
			_registryScroll = EditorGUILayout.BeginScrollView(_registryScroll);
			for (int i = 0; i < _targets.Count; i++)
			{
				DrawRegistryTarget(_targets[i]);
				EditorGUILayout.Space(6f);
			}
			EditorGUILayout.EndScrollView();
		}

		EditorGUIUtility.labelWidth = prevLabelWidth;
	}

	private void DrawRegistryTarget(RegistryTarget target)
	{
		EditorGUILayout.BeginVertical(EditorStyles.helpBox);

		target.foldout = EditorGUILayout.Foldout(target.foldout, target.label, true, EditorStyles.foldoutHeader);
		if (!target.foldout)
		{
			EditorGUILayout.EndVertical();
			return;
		}

		if (target.managerComponent == null)
		{
			EditorGUILayout.HelpBox($"{target.managerType.Name}가 붙은 프리팹을 3.Prefabs에서 찾지 못함.", MessageType.Warning);
			EditorGUILayout.EndVertical();
			return;
		}

		EditorGUILayout.LabelField(target.prefabPath, EditorStyles.miniLabel);

		SerializedObject so = new SerializedObject(target.managerComponent);
		SerializedProperty array = so.FindProperty(target.arrayFieldName);
		if (array == null || !array.isArray)
		{
			EditorGUILayout.HelpBox($"배열 필드 '{target.arrayFieldName}'을 찾지 못함.", MessageType.Error);
			EditorGUILayout.EndVertical();
			return;
		}

		// 등록된 enum 값 수집
		HashSet<int> registered = new HashSet<int>();
		for (int i = 0; i < array.arraySize; i++)
		{
			SerializedProperty enumProp = array.GetArrayElementAtIndex(i).FindPropertyRelative(target.enumFieldName);
			if (enumProp != null)
			{
				registered.Add(enumProp.intValue);
			}
		}

		// 누락 목록 = enum 전체 - 등록됨 (센티넬성 값은 제외)
		List<int> missing = new List<int>();
		foreach (object v in System.Enum.GetValues(target.enumType))
		{
			int iv = (int)v;
			if (IsSentinelEnumValue(target.enumType, iv))
			{
				continue;
			}
			if (!registered.Contains(iv))
			{
				missing.Add(iv);
			}
		}

		// 요약 + 인스펙터 열기를 한 줄에
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField($"등록 {array.arraySize}개 / 미사용or누락 {missing.Count}개",
			missing.Count > 0 ? EditorStyles.boldLabel : EditorStyles.label);
		if (GUILayout.Button("인스펙터 열기", GUILayout.Width(110f)))
		{
			Selection.activeObject = target.managerComponent;
			EditorGUIUtility.PingObject(target.managerComponent);
		}
		EditorGUILayout.EndHorizontal();

		// 누락 목록 — 폭이 남으니 한 줄에 여러 개를 흘려 담음(FlexibleSpace로 줄바꿈 대체)
		if (missing.Count > 0)
		{
			SectionHeader("미사용or누락된 값 (클릭하면 아래 추가 입력에 채워짐)");
			DrawMissingChips(target, missing);
		}

		// 등록된 항목 — 접힌 목록으로 두고, 펼친 것만 인스펙터처럼 편집
		SectionHeader("등록된 항목");
		DrawRegisteredEntries(target, so, array);

		// enum에 없는 종류를 이름만으로 만들기
		DrawRegistryNewTypeRow(target);

		// 이미 enum에 있는 종류를 매니저에 등록
		DrawRegistryAddRow(target, so, array);

		EditorGUILayout.EndVertical();
	}

	// 누락된 enum 값을 버튼 칩으로 나열. 칩 폭은 글자 길이대로 잡고, 창 폭이 차면 줄을 바꾼다.
	private void DrawMissingChips(RegistryTarget target, List<int> missing)
	{
		float available = EditorGUIUtility.currentViewWidth - 48f;
		const float spacing = 4f;

		Color prev = GUI.color;
		GUI.color = new Color(1f, 0.85f, 0.55f);

		bool rowOpen = false;
		float used = 0f;

		for (int i = 0; i < missing.Count; i++)
		{
			string name = System.Enum.GetName(target.enumType, missing[i]) ?? missing[i].ToString();

			// 글자가 다 들어가는 폭을 스타일에서 직접 계산해 잘림을 막는다
			float width = EditorStyles.miniButton.CalcSize(new GUIContent(name)).x + 10f;
			width = Mathf.Min(width, available);

			if (rowOpen && used + width + spacing > available)
			{
				GUILayout.FlexibleSpace();
				EditorGUILayout.EndHorizontal();
				rowOpen = false;
			}

			if (!rowOpen)
			{
				EditorGUILayout.BeginHorizontal();
				rowOpen = true;
				used = 0f;
			}

			if (GUILayout.Button(name, EditorStyles.miniButton, GUILayout.Width(width)))
			{
				_addEnumValue[target.label] = missing[i];
			}
			used += width + spacing;
		}

		if (rowOpen)
		{
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}

		GUI.color = prev;
	}

	// 등록된 항목 목록. 한 줄에 [종류 이름][에셋 요약][편집][삭제]를 나란히 놓고,
	// 편집을 누른 항목만 그 아래에 전체 필드를 펼침(인스펙터를 여기서 대체).
	private void DrawRegisteredEntries(RegistryTarget target, SerializedObject so, SerializedProperty array)
	{
		if (!_editIndex.TryGetValue(target.label, out int openIndex))
		{
			openIndex = -1;
		}

		for (int i = 0; i < array.arraySize; i++)
		{
			SerializedProperty element = array.GetArrayElementAtIndex(i);
			SerializedProperty enumProp = element.FindPropertyRelative(target.enumFieldName);
			string name = enumProp != null
				? (System.Enum.GetName(target.enumType, enumProp.intValue) ?? enumProp.intValue.ToString())
				: $"[{i}]";

			bool isOpen = openIndex == i;

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(name, GUILayout.MinWidth(140f));
			EditorGUILayout.LabelField(SummarizeEntry(target, element), EditorStyles.miniLabel);
			if (GUILayout.Button(isOpen ? "닫기" : "편집", GUILayout.Width(52f)))
			{
				_editIndex[target.label] = isOpen ? -1 : i;
			}
			if (GUILayout.Button("삭제", GUILayout.Width(52f)))
			{
				array.DeleteArrayElementAtIndex(i);
				so.ApplyModifiedProperties();
				EditorUtility.SetDirty(target.managerComponent);
				_editIndex[target.label] = -1;
				EditorGUILayout.EndHorizontal();
				return;
			}
			EditorGUILayout.EndHorizontal();

			if (isOpen)
			{
				// 원소 안의 모든 직렬화 필드를 그대로 그림 — 사운드 볼륨/폴리포니, 풀 카테고리/사이즈 전부 여기서 편집됨
				EditorGUI.indentLevel++;
				SerializedProperty iterator = element.Copy();
				SerializedProperty end = element.GetEndProperty();
				bool enterChildren = true;
				while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
				{
					enterChildren = false;
					EditorGUILayout.PropertyField(iterator, true);
				}
				EditorGUI.indentLevel--;

				if (so.hasModifiedProperties)
				{
					so.ApplyModifiedProperties();
					EditorUtility.SetDirty(target.managerComponent);
				}
			}
		}

		if (array.arraySize == 0)
		{
			EditorGUILayout.LabelField("등록된 항목 없음", EditorStyles.miniLabel);
		}
	}

	// 목록 한 줄에 보여줄 요약(연결된 에셋 이름). 뭐가 비었는지 펼치지 않고도 보이게 함.
	private static string SummarizeEntry(RegistryTarget target, SerializedProperty element)
	{
		if (target.prefabFieldName != null)
		{
			SerializedProperty prop = element.FindPropertyRelative(target.prefabFieldName);
			if (prop != null)
			{
				return prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "⚠ 비어 있음";
			}
		}
		if (target.arrayAssetFieldName != null)
		{
			SerializedProperty prop = element.FindPropertyRelative(target.arrayAssetFieldName);
			if (prop != null && prop.isArray)
			{
				if (prop.arraySize == 0)
				{
					return "⚠ 클립 없음";
				}
				Object first = prop.GetArrayElementAtIndex(0).objectReferenceValue;
				string firstName = first != null ? first.name : "(빈 칸)";
				return prop.arraySize == 1 ? firstName : $"{firstName} +{prop.arraySize - 1}";
			}
		}
		return "";
	}

	// enum에 없는 새 종류를 이름만으로 만들 수 있게 함.
	// enum은 코드라서 추가 후 컴파일이 끝나야 그 값을 고를 수 있음 — 그래서 등록은 다음 단계가 됨.
	private void DrawRegistryNewTypeRow(RegistryTarget target)
	{
		SectionHeader("새 종류 만들기 (enum에 없는 것)");

		if (!_newTypeName.TryGetValue(target.label, out string typed))
		{
			typed = "";
		}

		EditorGUILayout.BeginHorizontal();
		if (!string.IsNullOrEmpty(target.namePrefix))
		{
			EditorGUILayout.LabelField(target.namePrefix, GUILayout.Width(44f));
		}
		typed = EditorGUILayout.TextField(typed);
		_newTypeName[target.label] = typed;

		string fullName = (target.namePrefix ?? "") + typed;
		bool valid = HubEnumEditor.IsValidIdentifier(fullName) && !HubEnumEditor.Exists(target.enumType, fullName);

		EditorGUI.BeginDisabledGroup(!valid || HubEnumEditor.IsCompiling);
		if (GUILayout.Button("enum에 추가", GUILayout.Width(100f)))
		{
			string nameToAdd = fullName;
			System.Type enumType = target.enumType;
			int step = target.numberStep;
			int sentinel = target.sentinelThreshold;
			// 코드 수정 + 리임포트가 끼므로 GUI가 끝난 뒤에 실행
			EditorApplication.delayCall += () =>
			{
				if (HubEnumEditor.AddMember(enumType, nameToAdd, step, sentinel))
				{
					_newTypeName[target.label] = "";
				}
			};
		}
		EditorGUI.EndDisabledGroup();
		EditorGUILayout.EndHorizontal();

		if (!string.IsNullOrWhiteSpace(typed))
		{
			if (!HubEnumEditor.IsValidIdentifier(fullName))
			{
				EditorGUILayout.HelpBox($"'{fullName}'은 쓸 수 없는 이름임 — 영문/숫자/밑줄만, 숫자로 시작 불가.", MessageType.Error);
			}
			else if (HubEnumEditor.Exists(target.enumType, fullName))
			{
				EditorGUILayout.HelpBox($"'{fullName}'은 이미 {target.enumType.Name}에 있음 — 아래에서 골라 등록할 것.", MessageType.Info);
			}
			else
			{
				int next = HubEnumEditor.NextFreeValue(target.enumType, target.numberStep, target.sentinelThreshold);
				EditorGUILayout.HelpBox($"{target.enumType.Name}에 '{fullName} = {next}' 로 추가됨.\n" +
										"추가 후 컴파일이 끝나면 아래 '새 항목 추가'에서 골라 등록할 수 있음.", MessageType.None);
			}
		}

		if (HubEnumEditor.IsCompiling)
		{
			EditorGUILayout.HelpBox("컴파일 중 — 끝나면 새 종류를 고를 수 있음.", MessageType.Info);
		}
	}

	private void DrawRegistryAddRow(RegistryTarget target, SerializedObject so, SerializedProperty array)
	{
		SectionHeader("새 항목 추가");

		if (!_addEnumValue.TryGetValue(target.label, out int curEnum))
		{
			curEnum = 0;
		}

		// 종류 + 단일 에셋을 한 줄에 — 폭이 남으므로 줄을 나눌 이유가 없음
		EditorGUILayout.BeginHorizontal();
		System.Enum current = (System.Enum)System.Enum.ToObject(target.enumType, curEnum);
		System.Enum picked = EditorGUILayout.EnumPopup("종류", current);
		_addEnumValue[target.label] = System.Convert.ToInt32(picked);

		Object singleAsset = null;
		if (target.prefabFieldName != null)
		{
			_addAsset.TryGetValue(target.label, out singleAsset);
			singleAsset = EditorGUILayout.ObjectField(singleAsset, target.prefabAssetType, false);
			_addAsset[target.label] = singleAsset;
		}
		EditorGUILayout.EndHorizontal();

		// 배열형 에셋(사운드 clips[]) — 여러 개를 모아 한 번에 등록
		List<Object> assetList = null;
		if (target.arrayAssetFieldName != null)
		{
			if (!_addAssetList.TryGetValue(target.label, out assetList))
			{
				assetList = new List<Object>();
				_addAssetList[target.label] = assetList;
			}

			for (int i = 0; i < assetList.Count; i++)
			{
				EditorGUILayout.BeginHorizontal();
				assetList[i] = EditorGUILayout.ObjectField($"클립 {i}", assetList[i], target.arrayAssetType, false);
				if (GUILayout.Button("-", GUILayout.Width(24f)))
				{
					assetList.RemoveAt(i);
					EditorGUILayout.EndHorizontal();
					break;
				}
				EditorGUILayout.EndHorizontal();
			}

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("+ 클립 추가", GUILayout.Width(110f)))
			{
				assetList.Add(null);
			}
			EditorGUILayout.LabelField("여러 개 넣으면 재생마다 랜덤으로 하나 선택됨", EditorStyles.miniLabel);
			EditorGUILayout.EndHorizontal();
		}

		if (GUILayout.Button("추가", GUILayout.Height(22f)))
		{
			AddRegistryEntry(target, so, array, singleAsset, assetList);
		}
	}

	// 등록 배열 끝에 항목을 추가하고, 추가된 항목을 바로 편집 상태로 펼침.
	private void AddRegistryEntry(RegistryTarget target, SerializedObject so, SerializedProperty array,
		Object singleAsset, List<Object> assetList)
	{
		int enumValue = _addEnumValue[target.label];

		// 이미 등록된 값이면 중복 추가 방지
		for (int i = 0; i < array.arraySize; i++)
		{
			SerializedProperty e = array.GetArrayElementAtIndex(i).FindPropertyRelative(target.enumFieldName);
			if (e != null && e.intValue == enumValue)
			{
				Debug.LogWarning($"[Hub] 이미 등록된 종류임: {System.Enum.GetName(target.enumType, enumValue)}");
				return;
			}
		}

		array.InsertArrayElementAtIndex(array.arraySize);
		SerializedProperty added = array.GetArrayElementAtIndex(array.arraySize - 1);
		added.FindPropertyRelative(target.enumFieldName).intValue = enumValue;

		if (target.prefabFieldName != null && singleAsset != null)
		{
			SerializedProperty prop = added.FindPropertyRelative(target.prefabFieldName);
			if (prop != null)
			{
				prop.objectReferenceValue = singleAsset;
			}
		}

		// 배열형 에셋은 크기를 맞춘 뒤 하나씩 대입. null 항목은 건너뛰지 않고 그대로 둠
		// (사용자가 넣은 칸 수를 유지해야 인스펙터에서 헷갈리지 않음)
		if (target.arrayAssetFieldName != null && assetList != null && assetList.Count > 0)
		{
			SerializedProperty arrayProp = added.FindPropertyRelative(target.arrayAssetFieldName);
			if (arrayProp != null && arrayProp.isArray)
			{
				arrayProp.arraySize = assetList.Count;
				for (int i = 0; i < assetList.Count; i++)
				{
					arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = assetList[i];
				}
			}
		}

		so.ApplyModifiedProperties();
		EditorUtility.SetDirty(target.managerComponent);

		// 추가 직후 그 항목을 펼쳐서 나머지 설정(볼륨/폴리포니/사이즈 등)을 바로 만질 수 있게 함
		_editIndex[target.label] = array.arraySize - 1;
		if (assetList != null)
		{
			assetList.Clear();
		}
		_addAsset[target.label] = null;

		Debug.Log($"[Hub] {target.label}에 {System.Enum.GetName(target.enumType, enumValue)} 추가함.");
	}

	// enum의 '끝/미지정' 성격 값은 누락 목록에서 제외 — 등록 대상이 아니라서.
	private static bool IsSentinelEnumValue(System.Type enumType, int value)
	{
		string name = System.Enum.GetName(enumType, value);
		if (string.IsNullOrEmpty(name))
		{
			return true;
		}
		// VFX_NONE, SFX_NONE, NONE, UNKNOWN 같은 미지정 값
		return name.EndsWith("_NONE") || name == "NONE" || name == "UNKNOWN";
	}
}
