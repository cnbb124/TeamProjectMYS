using UnityEngine;

// 총알만 사격하는 경전투기.
public class EnemyGunShip : EnemyShip
{
	protected override void ShootWeapons()
	{
		weaponSystem.Shoot(PROJECTILE_TYPE.BULLET);
	}
}
