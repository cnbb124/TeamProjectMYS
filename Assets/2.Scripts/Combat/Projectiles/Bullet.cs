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
	private float _speed;
	[Header("<size=22>총알 설정</size>")]
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
		transform.Translate(Vector3.forward * _speed * Time.deltaTime);
		//기본 업데이트 실행(사거리 업뎃)
		base.Update();
		
	}

	public override void Init(Vector3 startPos, Vector3 dir, Unit attacker)
	{
		base.Init(startPos, dir, attacker);
		if(bulletData!=null)
		{
			_speed = bulletData.speed;
			baseDamage = bulletData.damage;
			maxRange = bulletData.maxRange;
			hitSoundType = bulletData.hitSoundType;
			ignoreArmor = bulletData.ignoreArmor;
			shieldDamageMultiplier = bulletData.shieldDamageMultiplier;
		}
	}



	
	/// <summary>
	/// 온트리거에 쓸 재정의함수
	/// </summary>
	/// <param name="other"></param>
	protected override void OnHit(Collider other)
	{
		base.OnHit(other);
		// 피격 VFX 출력 (Missile.Explode()와 동일하게 하드코딩 타입 사용). 사운드는 Unit.OnHitReaction이 담당.
		VFXManager.Instance.PlayEffectAtPosition(EFFECT_TYPE.VFX_BULLETHIT, other.ClosestPoint(transform.position), Quaternion.identity);

		//공통 데미지 함수 호출 (단일 대상)
		ApplyDamage(other, this.curDamage, this.dmgType);

		// 이펙트 및 데미지 연산 후 투사체 소멸
		ReturnToPool();
	}

}
