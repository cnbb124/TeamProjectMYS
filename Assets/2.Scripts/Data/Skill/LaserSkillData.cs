using UnityEngine;

// 레이저 스킬 데이터.
// 발동 → chargeTime 동안 차지 → 전방(owner.forward)으로 beamDuration 동안 빔 유지.
// 빔 유지 중 damageInterval마다 전방 직선 판정으로 사거리 내 적 전원에게 지속 데미지(관통).
[CreateAssetMenu(fileName = "New Laser Skill Data", menuName = "Create Data/Skill/Laser Skill Data")]
public class LaserSkillData : ActiveSkillData
{
	[Header("<size=16>차지 / 지속</size>")]
	[Tooltip("발동 후 빔이 나가기까지 모으는 시간(초)")]
	public float chargeTime = 0.5f;
	[Tooltip("빔이 유지되는 시간(초)")]
	public float beamDuration = 2f;

	[Header("<size=16>빔 판정</size>")]
	[Tooltip("빔 사거리(전방 직선)")]
	public float range = 700f;
	[Tooltip("빔 두께(판정 반경). SphereCast 반경 = 이 값. 빔 시각 두께(LineRenderer width)도 이 값의 2배(지름)로 자동 설정됨.")]
	public float beamRadius = 1f;
	[Tooltip("데미지 판정 1회(틱)당 데미지")]
	public int damagePerTick = 10;
	[Tooltip("데미지 판정 간격(초). 작을수록 촘촘하지만 부하↑. 0.1~0.3 권장")]
	public float damageInterval = 0.2f;
	[Tooltip("관통(방어력 무시) 여부")]
	public bool ignoreArmor = false;
	[Tooltip("실드에 주는 데미지 배율")]
	public float shieldDamageMultiplier = 1f;

	[Header("<size=16>이펙트</size>")]
	[Tooltip("차지 중 재생할 VFX(owner 부착, chargeTime 동안). VFXManager.vfxConfigs에 등록 필요")]
	public EFFECT_TYPE chargeEffectType;
	[Tooltip("빔 지속 중 재생할 VFX(owner 부착, beamDuration 동안). 전방으로 뻗는 빔 프리팹")]
	public EFFECT_TYPE beamEffectType;
	[Tooltip("적 피격(실드 없을 때) VFX")]
	public EFFECT_TYPE hitEffectType = EFFECT_TYPE.VFX_SKILL_LASER_HIT;
	[Tooltip("적 피격(실드에 막힘) VFX")]
	public EFFECT_TYPE shieldHitEffectType = EFFECT_TYPE.VFX_BULLET_HIT_SHIELD;

	[Header("<size=16>사운드</size>")]
	[Tooltip("차지 시작 사운드. SFX_NONE이면 무음")]
	public SOUND_TYPE chargeSoundType = SOUND_TYPE.SFX_NONE;
	[Tooltip("빔 발사 사운드. SFX_NONE이면 무음")]
	public SOUND_TYPE fireSoundType = SOUND_TYPE.SFX_NONE;

	public override Skill CreateSkill(Unit owner)
	{
		return new LaserSkill(owner, this);
	}
}
