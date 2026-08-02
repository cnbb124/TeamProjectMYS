using UnityEditor;
using UnityEngine;

// =====================================================================
// SceneTemplateData 인스펙터.
//
// '전투 스테이지 전용' 묶음은 씬 종류가 전투일 때만 보임 —
// 마을·격납고 템플릿에 스폰 포인트 칸이 보이면 무심코 값을 넣게 되고,
// 그러면 아무 데도 연결 안 되는 빈 오브젝트가 새 씬에 생김.
//
// 보이는 것만 막을 뿐 값 자체는 그대로 있음. 종류를 전투로 되돌리면 다시 나옴.
// =====================================================================
[CustomEditor(typeof(SceneTemplateData))]
public class SceneTemplateDataEditor : Editor
{
	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		SerializedProperty category = serializedObject.FindProperty("category");
		bool isBattle = category != null && (SCENE_CATEGORY)category.intValue == SCENE_CATEGORY.BATTLE;

		SerializedProperty iterator = serializedObject.GetIterator();
		// 첫 NextVisible만 자식으로 들어가야 최상위 필드부터 훑음
		bool enterChildren = true;
		while (iterator.NextVisible(enterChildren))
		{
			enterChildren = false;

			// 스크립트 칸은 기본 인스펙터처럼 회색으로 보여만 줌
			if (iterator.propertyPath == "m_Script")
			{
				EditorGUI.BeginDisabledGroup(true);
				EditorGUILayout.PropertyField(iterator, true);
				EditorGUI.EndDisabledGroup();
				continue;
			}

			if (iterator.propertyPath == "battle" && !isBattle)
			{
				continue;
			}

			EditorGUILayout.PropertyField(iterator, true);
		}

		serializedObject.ApplyModifiedProperties();
	}
}
