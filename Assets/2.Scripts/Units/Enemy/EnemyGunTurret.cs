using UnityEngine;

// 고정 포탑. 총알만 사격.
public class EnemyGunTurret : EnemyTurretBase
{
	protected override void ShootWeapons()
	{
		weaponSystem.Shoot(PROJECTILE_TYPE.BULLET);
	}
}
