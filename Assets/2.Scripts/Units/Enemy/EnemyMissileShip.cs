using UnityEngine;

// 미사일만 사격하는 전투기.
public class EnemyMissileShip : EnemyShip
{
	protected override void ShootWeapons()
	{
		weaponSystem.Shoot(PROJECTILE_TYPE.MISSILE);
	}

	// ATTACK_PASS 중에도 미사일 발사 유지.
	protected override void ShootWeaponsOnPass()
	{
		weaponSystem.Shoot(PROJECTILE_TYPE.MISSILE);
	}
}
