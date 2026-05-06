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


	protected override void Awake()
	{
		base.Awake();
		dmgType = DAMAGE_TYPE.EXPLOSION;
		
	}
	// Start is called before the first frame update
	protected override void Start()
	{
		base.Start();
		explosionInfo.explosionDamage = this.damage;
		explosionInfo.explosionRadius = this.explosionRadius;
	}

	// Update is called once per frame
	protected override void Update()
	{
		base.Update();
	}

	private void OnTriggerEnter(Collider other)
	{
		//공격자가 없는거일시 무시
		if (attacker == null)
		{
			return;
		}
		//부딪힌놈 레이어랑 발사자의 레이어가 같으면. 즉 같은팀일시. 무시
		if (other.gameObject.layer == attacker.gameObject.layer)
		{
			return;
		}
		//
		Explode(explosionInfo);

	}

	public virtual void Explode(ExplosionInfo explosionInfo)
	{
		//맞은것들의 충돌박스 싹다가져오기
		Collider[] hits = Physics.OverlapSphere(transform.position, explosionInfo.explosionRadius);

		//맞은것들
		foreach (Collider hit in hits)
		{

			IDamageable target = hit.GetComponent<IDamageable>();
			//데미지를 받지않는애면 건뛰
			if (target == null)
			{
				continue;
			}
			//데미지를 받는애면
			//데미지인포구조체 임시생성
			DamageInfo damageInfo = new DamageInfo
			{
				//피해유형
				type = dmgType,
				//데미지수치
				damageAmount = explosionInfo.explosionDamage,
				//크리티컬여부
				isCritical = critical,
				//맞은좌표=현재오브젝트(투사체,projectile,this)좌표
				hitPosition = transform.position,
				//맞은방향=현재오브젝트(투사체,projectile,this)의 앞방향에서. 폭발이라 조금더 다르게
				hitDiriection = (hit.transform.position - transform.position).normalized,
				//공격자정보=현재오브젝트의 gameobject
				attacker = this.attacker.gameObject
			};

			target.TakeDamage(damageInfo);
		}
	}
}
