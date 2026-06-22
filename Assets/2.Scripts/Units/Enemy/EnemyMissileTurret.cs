using UnityEngine;

// 고정 포탑. 미사일만 사격. 전탄 발사 후 reloadDuration 동안 RELOAD 상태.
public class EnemyMissileTurret : EnemyTurretBase
{
	[Header("<size=18>미사일 포탑 전용</size>")]
	[Tooltip("전탄 발사 후 재장전 대기 시간 (초).")]
	public float reloadDuration = 5f;

	protected override void ShootWeapons()
	{
		if (!CanFireMissile()) return;
		weaponSystem.Shoot(PROJECTILE_TYPE.MISSILE);
		EnterReload(reloadDuration);
	}
}
