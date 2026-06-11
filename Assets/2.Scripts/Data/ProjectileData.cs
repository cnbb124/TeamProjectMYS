using UnityEngine;

[CreateAssetMenu(fileName = "New Projectile Data", menuName ="Create Data/Projectile Data")]
public class ProjectileData : ItemData
{
	[Header("공통 수치")]
	[Tooltip("기본 데미지")]
	public int damage = 10;
	[Tooltip("비행 속도")]
	public float speed = 100f;

	[Tooltip("발사 속도")]
	public float launchSpeed = 10f;

	[Tooltip("최대 속도")]
	public float maxSpeed = 700f;

	[Header("변형탄 옵션")]
	[Tooltip("true면 아머 경감/차감 무시 (아머무시탄)")]
	public bool ignoreArmor = false;
	[Tooltip("실드에 주는 데미지 배율. 1 = 보통, 그 이상 = 실드 추가뎀배율")]
	public float shieldDamageMultiplier = 1f;

	[Header("미사일 전용 (총알이면 무시)")]
	public MISSILE_TYPE missileType;
	[Tooltip("폭발 피해 반경. 0이면 단발 판정")]
	public float explosionRadius = 8f;

	[Header("이펙트/사운드 종류")]
	public EFFECT_TYPE hitEffect;
}
