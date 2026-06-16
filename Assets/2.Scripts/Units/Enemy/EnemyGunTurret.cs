using UnityEngine;

// 고정 포탑. 총알만 사격. Start에서 이동속도/순찰범위 0으로 강제해 제자리 고정.
public class EnemyGunTurret : Enemy
{
	protected override void Start()
	{
		// 터렛이라 이동속도 0, 순찰범위 0
		base.Start();
		baseMoveSpeed = 0f;
		maxSpeed = 0f;
		patrolRadius = 0f;
	}

	protected override void OnAIAttack()
	{
		base.OnAIAttack();
		weaponSystem.Shoot(PROJECTILE_TYPE.BULLET);
	}
}
