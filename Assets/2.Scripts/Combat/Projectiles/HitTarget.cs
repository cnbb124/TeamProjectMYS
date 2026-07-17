using UnityEngine;

// 투사체가 무엇을 맞았는지 한 번만 조회해서 담아두는 묶음.
// OnTriggerEnter에서 만들어 OnHit → ApplyDamage까지 그대로 흘려보냄.
// (예전엔 단계마다 GetComponentInParent를 다시 불러서 같은 조회를 3번 반복했음)
//
// HitInfo와 구분할 것 — HitInfo는 '무슨 피해/효과를 주는가'를 담아 대상에게 넘기는 값이고(네트워크로도 감),
// 이건 '누가 맞았나'를 담아 투사체 안에서만 도는 참조 묶음임.
public readonly struct HitTarget
{
	// 실제로 맞은 히트박스 콜라이더
	public readonly Collider collider;

	// 피격 반응(사운드/VFX) 대상. HitTarget이 만들어졌다면 항상 있음
	public readonly IHittable hittable;

	// 데미지를 받는 대상. 환경 오브젝트처럼 데미지를 안 받는 대상이면 null
	public readonly IDamageable damageable;

	// 피격 지점(투사체 위치에서 가장 가까운 콜라이더 표면)
	public readonly Vector3 point;

	// 구조체 생성자
	public HitTarget(Collider collider, IHittable hittable, Vector3 point)
	{
		this.collider = collider;
		this.hittable = hittable;
		// IDamageable이 IHittable을 상속하므로, 추가 조회 없이 캐스트만으로 데미지 대상인지 갈림
		this.damageable = hittable as IDamageable;
		this.point = point;
	}
}
