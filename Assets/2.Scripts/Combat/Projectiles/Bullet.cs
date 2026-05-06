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
	}
	// Start is called before the first frame update
	protected override void Start()
	{
		
	}

	// Update is called once per frame
	protected override void Update()
	{
		//기본 업데이트 실행
		base.Update();
		//이동 로직
		transform.Translate(Vector3.forward * speed * Time.deltaTime);
	}


	private void OnTriggerEnter(Collider other)
	{
		//공격자가 없는거일시 무시
		if (attacker == null)
		{
			return;
		}
		//부딪힌놈 레이어랑 발사자의 레이어가 같으면. 즉 같은팀일시. 무시
		if(other.gameObject.layer==attacker.gameObject.layer)
		{
			return;
		}

		//
		OnHit(other);
	}

}
