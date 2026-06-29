using UnityEngine;

// 패시브/액티브 공통 — 배움(언락) 조건 + 인스턴스 생성 팩토리. 쿨다운/사용횟수 등 발동 관련 데이터는 ActiveSkillData에서.
public abstract class SkillData : ScriptableObject
{
	[Tooltip("저장/로드용 고유 ID. SkillDatabase가 이 값으로 역참조.")]
	public SKILL_ID id;

	[Tooltip("스킬 배움에 필요한 레벨. 0 이하면 레벨 조건 없음.")]
	public int requiredLevel;

	[Tooltip("스킬 배움에 필요한 호감도. 0 이하면 호감도 조건 없음.\n" +
		"※ AffectionManager는 구현됐으나 어느 NPC와의 호감도인지 지정하는 필드가 아직 없어서,\n" +
		"실제 NPC가 정해지기 전까지는 이 값 설정해도 항상 통과 처리됨.")]
	public int requiredAffinity;

	/// <summary>
	/// 스킬 배움 가능 여부 조회 함수. 부작용 없음 — SkillSystem.LearnSkill() 호출 전 먼저 체크.
	/// 레벨 조건만 실제 체크됨. 호감도는 위 설명대로 항상 통과.
	/// </summary>
	public virtual bool CanLearnSkill(Unit owner)
	{
		if (requiredLevel > 0)
		{
			Player player = owner as Player;
			if (player == null || player.level < requiredLevel)
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>이 데이터로 실제 동작하는 Skill 인스턴스를 만들어 반환. 구체 클래스에서 override구현.</summary>
	public abstract Skill CreateInstance(Unit owner);
}
