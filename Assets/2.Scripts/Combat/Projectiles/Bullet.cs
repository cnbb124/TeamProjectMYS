using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//기본총알
//넉백없음

public class Bullet : Projectile
{
	/// <summary>
	/// 총알 속도.
	/// </summary>
	private float _speed;
	[Header("<size=18>총알 설정(출력용 인스펙터수정X)</size>")]
	[Header("투사체 데이터(SO)")]
	//[HideInInspector]
	public BulletData bulletData;




	protected override void Awake()
	{
		base.Awake();

		dmgType = DAMAGE_TYPE.BULLET;
		projectileType = PROJECTILE_TYPE.BULLET;
	}
	
	

	
	protected override void Update()
	{
		// 일시정지/게임오버 중엔 이동·사거리 판정 정지 (재개 시 그 자리서 계속)
		if (GameManager.Instance != null && GameManager.Instance.IsGameplayFrozen) return;
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
			hitVfxType = bulletData.hitEffectType;
			shieldHitVfxType = bulletData.shieldHitEffectType;
			ignoreArmor = bulletData.ignoreArmor;
			shieldDamageMultiplier = bulletData.shieldDamageMultiplier;
		}
	}

	// bulletData가 정한 speed를 발사 시점에 덮어씀 — 보스 탄막 패턴처럼 같은 데이터로 포인트별 속도만 다르게 줄 때 사용.
	// 반드시 Init() 이후에 호출(Init이 bulletData.speed로 _speed를 세팅하므로).
	public void OverrideSpeed(float speed)
	{
		_speed = speed;
	}



	
	/// <summary>
	/// 온트리거에 쓸 재정의함수
	/// </summary>
	/// <param name="other"></param>
	protected override void OnHit(HitTarget hit)
	{
		base.OnHit(hit);

		// 데미지를 안 받는 환경 오브젝트 — 피격 반응(사운드/VFX)만 위임.
		// 데미지 대상은 아래 ApplyDamage → TakeDamage → OnHitReaction이 실드 분기까지 처리함.
		if (hit.damageable == null)
		{
			hit.hittable.OnHitReaction(BuildHitInfo(hit));
		}

		//공통 데미지 함수 호출 (단일 대상)
		ApplyDamage(hit, this.curDamage, this.dmgType);

		// 이펙트 및 데미지 연산 후 투사체 소멸
		ReturnToPool();
	}

}
