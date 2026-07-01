using UnityEngine;

[CreateAssetMenu(fileName = "New DronSkillData", menuName = "Create Data/Skill/Drone Skill Data")]
public class DroneSkillData : ActiveSkillData
{

	public override Skill CreateSkill(Unit owner)
	{
		return new DroneSkill(owner, this);
	}

}
