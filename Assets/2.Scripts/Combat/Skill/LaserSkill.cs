using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================

// ================================================================

public class LaserSkill : ActiveSkill
{
	private LaserSkillData LaserSkillData
	{
		get
		{
			return _activeSkillData as LaserSkillData;
		}
	}

	private bool _isWarping;
	private float _warpStartTime;

	public LaserSkill(Unit owner, LaserSkillData skillData) : base(owner, skillData)
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
