using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// SceneTemplateData — 씬 한 종류의 구성표.
//
// "전투씬에 뭐가 들어가야 하는가"를 코드에 박지 않고 에셋으로 들고 감.
// 이미 잘 돌아가는 씬(STAGE1 등)을 열어 Hub에서 [현재 씬을 템플릿으로 저장]을 누르면
// 그 씬의 루트 구성이 그대로 여기에 기록됨.
//
// 종류가 늘면 에셋을 하나 더 만들면 됨 — Template_Battle / Template_Station / Template_Menu 식.
// 코드를 고칠 필요가 없어서 프로그래머가 아니어도 종류를 늘릴 수 있음.
//
// ⚠ 에디터 전용 에셋임(Editor 폴더에 있음). 런타임 코드에서 참조하지 말 것.
// =====================================================================
[CreateAssetMenu(fileName = "New SceneTemplate", menuName = "Create Data/Scene Template")]
public class SceneTemplateData : ScriptableObject
{
	[Header("설명 (참고용)")]
	[TextArea(2, 4)]
	public string description;

	[Header("배치할 프리팹 — 순서대로 씬 루트에 놓임")]
	public List<GameObject> prefabs = new List<GameObject>();

	[Header("새로 만들 빈 오브젝트 — MapObject 같은 컨테이너용")]
	public List<string> emptyObjectNames = new List<string>();

	// 컨테이너 하위에 놓인 프리팹의 배치까지 기록하는 항목.
	// 새 전투 스테이지는 배치를 새로 하는 게 정상이라 기본은 비어 있고,
	// 마을씬처럼 배치 자체가 내용인 씬은 캡처 시 '하위 배치까지 기록'을 켜서 담음.
	[System.Serializable]
	public class PlacedObject
	{
		public GameObject prefab;
		[Tooltip("부모 경로. 'MapObject' 또는 'MapObject/Asteroids' 식. 비어 있으면 씬 루트")]
		public string parentPath;
		public Vector3 localPosition;
		public Vector3 localEuler;
		public Vector3 localScale = Vector3.one;
	}

	[Header("배치까지 기록된 프리팹 (선택 사항)")]
	public List<PlacedObject> placedObjects = new List<PlacedObject>();

	[Header("조명")]
	[Tooltip("켜면 기본 Directional Light를 새로 만듦. 스테이지마다 분위기가 달라야 하므로 프리팹으로 안 묶는 쪽이 나음")]
	public bool createDirectionalLight = true;
	[Tooltip("만들 조명의 회전(도)")]
	public Vector3 lightRotation = new Vector3(50f, -30f, 0f);

	// Lighting > Environment 설정. 씬마다 따로 저장되는 값이라 새 씬을 만들면 기본값으로 초기화됨 —
	// 배경(스카이박스)이 안 따라오는 게 그 때문이므로 템플릿이 같이 들고 감.
	[Header("환경 (Lighting > Environment)")]
	public Material skybox;
	public UnityEngine.Rendering.AmbientMode ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
	public float ambientIntensity = 1f;
	public Color ambientSkyColor = new Color(0.212f, 0.227f, 0.259f, 1f);

	[Header("스폰 포인트")]
	public int defaultSpawnPointCount = 8;
	public int fixedSpawnPointCount = 2;
	public float spawnRingRadius = 400f;

	[Header("기본값")]
	[Tooltip("씬 생성 시 미리 채워둘 BGM. SFX_NONE이면 비워둠")]
	public SOUND_TYPE bgm = SOUND_TYPE.SFX_NONE;
	[Tooltip("씬을 저장할 폴더")]
	public string targetFolder = "Assets/1.Scenes/BuildScene";

	[Header("재현 불가 항목 (캡처 시 기록됨, 읽기용)")]
	[Tooltip("프리팹이 아니면서 스크립트/컴포넌트를 가진 오브젝트 — 새 씬에서 자동 재현이 안 됨.\n" +
			 "여기 뭔가 남아 있으면 그 오브젝트를 먼저 프리팹으로 만들고 다시 캡처할 것.")]
	public List<string> notReproducible = new List<string>();
}
