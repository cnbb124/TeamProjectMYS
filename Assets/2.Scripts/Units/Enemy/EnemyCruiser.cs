using UnityEngine;

// 총알+미사일 동시 사격하는 순양함급.
public class EnemyCruiser : Enemy
{
	protected override void ShootWeapons()
	{
		weaponSystem.Shoot(PROJECTILE_TYPE.BULLET);
		weaponSystem.Shoot(PROJECTILE_TYPE.MISSILE);
	}
}
