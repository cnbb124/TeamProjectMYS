using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// 첫 액티브 스킬 구현체. ActiveSkill.UseSkill()을 override해서
// warpSkillData.warpSequenceTime만큼 채널링 후 owner.transform.forward 방향으로
// warpDistance만큼 이동(Unit.Teleport() 사용). 채널링 타이머는 코루틴 없이
// Time.time 비교 방식(ActiveSkill.canUseSkill()의 쿨다운 계산과 동일한 "+" 스타일) —
// SkillSystem.Update()가 매 프레임 Tick()을 호출해서 시간이 됐는지 확인.
// 장애물/충돌 체크 없음 — 필요해지면 Raycast로 막힌 위치 보정 추가.
//
// 에디터 세팅: WarpSkillData(.asset, Create Data/Skill/Warp Skill Data) 에셋을 만들고
// SkillSystem.LearnSkill(그 데이터)을 호출하면 CreateInstance()를 통해 이 인스턴스가
// 자동 생성되어 슬롯에 등록됨. 컴포넌트를 직접 GameObject에 붙이는 단계는 없음.
// ================================================================

public class WarpSkill : ActiveSkill
{
	private WarpSkillData WarpSkillData
	{
		get
		{
			return _activeSkillData as WarpSkillData;
		}
	}

	private bool _isWarping;
	private float _warpStartTime;

	public WarpSkill(Unit owner, WarpSkillData skillData) : base(owner, skillData)
	{
	}

	protected override void UseSkill()
	{
		base.UseSkill();
		//워프인
		_vfx.PlayEffectAtUnit(WarpSkillData.warpInEffectType, _owner.transform, _owner.transform.position, _owner.transform.rotation, WarpSkillData.warpSequenceTime);
		//워프아웃
		_vfx.PlayEffectAtUnit
			(
				WarpSkillData.warpOutEffectType,
				_owner.transform,
				_owner.transform.position + _owner.transform.forward * WarpSkillData.warpDistance,
				_owner.transform.rotation,
				WarpSkillData.warpSequenceTime
			);
		_sound.PlaySFX3DAtUnit(WarpSkillData.warpSoundType, _owner.transform);

		_isWarping = true;
		_warpStartTime = Time.time;
	}

	public override void UpdateSkill()
	{
		if (!_isWarping)
		{
			return;
		}

		// 마지막 시작시간 + 채널링시간을 다 넘었으면(채널링이 끝났으면) 실제 워프 실행.
		if (Time.time < _warpStartTime + WarpSkillData.warpSequenceTime)
		{
			return;
		}

		_isWarping = false;
		Vector3 warpTargetPos = _owner.transform.position + _owner.transform.forward * WarpSkillData.warpDistance;
		_owner.Teleport(warpTargetPos);
	}
}
