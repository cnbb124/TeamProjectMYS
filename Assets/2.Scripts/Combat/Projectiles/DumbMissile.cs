using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DumbMissile : Missile
{

	protected override void OnMaxRange()
	{
		Explode(explosionInfo);
		base.OnMaxRange();
	}

}
