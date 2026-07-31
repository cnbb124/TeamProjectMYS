using UnityEngine;

// WarpSkill 전용 데이터. skillCoolDown/maxUseCount는 ActiveSkillData에서 상속.
[CreateAssetMenu(fileName = "New Warp Skill Data", menuName = "Create Data/Skill/Warp Skill Data")]
public class WarpSkillData : ActiveSkillData
{
	[Tooltip("워프 이동 거리.")]
	public float warpDistance = 50f;
	[Tooltip("워프하는데 걸리는 시간")]
	public float warpSequenceTime = 1f;
	[Tooltip("워프 사운드 SFX 재생 딜레이시간")]
	public float warpSoundDelayTime = 1f;
	[Tooltip("워프 시 출력될 VFX 타입. 실제 출력할 프리팹은 VFXManager.vfxConfigs에 등록.")]
	public EFFECT_TYPE warpInEffectType;
	public EFFECT_TYPE warpOutEffectType;
	[Tooltip("워프 시 재생될 사운드 타입. 실제 클립은 SoundManager에 등록.")]
	public SOUND_TYPE warpSoundType;

	


	public override Skill CreateSkill(Unit owner)
	{
		return new WarpSkill(owner, this);
	}
}
