using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DumbMissile : Missile
{
	/// <summary>
	/// 최대사거리 도달시 폭발용
	/// </summary>
	protected override void OnMaxRange()
	{
		Explode(explosionInfo);
		base.OnMaxRange();
	}
}
