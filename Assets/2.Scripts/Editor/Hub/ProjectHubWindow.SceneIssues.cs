using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================
// ProjectHubWindow — 씬 문제 목록.
//
// 표를 읽고 사람이 판단하던 걸 대신함. "무엇이 잘못됐고 어떻게 고치는지"를
// 한 줄씩 보여주고, 고칠 수 있는 건 버튼 하나로 처리함.
// =====================================================================
public partial class ProjectHubWindow
{
	private enum IssueLevel { Warn, Error }

	private class SceneIssue
	{
		public IssueLevel level;
		public string message;
		public string fixLabel;    // 비우면 버튼 없음(사람이 직접 해야 하는 것)
		public Action fix;
	}

	private readonly List<SceneIssue> _sceneIssues = new List<SceneIssue>();

	private void DrawSceneIssues()
	{
		BuildSceneIssues();

		EditorGUILayout.LabelField(
			_sceneIssues.Count > 0 ? $"확인 요망 사항 ({_sceneIssues.Count})" : "확인 요망 사항",
			HubStyles.SectionTitle);

		if (_sceneIssues.Count == 0)
		{
			HubStyles.ColoredLabel("모두 정상입니다.", HubStyles.Ok, HubStyles.IssueText);
			HubStyles.Separator();
			return;
		}

		for (int i = 0; i < _sceneIssues.Count; i++)
		{
			DrawIssueRow(_sceneIssues[i]);
		}

		HubStyles.Separator();
	}

	private void DrawIssueRow(SceneIssue issue)
	{
		EditorGUILayout.BeginHorizontal(HubStyles.Card);

		Rect dot = GUILayoutUtility.GetRect(8f, 8f, GUILayout.Width(8f), GUILayout.Height(8f));
		dot.y += 5f;
		HubStyles.StatusDot(dot, issue.level == IssueLevel.Error ? HubStyles.Error : HubStyles.Warn);

		EditorGUILayout.LabelField(issue.message, HubStyles.IssueText);

		if (issue.fix != null && !string.IsNullOrEmpty(issue.fixLabel))
		{
			if (GUILayout.Button(issue.fixLabel, GUILayout.Width(96f)))
			{
				Action deferred = issue.fix;
				// 목록을 그리는 중에 목록을 다시 만들면 레이아웃이 깨지므로 GUI가 끝난 뒤에 실행
				EditorApplication.delayCall += () =>
				{
					deferred();
					RefreshSceneRows();
					Repaint();
				};
			}
		}

		EditorGUILayout.EndHorizontal();
	}

	private void BuildSceneIssues()
	{
		_sceneIssues.Clear();

		SerializedObject so = FindGameManagerSerialized(out GameObject prefab);
		SerializedProperty list = so != null ? so.FindProperty("_sceneSettings") : null;
		if (list == null || !list.isArray)
		{
			_sceneIssues.Add(new SceneIssue
			{
				level = IssueLevel.Error,
				message = "GameManager 프리팹의 씬 설정표를 찾지 못했습니다. 3.Prefabs 안에 GameManager 프리팹이 있는지 확인하세요.",
			});
			return;
		}

		for (int i = 0; i < _sceneRows.Count; i++)
		{
			SceneRow row = _sceneRows[i];
			bool isWorkScene = string.IsNullOrEmpty(row.typeName);
			if (isWorkScene || string.IsNullOrEmpty(row.assetPath))
			{
				continue;   // 작업씬과 파일 없는 이름은 아래에서 따로 처리
			}

			string display = SceneDisplayNames.TryGetValue(row.sceneType, out string d)
				? $"{d}({row.typeName})"
				: row.typeName;

			if (!row.inBuildSettings)
			{
				string path = row.assetPath;
				_sceneIssues.Add(new SceneIssue
				{
					level = IssueLevel.Error,
					message = $"{display} 씬이 빌드 목록에 없습니다. 게임에서 이 씬으로 넘어갈 수 없습니다.",
					fixLabel = "빌드에 추가",
					fix = () => AddSceneToBuildSettings(path),
				});
			}
			else if (!row.buildEnabled)
			{
				string path = row.assetPath;
				_sceneIssues.Add(new SceneIssue
				{
					level = IssueLevel.Warn,
					message = $"{display} 씬이 빌드 목록에 있지만 체크가 꺼져 있습니다. 빌드에 포함되지 않습니다.",
					fixLabel = "체크 켜기",
					fix = () => EnableSceneInBuildSettings(path),
				});
			}

			SerializedProperty entry = FindEntry(list, row.sceneType);
			if (entry == null)
			{
				SCENE_TYPE type = row.sceneType;
				_sceneIssues.Add(new SceneIssue
				{
					level = IssueLevel.Error,
					message = $"{display} 씬의 설정이 등록되지 않았습니다. 배경음과 씬 종류가 모두 비어 있는 것으로 처리됩니다.",
					fixLabel = "설정 추가",
					fix = () => AddSceneSettingsRow(type),
				});
				continue;
			}

			SerializedProperty bgm = entry.FindPropertyRelative("bgm");
			if (bgm != null && bgm.intValue == (int)SOUND_TYPE.SFX_NONE)
			{
				_sceneIssues.Add(new SceneIssue
				{
					level = IssueLevel.Warn,
					message = $"{display} 씬에 배경음이 지정되지 않았습니다. 아래 표에서 [설정]을 눌러 고를 수 있습니다.",
				});
			}
		}

		// SCENE_TYPE에는 있는데 씬 파일이 없는 이름
		foreach (SCENE_TYPE type in Enum.GetValues(typeof(SCENE_TYPE)))
		{
			if (type == SCENE_TYPE.UNKNOWN || HasSceneFile(type))
			{
				continue;
			}
			_sceneIssues.Add(new SceneIssue
			{
				level = IssueLevel.Warn,
				message = $"{type} 는 이름만 있고 실제 씬 파일이 없습니다. 씬을 만들거나 SCENE_TYPE에서 지우세요.",
			});
		}
	}

	private void AddSceneSettingsRow(SCENE_TYPE type)
	{
		SerializedObject so = FindGameManagerSerialized(out GameObject prefab);
		SerializedProperty list = so != null ? so.FindProperty("_sceneSettings") : null;
		if (list == null || !list.isArray)
		{
			return;
		}

		list.InsertArrayElementAtIndex(list.arraySize);
		SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
		SetInt(added, "scene", (int)type);
		SetInt(added, "bgm", (int)SOUND_TYPE.SFX_NONE);
		SetInt(added, "category", (int)GuessCategory(type));
		so.ApplyModifiedProperties();
		EditorUtility.SetDirty(prefab);
		AssetDatabase.SaveAssets();
	}
}
