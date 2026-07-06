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



	
	/// <summary>
	/// 온트리거에 쓸 재정의함수
	/// </summary>
	/// <param name="other"></param>
	protected override void OnHit(Collider other)
	{
		base.OnHit(other);

		IDamageable dmg = other.GetComponentInParent<IDamageable>();
		if (dmg == null)
		{
			// 데미지를 안 받는 대상.
			//  - IHittable(피격 반응 있는 환경)이면 그쪽에 위임(자기 피격 이펙트).
			//  - 아무것도 없는 순수 대상(벽 등)이면 폴백으로 일반 히트 VFX.
			IHittable hittable = other.GetComponentInParent<IHittable>();
			if (hittable != null)
			{
				hittable.OnHitReaction(BuildHitInfo(other));
			}
			else
			{
				VFXManager.Instance.PlayEffectAtPosition(hitVfxType, other.ClosestPoint(transform.position), Quaternion.identity);
			}
		}
		// 데미지 대상(dmg != null)의 피격 VFX/사운드는 아래 ApplyDamage → TakeDamage → OnHitReaction이 실드 분기까지 처리.

		//공통 데미지 함수 호출 (단일 대상)
		ApplyDamage(other, this.curDamage, this.dmgType);

		// 이펙트 및 데미지 연산 후 투사체 소멸
		ReturnToPool();
	}

	// 환경(IHittable, 데미지 안 받음) 대상에 넘길 피격 정보 구성. 데미지 관련 필드는 안 씀.
	private HitInfo BuildHitInfo(Collider other)
	{
		Vector3 hitPos = other.ClosestPoint(transform.position);
		return new HitInfo
		{
			type = dmgType,
			hitPosition = hitPos,
			hitDiriection = (hitPos - transform.position).normalized,
			attacker = attacker != null ? attacker.gameObject : null,
			hitVfxType = this.hitVfxType,
			shieldHitVfxType = this.shieldHitVfxType,
		};
	}

}
