using UnityEngine;

// 총알만 사격하는 경전투기. OnAIAttack에서 BULLET만 발사.
public class EnemyGunShip : Enemy
{
	protected override void OnAIAttack()
	{
		base.OnAIAttack();
		weaponSystem.Shoot(PROJECTILE_TYPE.BULLET);
	}
}
