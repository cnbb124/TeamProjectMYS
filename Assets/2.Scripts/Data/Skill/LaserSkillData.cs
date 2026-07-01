using UnityEngine;

[CreateAssetMenu(fileName = "New Laser Skill Data", menuName = "Create Data/Skill/Laser Skill Data")]
public class LaserSkillData : ActiveSkillData
{

	public override Skill CreateSkill(Unit owner)
	{
		return new LaserSkill(owner, this);
	}

}
