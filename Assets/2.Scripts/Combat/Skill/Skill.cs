// ================================================================
// [외부 참조 가이드]
// ================================================================
// 패시브/액티브 스킬 공통 베이스. MonoBehaviour 아님 — SkillData.CreateInstance()가
// 만들어내는 순수 데이터+로직 객체. 배움/슬롯 등록/보유 관리는 SkillSystem이 전담
// (이 클래스는 자기 자신을 슬롯에 등록하는 책임이 없음).
//
// _owner        이 스킬을 쓰는 캐릭터. 생성자에서 주입.
// _skillData    배움(언락) 조건 데이터(SO). 같은 스킬 클래스 + 다른 SkillData 에셋 = 변형 스킬.
// ================================================================

public abstract class Skill
{
	protected Unit _owner;
	protected SkillData _skillData;

	protected SoundManager _sound;
	protected VFXManager _vfx;
	protected PoolManager _pool;
	protected Skill(Unit owner, SkillData skillData)
	{
		_owner = owner;
		_skillData = skillData;
		_vfx = VFXManager.Instance;
		_sound = SoundManager.Instance;
		_pool = PoolManager.Instance;
	}

	/// <summary>저장/로드용 ID. SkillSystem.CollectSaveData()에서 사용.</summary>
	public SKILL_ID SkillId
	{
		get
		{
			return _skillData.id;
		}
	}
}
