using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;




public class MYSSkill : ActiveSkill
{
	private MYSSkillData MYSSkillData

	{
		get
		{
			return _activeSkillData as MYSSkillData;
		}
	}



	public MYSSkill(Unit owner, MYSSkillData skillData) : base(owner, skillData)
	{
	}



	// 레거시. 데이터로옮김
	////발사할 미사일 수
	//private int _missileCount;

	////발사할 타입
	//private SKILL_MSY_TYPE msyType;

	private bool _isFiring;
	private float _fireStartTime;

	protected override void UseSkill()
	{
		base.UseSkill();
		_isFiring = true;
		_fireStartTime = Time.time;

	}
	public override void UpdateSkill()
	{
		if (!_isFiring)
		{
			return;
		}

		if (Time.time < _fireStartTime + MYSSkillData.siloOpenTime)
		{
			return;
		}
		_isFiring = false;
		FireMYS();

	}

	private void FireMYS()
	{
		switch (MYSSkillData.msyType)
		{
			case SKILL_MSY_TYPE.HOMING:
				break;
			case SKILL_MSY_TYPE.CLUSTER:
				break;
		}

	}
}
