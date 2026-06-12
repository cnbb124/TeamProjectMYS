// 공통 베이스
using UnityEngine;

public abstract class ProjectileData : ItemData
{
	[Header("공통 전투 수치")]
	[Tooltip("기본 데미지")]
	public int damage = 10;
	[Tooltip("최대 사거리")]
	public float maxRange = 1000f;
	[Tooltip("관통탄 여부")]
	public bool ignoreArmor;
	[Tooltip("실드 추가뎀 비율")]
	public float shieldDamageMultiplier = 1f;
	[Tooltip("피격시 데미지의 종류")]
	public DAMAGE_TYPE damageType;
	[Tooltip("피격시 VFX매니저에서 실행할 이펙트 종류")]
	public EFFECT_TYPE hitEffect;     // VFX_BULLETHIT 등

}



