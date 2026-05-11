using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//기본총알
//넉백없음

public class Bullet : Projectile
{
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
		//기본 업데이트 실행(사거리 업뎃)
		base.Update();
		//이동 로직
		transform.Translate(Vector3.forward * speed * Time.deltaTime);
	}



	
	//온트리거에 쓸 재정의함수
	protected override void OnHit(Collider other)
	{
		// 이펙트 출력 로직추가 

		// 부모의 공통 데미지 함수 호출 (단일 대상)
		ApplyDamage(other, this.curDamage, this.dmgType);

		// 이펙트 및 데미지 연산 후 투사체 소멸
		ReturnToPool();
	}

}
