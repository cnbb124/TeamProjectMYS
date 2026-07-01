using UnityEngine;

[CreateAssetMenu(fileName = "New ShieldChargeSkillData", menuName = "Create Data/Skill/Shield Charge Skill Data")]
public class ShieldChargeSkillData : ActiveSkillData
{

	public override Skill CreateSkill(Unit owner)
	{
		return new ShieldChargeSkill(owner, this);
	}

}
