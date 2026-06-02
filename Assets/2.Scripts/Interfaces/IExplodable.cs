using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct ExplosionInfo
{
	public float explosionRadius;//폭발반경
	public int explosionDamage;//폭발피해
	
	
}
public interface IExplodable
{
	//폭발시 호출할 함수
	void Explode(ExplosionInfo info);

}
