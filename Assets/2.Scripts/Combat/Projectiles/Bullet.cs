using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//기본총알
//넉백없음

public class Bullet : Projectile
{
	//스피드설정
	[Header("총알 속도 설정")]
	public float speed;


	protected override void Awake()
	{
		base.Awake();

		dmgType = DAMAGE_TYPE.BULLET;
		projectileType = PROJECTILE_TYPE.BULLET;
	}
	// Start is called before the first frame update
	

	// Update is called once per frame
	protected override void Update()
	{
		//이동 로직
		transform.Translate(Vector3.forward * speed * Time.deltaTime);
		//기본 업데이트 실행(사거리 업뎃)
		base.Update();
		
	}



	
	/// <summary>
	/// 온트리거에 쓸 재정의함수
	/// </summary>
	/// <param name="other"></param>
	protected override void OnHit(Collider other)
	{
		base.OnHit(other);
		// 이펙트 출력 (사운드,파티클)로직추가 

		SoundManager.Instance.PlaySFX3DAtPosition(SOUND_TYPE.SFX_BULLETHIT, this.transform.position);
		//공통 데미지 함수 호출 (단일 대상)
		ApplyDamage(other, this.curDamage, this.dmgType);

		// 이펙트 및 데미지 연산 후 투사체 소멸
		ReturnToPool();
	}

}
