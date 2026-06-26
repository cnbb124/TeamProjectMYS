using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillDatabase))]
public class SkillDatabaseEditor : Editor
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		EditorGUILayout.Space();

		if (GUILayout.Button("전체 스캔 & 갱신", GUILayout.Height(30)))
		{
			ScanAllSkills();
		}
	}

	private void ScanAllSkills()
	{
		List<SkillData> found = new List<SkillData>();
		List<string> noIdSkills = new List<string>();

		string[] guids = AssetDatabase.FindAssets("t:SkillData");
		foreach (string guid in guids)
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
			if (skill == null)
			{
				continue;
			}
			if (skill.id == SKILL_ID.NONE)
			{
				noIdSkills.Add(path);
				continue;
			}
			found.Add(skill);
		}

		found.Sort(CompareById);

		SerializedProperty prop = serializedObject.FindProperty("allSkills");
		prop.arraySize = found.Count;
		for (int i = 0; i < found.Count; i++)
		{
			prop.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
		}

		serializedObject.ApplyModifiedProperties();
		EditorUtility.SetDirty(target);
		AssetDatabase.SaveAssets();

		Debug.Log($"SkillDatabase 갱신 완료: {found.Count}개 등록");

		if (noIdSkills.Count > 0)
		{
			Debug.LogWarning($"SKILL_ID.NONE인 항목 {noIdSkills.Count}개 제외됨 (id 설정 필요):\n" + string.Join("\n", noIdSkills));
		}
	}

	private static int CompareById(SkillData a, SkillData b)
	{
		return ((int)a.id).CompareTo((int)b.id);
	}
}
