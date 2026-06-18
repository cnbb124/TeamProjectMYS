using UnityEngine;

// 총알+미사일 동시 사격하는 순양함급.
public class EnemyFighterShip : EnemyShip
{
	protected override void ShootWeapons()
	{
		weaponSystem.Shoot(PROJECTILE_TYPE.BULLET);
		weaponSystem.Shoot(PROJECTILE_TYPE.MISSILE);
	}

	// ATTACK_PASS 중에는 총알 제외, 미사일만 발사.
	protected override void ShootWeaponsOnPass()
	{
		weaponSystem.Shoot(PROJECTILE_TYPE.MISSILE);
	}
}
