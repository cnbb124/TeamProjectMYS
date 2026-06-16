using UnityEngine;

// 미사일만 사격하는 전투기. OnAIAttack에서 MISSILE만 발사.
public class EnemyMissileShip : Enemy
{
	protected override void OnAIAttack()
	{
		base.OnAIAttack();
		weaponSystem.Shoot(PROJECTILE_TYPE.MISSILE);
	}
}
