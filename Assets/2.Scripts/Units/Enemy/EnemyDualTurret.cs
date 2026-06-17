using UnityEngine;

// 고정 포탑. 총알+미사일 동시 사격.
public class EnemyDualTurret : EnemyTurretBase
{
	protected override void ShootWeapons()
	{
		weaponSystem.Shoot(PROJECTILE_TYPE.BULLET);
		weaponSystem.Shoot(PROJECTILE_TYPE.MISSILE);
	}
}
