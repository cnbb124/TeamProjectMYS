using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 총알 전용
[CreateAssetMenu(fileName = "New Bullet Data", menuName = "Create Data/Item/Projectile Data/Bullet")]
public class BulletData : ProjectileData
{
	[Tooltip("총알 날아가는 속도")]
	public float speed;

	[Tooltip("실제 발사될 총알 프리팹이 등록된 풀 종류. WeaponSystem이 이 값으로 PoolManager.GetProjectile(POOL_TYPE) 호출.")]
	public POOL_TYPE curBulletPoolType;
}
