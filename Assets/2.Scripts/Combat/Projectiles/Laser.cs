using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Laser : Projectile
{
	protected override void Awake()
	{
		base.Awake();
		dmgType = DAMAGE_TYPE.LASER;
	}
	// Start is called before the first frame update
	protected override void Start()
	{
		
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
		OnHit(other);
	}
}
