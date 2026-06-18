using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemDatabase))]
public class ItemDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space();

        if (GUILayout.Button("전체 스캔 & 갱신", GUILayout.Height(30)))
        {
            ScanAllItems();
        }
    }

    private void ScanAllItems()
    {
        List<ItemData> found = new List<ItemData>();
        List<string> noIdItems = new List<string>();

        string[] guids = AssetDatabase.FindAssets("t:ItemData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null)
            {
                continue;
            }
            if (item.id == ITEM_ID.NONE)
            {
                noIdItems.Add(path);
                continue;
            }
            found.Add(item);
        }

        // id 오름차순 정렬
        found.Sort((a, b) => ((int)a.id).CompareTo((int)b.id));

        SerializedProperty prop = serializedObject.FindProperty("allItems");
        prop.arraySize = found.Count;
        for (int i = 0; i < found.Count; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();

        Debug.Log($"ItemDatabase 갱신 완료: {found.Count}개 등록");

        if (noIdItems.Count > 0)
        {
            Debug.LogWarning($"ITEM_ID.NONE인 항목 {noIdItems.Count}개 제외됨 (id 설정 필요):\n" + string.Join("\n", noIdItems));
        }
    }
}
