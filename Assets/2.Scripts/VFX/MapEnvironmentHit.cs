using UnityEngine;

// 맵에 배치하는 환경 오브젝트(벽/구조물 등)의 피격 반응.
// 데미지는 안 받고 피격 효과(VFX/사운드)만 냄 — 그래서 IDamageable이 아니라 IHittable만 구현함.
// (파괴 가능한 오브젝트는 cs_Map_Asteroid처럼 IDamageable을 구현할 것)
//
// [에디터 세팅]
//   1. 이 스크립트를 환경 오브젝트 루트에 부착
//   2. 자식에 피격판정용 트리거 콜라이더를 두고 레이어를 HitBox로 지정
//      (투사체는 HitBox 레이어만 감지함 — Projectile.OnTriggerEnter 참고)
//   3. 비행기가 통과 못 하게 할 실제 콜라이더는 별도로 둘 것(트리거 아님)
public class MapEnvironmentHit : MonoBehaviour, IHittable
{
	[Tooltip("Sound Override가 ON일 때 재생할 피격음. SFX_NONE이면 무음(소리 안 남).\n" +
		"OFF면 이 값은 무시되고 투사체(탄종)가 준 피격음을 씀.")]
	[SerializeField]
	private SOUND_TYPE _hitSoundOverride = SOUND_TYPE.SFX_NONE;
	[Tooltip("피격음을 이 오브젝트가 직접 지정할지 여부.\n" +
		"OFF: 투사체(탄종)가 준 피격음을 그대로 사용.\n" +
		"ON : Hit Sound Override 값을 사용(SFX_NONE으로 두면 이 오브젝트만 무음).")]
	[SerializeField]
	private bool _soundOverride = false;
	[Header("직선 시야 차단 (레이저 관통 + 락온 차폐 공통)")]
	[Tooltip("벽/구조물이면 켤 것. 끄면 빔이 관통하고 뒤에 있는 적도 락온됨.")]
	[SerializeField]
	private bool _blocksLineOfSight = true;

	public bool BlocksLineOfSight => _blocksLineOfSight;
	
	// 투사체가 HitInfo에 실어준 피격 지점/VFX 종류로 반응만 냄. 데미지 필드는 안 씀.
	public void OnHitReaction(HitInfo info)
	{
		if (VFXManager.Instance != null)
		{
			VFXManager.Instance.PlayEffectAtPosition(info.hitVfxType, info.hitPosition, Quaternion.identity);
		}
		SOUND_TYPE hitSound = _soundOverride ? _hitSoundOverride : info.hitSoundType;
		
		if (hitSound != SOUND_TYPE.SFX_NONE && SoundManager.Instance != null)
		{
			SoundManager.Instance.PlaySFX3DAtPosition(hitSound, info.hitPosition);
		}
	}
}
