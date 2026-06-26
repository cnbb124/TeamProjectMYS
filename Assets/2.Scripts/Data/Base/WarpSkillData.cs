using UnityEngine;

// WarpSkill 전용 데이터. skillCoolDown/maxUseCount는 ActiveSkillData에서 상속.
[CreateAssetMenu(fileName = "New Warp Skill Data", menuName = "Create Data/Skill/Warp Skill Data")]
public class WarpSkillData : ActiveSkillData
{
	[Tooltip("워프 이동 거리.")]
	public float warpDistance = 50f;
}
