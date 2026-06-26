using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// canUseSkill()   쿨다운 다 됐는지 확인용 조회 함수. 부작용(쿨다운 갱신) 없음.
//                  → 스킬 발동 전 자식 클래스/입력처리 쪽에서 먼저 체크
// UseSkill()       실제 스킬 발동 로직 + 쿨다운 갱신. 자식 클래스에서 override 시 base.UseSkill() 호출 권장.
//                  → 호출하는 쪽에서 canUseSkill() 통과 확인 후 호출
// StopSkill()      스킬 중단/취소 로직. 자식 클래스에서 override.
// skillCoolDown    쿨다운 시간(초). 인스펙터에서 스킬별로 설정.
// ================================================================

public class Skill : MonoBehaviour
{
	[SerializeField]
	protected float skillCoolDown;



	private float lastSkillUseTime;


	protected virtual bool canUseSkill()
	{
		// 현재시간이 마지막사용시간+쿨다운을 아직 안 넘었으면(쿨다운이 안 끝났으면) 취소
		if (Time.time < lastSkillUseTime + skillCoolDown)
		{

			return false;
		}

		return true;
	}

	protected virtual void UseSkill()
	{
		lastSkillUseTime = Time.time;
		// 추가적인 스킬 로직은 자식 클래스에서 override
	}

	protected virtual void StopSkill()
	{

	}

}
