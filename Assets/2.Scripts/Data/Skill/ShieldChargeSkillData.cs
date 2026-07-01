using UnityEngine;

[CreateAssetMenu(fileName = "New ShieldChargeSkillData", menuName = "Create Data/Skill/ShieldChargeSkillData")]
public class ShieldChargeSkillData : ActiveSkillData
{

	public override Skill CreateSkill(Unit owner)
	{
		return new ShieldChargeSkill(owner, this);
	}

}
