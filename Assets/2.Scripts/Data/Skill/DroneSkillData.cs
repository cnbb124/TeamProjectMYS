using UnityEngine;

[CreateAssetMenu(fileName = "New DronSkillData", menuName = "Create Data/Skill/DronSkillData")]
public class DroneSkillData : ActiveSkillData
{

	public override Skill CreateSkill(Unit owner)
	{
		return new DroneSkill(owner, this);
	}

}
