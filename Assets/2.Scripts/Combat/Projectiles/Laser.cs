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
	

	// Update is called once per frame
	protected override void Update()
	{
		base.Update();
	}

}
