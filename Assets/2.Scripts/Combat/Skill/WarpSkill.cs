using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// 첫 액티브 스킬 구현체. ActiveSkill.UseSkill()을 override해서
// owner.transform.forward 방향으로 warpSkillData.warpDistance만큼 즉시 이동(Unit.Teleport() 사용).
// 장애물/충돌 체크 없음 — 필요해지면 Raycast로 막힌 위치 보정 추가.
//
// 에디터 세팅: 이 컴포넌트를 Player(또는 스킬 보유 유닛)의 자식 오브젝트에 부착하고,
// skillData 슬롯에 WarpSkillData(.asset, Create Data/Skill/Warp Skill Data) 연결.
// Player.InitSkillSlots()가 Start()에서 자동으로 찾아 skillSlot에 등록함.
// ================================================================

public class WarpSkill : ActiveSkill
{
	private WarpSkillData warpSkillData
	{
		get
		{
			return activeSkillData as WarpSkillData;
		}
	}

	protected override void UseSkill()
	{
		base.UseSkill();

		Vector3 warpTargetPos = owner.transform.position + owner.transform.forward * warpSkillData.warpDistance;
		owner.Teleport(warpTargetPos);
	}
}
