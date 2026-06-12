using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 총알 전용
[CreateAssetMenu(fileName = "New Bullet Data", menuName = "Create Data/Item/Projectile Data/Bullet")]
public class BulletData : ProjectileData
{
	[Tooltip("총알 날아가는 속도")]
	public float speed;
}
