using UnityEngine;

// 발동(쿨다운/사용횟수)이 있는 액티브 스킬 전용 데이터. requiredLevel/requiredAffinity는 SkillData에서 상속.
public abstract class ActiveSkillData : SkillData
{
	[Tooltip("쿨다운 시간(초).")]
	public float skillCoolDown;

	[Tooltip("최대 사용 횟수. 0 이하면 횟수 제한 없음(쿨다운만 적용).")]
	public int maxUseCount;
}
