using UnityEngine;

// 고정 포탑. 미사일만 사격. Start에서 이동속도/순찰범위 0으로 강제해 제자리 고정.
public class EnemyMissileTurret : Enemy
{
	[Header("회전할 파츠")]
	[Tooltip("*SOCKET_SWIVEL 연결 — 수평(Y축) 좌우 회전")]
	public Transform swivelTransform;
	[Tooltip("*SOCKET_MOUNT 연결 — 수직(X축) 상하 회전. 총구가 달린 파츠.")]
	public Transform mountTransform;

	protected override void Start()
	{
		// 터렛이라 이동속도 0, 순찰범위 0
		base.Start();
		baseMoveSpeed = 0f;
		maxSpeed = 0f;
		patrolRadius = 0f;
	}

	// 루트 전체 대신 swivel(좌우)/mount(상하)만 회전. FORGE3D F3DTurret smoothControlling 방식 참고.
	protected override void RotateTowardTarget()
	{
		if (target == null)
		{
			return;
		}

		// 수평(Y축) — Swivel이 좌우로만 회전
		if (swivelTransform != null)
		{
			Vector3 flatTarget = target.position;
			flatTarget.y = swivelTransform.position.y;
			Quaternion rotY = Quaternion.LookRotation(flatTarget - swivelTransform.position, swivelTransform.up);
			swivelTransform.rotation = Quaternion.Slerp(swivelTransform.rotation, rotY, rotateSpeed * Time.fixedDeltaTime);
			swivelTransform.localEulerAngles = new Vector3(0f, swivelTransform.localEulerAngles.y, 0f);
		}

		// 수직(X축) — Mount가 상하로만 회전
		if (mountTransform != null)
		{
			Vector3 dir = target.position - mountTransform.position;
			Vector3 up = swivelTransform != null ? swivelTransform.up : transform.up;
			Quaternion rotX = Quaternion.LookRotation(dir, up);
			mountTransform.rotation = Quaternion.Slerp(mountTransform.rotation, rotX, rotateSpeed * Time.fixedDeltaTime);
			mountTransform.localEulerAngles = new Vector3(mountTransform.localEulerAngles.x, 0f, 0f);
		}
	}

	protected override void OnAIAttack()
	{
		base.OnAIAttack();
		weaponSystem.Shoot(PROJECTILE_TYPE.MISSILE);
	}
}
