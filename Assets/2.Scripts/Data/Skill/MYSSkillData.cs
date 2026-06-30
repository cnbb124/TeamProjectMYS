using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New MYS Skill Data", menuName = "Create Data/Skill/MYS Skill Data")]

public class MYSSkillData : ActiveSkillData
{
	[Tooltip("발사할 미사일 타입")]
	public SKILL_MSY_TYPE msyType;
	[Tooltip("미사일 SO 데이터")]
	public MissileData missileData;
	[Tooltip("발사할 미사일 갯수")]
	public int missileCount;
	[Tooltip("사일로 오픈 시간(애니메이션등)")]
	public float siloOpenTime;
	[Tooltip("발사 지속 시간")]
	public float fireDurationTime;

	public override Skill CreateSkill(Unit owner)
	{
		return new MYSSkill(owner, this);
	}
}
