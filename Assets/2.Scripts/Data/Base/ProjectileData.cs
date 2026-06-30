// 공통 베이스
using UnityEngine;

public abstract class ProjectileData : ItemData
{
	[Header("<size=18>공통 전투 수치</size>")]
	[Tooltip("기본 데미지")]
	public int damage = 10;
	[Tooltip("최대 사거리")]
	public float maxRange = 1000f;
	[Tooltip("관통탄 여부")]
	public bool ignoreArmor;
	[Tooltip("실드 추가뎀 비율")]
	public float shieldDamageMultiplier = 1f;
	[Header("<size=18>타입 설정</size>")]
	[Tooltip("피격시 데미지의 종류")]
	public DAMAGE_TYPE damageType;
	[Tooltip("실제 발사될 투사체(총알/미사일) 프리팹이 등록된 풀 종류. WeaponSystem/Skill이 이 값으로 PoolManager.GetProjectile(POOL_TYPE) 호출.")]
	public POOL_TYPE curProjectilePoolType;
	[Tooltip("피격시 VFX매니저에서 실행할 이펙트 종류")]
	public EFFECT_TYPE hitEffectType;     // VFX_BULLETHIT 등
	[Tooltip("발사 시 총구에서 재생할 머즐플래시 이펙트. 기본값은 일반 총알 머즐.")]
	public EFFECT_TYPE muzzleEffectType = EFFECT_TYPE.VFX_BULLET_MUZZLE;

	[Header("<size=18>사운드 설정</size>")]
	[Tooltip("발사 시 재생할 사운드. SFX_NONE(미등록)이면 무음")]
	public SOUND_TYPE shootSoundType = SOUND_TYPE.SFX_NONE;
	
	// hitSoundType은 여기 없음 — Bullet만 의미 있음(BulletData에 따로 있음).
	// Missile은 Explode()의 explosionSoundType이 그 역할을 대신함(MissileData 참고). 같이 노출하면 중복재생 유발해서 베이스에서 뺌.
	// 실드 피격음(shieldHitSoundType)도 여기 없음 — 탄종별로 안 나누고 Unit.GetPlaySoundTypeShield(DamageInfo)에서 DAMAGE_TYPE 기준으로 일괄 처리.

}



