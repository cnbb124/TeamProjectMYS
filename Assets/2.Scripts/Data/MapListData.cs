using UnityEngine;

// =====================================================================
// MapListData — 맵 선택 화면의 버튼별 진입 씬 목록.
//
// MapSelectorUI가 이 에셋을 읽어 버튼 i를 maps[i]에 연결함.
// 씬 이름을 코드(핸들러)에 박지 않고 여기에 두는 이유:
//   ① 씬을 추가할 때 코드 수정 없이 목록만 늘리면 됨
//   ② Hub의 씬 생성이 "이 씬을 몇 번 버튼에 넣기"를 이 에셋에 써 넣을 수 있음
//
// index 0 = 1번 버튼. sceneName이 비어 있으면 그 버튼은 잠김(interactable=false).
// =====================================================================
[CreateAssetMenu(fileName = "New Map List", menuName = "Create Data/Map List")]
public class MapListData : ScriptableObject
{
	[System.Serializable]
	public class MapEntry
	{
		[Tooltip("버튼에 표시할 이름(선택). 비워도 진입에는 영향 없음")]
		public string displayName;

		[Tooltip("진입시킬 씬 이름. SCENE_TYPE 이름과 동일해야 함(대소문자 무관).\n" +
				 "비우면 이 버튼은 잠김.")]
		public string sceneName;
	}

	[Tooltip("index 0 = 1번 버튼. 버튼 수만큼 항목을 둠.")]
	public MapEntry[] maps = new MapEntry[0];

	/// <summary>버튼 인덱스의 씬 이름. 범위를 벗어나거나 비어 있으면 null.</summary>
	public string GetSceneName(int index)
	{
		if (index < 0 || index >= maps.Length || maps[index] == null)
		{
			return null;
		}
		string s = maps[index].sceneName;
		return string.IsNullOrWhiteSpace(s) ? null : s;
	}
}
