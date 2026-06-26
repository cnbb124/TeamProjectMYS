using System.Collections.Generic;
using UnityEngine;

// 모든 SkillData SO를 id로 조회하는 데이터베이스. ItemDatabase와 동일 패턴.
// 에디터 인스펙터의 "전체 스캔 & 갱신" 버튼으로 allSkills 배열을 채운 뒤 커밋.
// GameManager.Awake()에서 skillDatabase.Init() 호출 필요.
// 조회: SkillDatabase.Instance.Get(SKILL_ID.WARP)
//        SkillDatabase.Instance.Get<WarpSkillData>(SKILL_ID.WARP)
[CreateAssetMenu(fileName = "New Skill Database", menuName = "Create Data/Skill/Skill Database")]
public class SkillDatabase : ScriptableObject
{
	public static SkillDatabase Instance { get; private set; }

	public SkillData[] allSkills;

	private Dictionary<int, SkillData> _db = new Dictionary<int, SkillData>();

	public void Init()
	{
		Instance = this;
		_db.Clear();
		foreach (SkillData skill in allSkills)
		{
			if (skill == null || skill.id == SKILL_ID.NONE)
			{
				continue;
			}
			_db[(int)skill.id] = skill;
		}
	}

	public SkillData Get(SKILL_ID id)
	{
		SkillData result;
		_db.TryGetValue((int)id, out result);
		return result;
	}

	public T Get<T>(SKILL_ID id) where T : SkillData
	{
		return Get(id) as T;
	}

	public SkillData Get(int id)
	{
		SkillData result;
		_db.TryGetValue(id, out result);
		return result;
	}
}
