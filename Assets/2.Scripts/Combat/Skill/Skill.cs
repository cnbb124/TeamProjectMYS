using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skill : MonoBehaviour
{
	[SerializeField]
	protected float skillCoolDown;



	private float lastSkillUseTime;


	protected virtual bool canUseSkill()
	{
		//현재지난시간<마지막사용시간+쿨다운 즉  현재시간으로부터 쿨다운이 안됐으면 이면 취소
		if (Time.time < lastSkillUseTime + skillCoolDown)
		{
			
			return false;
		}
		
		return true;
	}

	protected virtual void UseSkill()
	{
		lastSkillUseTime = Time.time;
		//추가로직오버라이드
	}

	protected virtual void StopSkill()
	{

	}

}
