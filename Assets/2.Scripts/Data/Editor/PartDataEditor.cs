using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PartData))]
public class PartDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // providedSlots 제외하고 기본 필드 전부 출력
        DrawPropertiesExcluding(serializedObject, "providedSlots");

        // FRAME 파츠일 때만 providedSlots 표시
        PartData partData = (PartData)target;
        if (partData.partType == PART_TYPE.FRAME)
        {
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("providedSlots"),
                new GUIContent("Provided Slots"),
                true
            );
        }

        serializedObject.ApplyModifiedProperties();
    }
}
