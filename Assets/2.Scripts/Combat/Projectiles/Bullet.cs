using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


//기본총알
//넉백없음

public class Bullet : Projectile
{
	/// <summary>
	/// 총알 속도.
	/// </summary>
	private float speed;
	[Header("<size=18>총알 설정</size>")]
	[Header("투사체 데이터(SO)")]
	public BulletData bulletData;




	protected override void Awake()
	{
		base.Awake();

		dmgType = DAMAGE_TYPE.BULLET;
		projectileType = PROJECTILE_TYPE.BULLET;
	}
	
	

	
	protected override void Update()
	{
		//이동 로직
		transform.Translate(Vector3.forward * speed * Time.deltaTime);
		//기본 업데이트 실행(사거리 업뎃)
		base.Update();
		
	}

	public override void Init(Vector3 startPos, Vector3 dir, Unit attacker)
	{
		base.Init(startPos, dir, attacker);
		if(bulletData!=null)
		{
			speed = bulletData.speed;
			baseDamage = bulletData.damage;
			maxRange = bulletData.maxRange;
			// ignoreArmor, shieldDamageMultiplier는 DamageInfo 생성 시 사용 (ApplyDamage 쪽)
		}
	}



	
	/// <summary>
	/// 온트리거에 쓸 재정의함수
	/// </summary>
	/// <param name="other"></param>
	protected override void OnHit(Collider other)
	{
		base.OnHit(other);
		// 이펙트 출력 (사운드,파티클)로직추가 

		
		//공통 데미지 함수 호출 (단일 대상)
		ApplyDamage(other, this.curDamage, this.dmgType);

		// 이펙트 및 데미지 연산 후 투사체 소멸
		ReturnToPool();
	}

}
