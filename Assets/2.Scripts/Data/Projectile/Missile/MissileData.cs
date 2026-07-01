using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 미사일 전용
[CreateAssetMenu(fileName = "New Missile Data", menuName = "Create Data/Item/Projectile Data/Missile")]
public class MissileData : ProjectileData
{
	[Tooltip("폭발 시 재생할 사운드 (Missile.Explode()에서 1번만 재생, 맞은 유닛 수와 무관). SFX_NONE(미등록)이면 무음.\n" +
		"※ 미사일은 hitSoundType이 따로 없음 — 실드 없는 유닛 피격음은 이 explosionSoundType이 담당함.")]
	public SOUND_TYPE explosionSoundType = SOUND_TYPE.SFX_NONE;

	[Tooltip("폭발 시 재생할 VFX (Missile.Explode()). 실드/비실드 구분 없이 폭발 1종.")]
	public EFFECT_TYPE explodeEffectType = EFFECT_TYPE.VFX_EXPLOSION_MISSILE;

	[Header("<size=18>미사일 설정</size>")]
	[Tooltip("초당 최대 선회 각도 (도/초). 클수록 날카롭게 꺾음.")]
	public float turnRate = 120f;
	[Tooltip("발사 직후 직진 유지 거리. 이 거리 전엔 유도(Steer) 안 하고 직진만 함.")]
	public float straightFlightDistance = 5.0f;
	[Tooltip("비례항법 계수 (1~5). 클수록 예측 추적 강화. 3 권장.")]
	public float navGain = 3f;
	[Tooltip("발사 시 기체 속도(curSpeed)에 더해질 추가 시작 속도. 최종 시작속도 = clamp(curSpeed + 이 값, 1, maxSpeed).")]
	public float launchSpeedBonus = 10f;
	[Tooltip("최대 도달 속도")]
	public float maxSpeed = 500f;
	[Tooltip("최고 속도 도달까지 걸리는 시간 (초).")]
	public float accelerateTime = 0.8f;
	[Tooltip("Missile의 실제 피해 범위. 변경시 이펙트 크기도 같이 변경됨.")]
	public float explosionRadius = 8f;
	[Tooltip("VFXManager에 연결된 폭발이펙트용 파티클 원본의 범위 입력. 원본값 입력 후 수정X.")]
	public float vfxBaseRadius = 8f;

}
