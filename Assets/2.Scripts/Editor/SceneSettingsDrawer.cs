using UnityEditor;
using UnityEngine;

// 씬 설정표 한 줄. 직접 지정이 꺼져 있으면 카테고리로 계산된 값을 회색으로 보여줌.
[CustomPropertyDrawer(typeof(GameManager.SceneSettings))]
public class SceneSettingsDrawer : PropertyDrawer
{
	private static readonly string[] FlagFields =
	{
		"keepsPlayerShip", "canSave", "isBattleScene", "isStationScene", "shipControlDisabled", "shipHidden"
	};

	private static readonly string[] FlagLabels =
	{
		"함선 유지", "저장 가능", "전투 스테이지", "정거장 계열", "조종 불가", "함선 숨김"
	};

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		float line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
		if (!property.isExpanded)
		{
			return line;
		}
		return line * (5 + FlagFields.Length);
	}

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		SerializedProperty scene = property.FindPropertyRelative("scene");
		SerializedProperty category = property.FindPropertyRelative("category");
		SerializedProperty bgm = property.FindPropertyRelative("bgm");
		SerializedProperty overrideFlags = property.FindPropertyRelative("overrideFlags");

		float line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
		Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

		property.isExpanded = EditorGUI.Foldout(row, property.isExpanded,
			$"{EnumName(scene)}  ({EnumName(category)})", true);
		if (!property.isExpanded)
		{
			return;
		}

		EditorGUI.indentLevel++;
		row.y += line;
		EditorGUI.PropertyField(row, scene);
		row.y += line;
		EditorGUI.PropertyField(row, category);
		row.y += line;
		EditorGUI.PropertyField(row, bgm);
		row.y += line;
		EditorGUI.PropertyField(row, overrideFlags, new GUIContent("속성 직접 지정"));

		GameManager.SceneSettings resolved = ResolveFrom(property);
		bool editable = overrideFlags.boolValue;

		using (new EditorGUI.DisabledScope(!editable))
		{
			for (int i = 0; i < FlagFields.Length; i++)
			{
				row.y += line;
				SerializedProperty flag = property.FindPropertyRelative(FlagFields[i]);
				if (flag == null)
				{
					continue;
				}

				if (editable)
				{
					EditorGUI.PropertyField(row, flag, new GUIContent(FlagLabels[i]));
				}
				else
				{
					EditorGUI.Toggle(row, FlagLabels[i], ResolvedFlag(resolved, i));
				}
			}
		}
		EditorGUI.indentLevel--;
	}

	// enum에 없는 값이 저장돼 있으면 enumValueIndex가 -1이라 배열 접근이 터짐
	private static string EnumName(SerializedProperty prop)
	{
		int index = prop.enumValueIndex;
		if (index < 0 || index >= prop.enumDisplayNames.Length)
		{
			return $"?({prop.intValue})";
		}
		return prop.enumDisplayNames[index];
	}

	private static GameManager.SceneSettings ResolveFrom(SerializedProperty property)
	{
		GameManager.SceneSettings entry = new GameManager.SceneSettings();
		entry.category = (SCENE_CATEGORY)property.FindPropertyRelative("category").intValue;
		entry.overrideFlags = false;
		return GameManager.Resolve(entry);
	}

	private static bool ResolvedFlag(GameManager.SceneSettings s, int index)
	{
		switch (index)
		{
			case 0: return s.keepsPlayerShip;
			case 1: return s.canSave;
			case 2: return s.isBattleScene;
			case 3: return s.isStationScene;
			case 4: return s.shipControlDisabled;
			default: return s.shipHidden;
		}
	}
}
