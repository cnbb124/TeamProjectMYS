using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PartData))]
public class PartDataEditor : Editor
{
    // providedSlots에서 제외할 BASE 타입 (항상 자동 생성되므로 선택 불필요)
    private static PART_TYPE[] EXCLUDED_TYPES = { PART_TYPE.ENGINE, PART_TYPE.FRAME };

    private PART_TYPE[] _allowedTypes;
    private string[] _allowedTypeNames;

    private void OnEnable()
    {
        List<PART_TYPE> allowed = new List<PART_TYPE>();
        foreach (PART_TYPE type in Enum.GetValues(typeof(PART_TYPE)))
        {
            bool excluded = false;
            foreach (PART_TYPE ex in EXCLUDED_TYPES)
            {
                if (type == ex)
                {
                    excluded = true;
                    break;
                }
            }
            if (!excluded)
            {
                allowed.Add(type);
            }
        }

        _allowedTypes = allowed.ToArray();
        _allowedTypeNames = new string[_allowedTypes.Length];
        for (int i = 0; i < _allowedTypes.Length; i++)
        {
            _allowedTypeNames[i] = _allowedTypes[i].ToString();
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        PartData partData = (PartData)target;

        if (partData.partType == PART_TYPE.FRAME)
        {
            // FRAME: maxPartHp 숨김 (유닛 본체 HP와 동일시. statBonuses HP_MAX로 설정)
            DrawPropertiesExcluding(serializedObject, "providedSlots", "maxPartHp");

            EditorGUILayout.HelpBox("FRAME은 maxPartHp 미사용.\nHP는 Stat Bonuses → HP_MAX로 설정하세요.", MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Provided Slots (ENGINE/FRAME 선택 불가)", EditorStyles.boldLabel);
            DrawProvidedSlots();
        }
        else
        {
            // 일반 파츠: providedSlots 미표시 (FRAME 전용 필드)
            DrawPropertiesExcluding(serializedObject, "providedSlots");
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawProvidedSlots()
    {
        SerializedProperty prop = serializedObject.FindProperty("providedSlots");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        for (int i = 0; i < prop.arraySize; i++)
        {
            EditorGUILayout.BeginHorizontal();

            SerializedProperty element = prop.GetArrayElementAtIndex(i);
            int currentValue = element.intValue;

            int currentIndex = 0;
            for (int j = 0; j < _allowedTypes.Length; j++)
            {
                if ((int)_allowedTypes[j] == currentValue)
                {
                    currentIndex = j;
                    break;
                }
            }

            int newIndex = EditorGUILayout.Popup(currentIndex, _allowedTypeNames);
            element.intValue = (int)_allowedTypes[newIndex];

            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                prop.DeleteArrayElementAtIndex(i);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ 슬롯 추가"))
        {
            prop.InsertArrayElementAtIndex(prop.arraySize);
            prop.GetArrayElementAtIndex(prop.arraySize - 1).intValue = (int)_allowedTypes[0];
        }

        EditorGUILayout.EndVertical();
    }
}
