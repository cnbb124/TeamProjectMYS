using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// 첫 액티브 스킬 구현체. ActiveSkill.UseSkill()을 override해서
// owner.transform.forward 방향으로 warpSkillData.warpDistance만큼 즉시 이동(Unit.Teleport() 사용).
// 장애물/충돌 체크 없음 — 필요해지면 Raycast로 막힌 위치 보정 추가.
//
// 에디터 세팅: WarpSkillData(.asset, Create Data/Skill/Warp Skill Data) 에셋을 만들고
// SkillSystem.LearnSkill(그 데이터)을 호출하면 CreateInstance()를 통해 이 인스턴스가
// 자동 생성되어 슬롯에 등록됨. 컴포넌트를 직접 GameObject에 붙이는 단계는 없음.
// ================================================================

public class WarpSkill : ActiveSkill
{
	private WarpSkillData warpSkillData
	{
		get
		{
			return _activeSkillData as WarpSkillData;
		}
	}

	public WarpSkill(Unit owner, WarpSkillData skillData) : base(owner, skillData)
	{
	}

	protected override void UseSkill()
	{
		base.UseSkill();

		Vector3 warpTargetPos = _owner.transform.position + _owner.transform.forward * warpSkillData.warpDistance;
		_owner.Teleport(warpTargetPos);
	}
}
