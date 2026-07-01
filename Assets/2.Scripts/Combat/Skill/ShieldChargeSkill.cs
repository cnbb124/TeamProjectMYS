using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================

// ================================================================

public class ShieldChargeSkill : ActiveSkill
{
	private ShieldChargeSkillData ShieldChargeSkillData
	{
		get
		{
			return _activeSkillData as ShieldChargeSkillData;
		}
	}

	

	public ShieldChargeSkill(Unit owner, ShieldChargeSkillData skillData) : base(owner, skillData)
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
