using UnityEngine;

// 총알+미사일 동시 사격하는 순양함급. OnAIAttack에서 BULLET+MISSILE 발사.
public class EnemyCruiser : Enemy
{
	protected override void OnAIAttack()
	{
		base.OnAIAttack();
		weaponSystem.Shoot(PROJECTILE_TYPE.BULLET);
		weaponSystem.Shoot(PROJECTILE_TYPE.MISSILE);
	}
}
