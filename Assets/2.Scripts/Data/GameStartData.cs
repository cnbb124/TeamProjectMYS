using System.Collections.Generic;
using UnityEngine;

// 새 게임 시작 상태를 한곳에 모은 SO. GameManager.ClearData()와 UnitParts가 읽어감.
// 값이 매니저 프리팹이 아니라 에셋에 있어야 Hub 데이터 탭에서 바로 만질 수 있음.
[CreateAssetMenu(fileName = "New GameStartData", menuName = "Create Data/Game Start Data")]
public class GameStartData : ScriptableObject
{
	[System.Serializable]
	public class StartItem
	{
		public ItemData item;
		public int count = 1;
	}

	[Header("<size=14>시작 재화</size>")]
	[Tooltip("새 게임 시작 시 지급할 골드.")]
	public int startGold = 0;

	[Header("<size=14>시작 레벨</size>")]
	public int startLevel = 1;

	[Header("<size=14>기준 기체</size>")]
	[Tooltip("스탯의 기준이 되는 플레이어 프리팹. 함선이 없는 씬(정거장)에서 스탯을 계산할 때 씀.")]
	public GameObject basePlayerPrefab;

	[Header("<size=14>시작 장착 파츠</size>")]
	[Tooltip("순서 무관. 코드에서 FRAME을 항상 먼저 처리함.")]
	public List<PartData> startParts = new List<PartData>();

	[Header("<size=14>시작 소지품</size>")]
	[Tooltip("장착이 아니라 가방에 들어감.")]
	public List<StartItem> startItems = new List<StartItem>();

	[Header("<size=14>시작 스킬</size>")]
	public List<SkillData> startSkills = new List<SkillData>();
}
