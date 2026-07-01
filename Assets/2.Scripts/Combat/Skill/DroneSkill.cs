using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================

// ================================================================

public class DroneSkill : ActiveSkill
{
	private DroneSkillData DroneSkillData
	{
		get
		{
			return _activeSkillData as DroneSkillData;
		}
	}

	

	public DroneSkill(Unit owner, DroneSkillData skillData) : base(owner, skillData)
	{
	}

	protected override void UseSkill()
	{
		base.UseSkill();

	}

	public override void UpdateSkill()
	{
		
	}
}
