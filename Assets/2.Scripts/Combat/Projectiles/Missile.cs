using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

//미사일
//폭발형. 범위(스플)데미지. barrel때를 참고.
//락온가능. 유도성능있음.
public class Missile : Projectile, IExplodable
{

	public ExplosionInfo explosionInfo;
	//const string sMissileHeader = "<size=20>["+"미사일 설정"+"]</size>";
	[Space(5)]
	[Header("<size=18>[미사일 설정]</size>")]
	[Header("폭발 범위 세팅")]
	public float explosionRadius;
	[Header("유도 각도 세팅")]
	public float chaseMax;//차후 이름 변경필요
	[Header("락온 목표")]
	public Transform targetTr;

	protected override void Awake()
	{
		base.Awake();
		dmgType = DAMAGE_TYPE.EXPLOSION;
		projectileType = PROJECTILE_TYPE.MISSILE;

	}

	// Update is called once per frame
	protected override void Update()
	{
		base.Update();//최대사거리로직
					  //이동로직, 추적로직 추가필요
	}
	// Start is called before the first frame update

	public override void Init(Vector3 startPos, Vector3 dir, Unit attacker)
	{
		base.Init(startPos, dir, attacker);

		// 풀에서 꺼낼 때마다 인스펙터의 최신 damage 값으로 갱신
		explosionInfo.explosionDamage = this.curDamage;
		explosionInfo.explosionRadius = this.explosionRadius;
	}




	//온트리거에 쓸 재정의함수

	protected override void OnHit(Collider other)
	{
		// 폭발 실행 후 투사체 소멸
		Explode(explosionInfo);
		ReturnToPool();
	}


	public void Explode(ExplosionInfo explosionInfo)
	{
		//이펙트 출력 로직 추가

		//맞은것들의 충돌박스 싹다가져오기
		Collider[] hits = Physics.OverlapSphere(transform.position, explosionInfo.explosionRadius);


		// 중복 타격 방지를 위한 HashSet 추가//차후 확인및보강필요
		HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();


		//맞은것들 전부처리
		foreach (Collider hit in hits)
		{
			IDamageable target = hit.GetComponent<IDamageable>();

			if (target != null && !damagedTargets.Contains(target))
			{
				ApplyDamage(hit, explosionInfo.explosionDamage, this.dmgType);
				damagedTargets.Add(target); // 타격 대상 기록 차후확인및보강필요
			}
		}
	}
}
