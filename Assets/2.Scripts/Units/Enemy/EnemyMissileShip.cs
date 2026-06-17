using UnityEngine;

// 미사일만 사격하는 전투기.
public class EnemyMissileShip : Enemy
{
	protected override void ShootWeapons()
	{
		weaponSystem.Shoot(PROJECTILE_TYPE.MISSILE);
	}
}
