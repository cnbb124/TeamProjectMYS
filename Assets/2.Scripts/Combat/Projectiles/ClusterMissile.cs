using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 분열 미사일.
// splitDistance만큼 이동하면 splitCount개의 HOMING Missile로 분열(자탄 소환).
// 자탄은 LockOnSystem.MultiLockedTargets를 라운드로빈으로 배정, 타겟이 없으면 직진(targetTr=null).
public class ClusterMissile : Missile
{
	// ClusterMisslleData에서 복사
	private float splitDistance;
	private int splitCount;
	private float splitSpreadAngle;

	// 분열 시 자탄에게 배정할 타겟 목록 (WeaponSystem에서 전달, 없으면 전부 직진)
	private List<Transform> splitTargets;

	private bool hasSplit = false;

	public override void Init(Vector3 startPos, Vector3 dir, Unit attacker)
	{
		base.Init(startPos, dir, attacker);

		if (missileData is ClusterMisslleData clusterData)
		{
			splitDistance = clusterData.splitDistance;
			splitCount = clusterData.splitCount;
			splitSpreadAngle = clusterData.splitSpreadAngle;
		}

		hasSplit = false;
		// 풀링 재사용 시 이전 발사의 타겟이 남지 않도록 초기화 (필요하면 4-arg Init에서 다시 설정)
		splitTargets = null;
	}

	// 락온된 타겟들까지 같이 넘기는 오버로드
	public void Init(Vector3 startPos, Vector3 dir, Unit attacker, List<Transform> targets)
	{
		Init(startPos, dir, attacker);
		splitTargets = targets;
	}

	protected override void Update()
	{
		base.Update();

		if (!hasSplit && traveledDistance >= splitDistance)
		{
			Split();
		}
	}

	// splitCount개의 HOMING 자탄을 부채꼴로 소환하고 본체는 소멸.
	private void Split()
	{
		hasSplit = true;

		float angleStep = splitCount > 1 ? splitSpreadAngle / (splitCount - 1) : 0f;
		float startAngle = -splitSpreadAngle * 0.5f;

		for (int i = 0; i < splitCount; i++)
		{
			Missile child = PoolManager.Instance.GetMissile();

			// 부채꼴로 퍼지는 발사 방향 (가운데 기준 좌우대칭)
			float angle = startAngle + angleStep * i;
			Vector3 spreadDir = Quaternion.AngleAxis(angle, transform.up) * transform.forward;

			// 타겟 라운드로빈 배정, 없으면 null(직진)
			Transform target = null;
			if (splitTargets != null && splitTargets.Count > 0)
			{
				target = splitTargets[i % splitTargets.Count];
			}

			child.Init(transform.position, spreadDir, attacker, target);
		}

		// 분열 지점에서 폭발 (ClusterMisslleData의 damage/explosionRadius 기준)
		Explode(explosionInfo);
		ReturnToPool();
	}
}
