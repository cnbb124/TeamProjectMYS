using UnityEngine;

// 고정 포탑. 미사일만 사격.
public class EnemyMissileTurret : EnemyTurretBase
{
	protected override void ShootWeapons()
	{
		weaponSystem.Shoot(PROJECTILE_TYPE.MISSILE);
	}
}
