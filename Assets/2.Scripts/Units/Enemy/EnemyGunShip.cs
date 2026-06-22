using UnityEngine;

// 총알만 사격하는 경전투기.
public class EnemyGunShip : EnemyShip
{
	protected override void ShootWeapons()
	{
		weaponSystem.Shoot(PROJECTILE_TYPE.BULLET);
	}

	// ATTACK_PASS 중에도 타겟을 지나치기 전까지는 계속 사격.
	protected override void ShootWeaponsOnPass()
	{
		weaponSystem.Shoot(PROJECTILE_TYPE.BULLET);
	}
}
