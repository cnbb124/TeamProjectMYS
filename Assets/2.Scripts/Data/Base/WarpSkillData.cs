using UnityEngine;

// WarpSkill 전용 데이터. skillCoolDown/maxUseCount는 ActiveSkillData에서 상속.
[CreateAssetMenu(fileName = "New Warp Skill Data", menuName = "Create Data/Skill/Warp Skill Data")]
public class WarpSkillData : ActiveSkillData
{
	[Tooltip("워프 이동 거리.")]
	public float warpDistance = 50f;
	[Tooltip("워프하는데 걸리는 시간")]
	public float warpSequenceTime = 1f;
	[Tooltip("워프시 출력될 VFX")]
	public GameObject warpEffectPrefab;

	


	public override Skill CreateInstance(Unit owner)
	{
		return new WarpSkill(owner, this);
	}
}
