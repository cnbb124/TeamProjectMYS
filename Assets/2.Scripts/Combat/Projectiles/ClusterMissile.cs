using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 분열 미사일.
// splitDistance만큼 이동하면 splitCount개의 HOMING Missile로 분열(자탄 소환).
// 자탄은 LockOnSystem.MultiLockedTargets를 라운드로빈으로 배정, 타겟이 없으면 직진(targetTr=null).
public class ClusterMissile : Missile
{
	// ClusterMisslleData에서 복사
	private float _splitDistance;
	private int _splitCount;
	private float _splitSpreadAngle;

	// 분열 시 자탄에게 배정할 타겟 목록 (WeaponSystem에서 전달, 없으면 전부 직진)
	private List<Transform> _splitTargets;

	private bool _hasSplit = false;

	public override void Init(Vector3 startPos, Vector3 dir, Unit attacker)
	{
		base.Init(startPos, dir, attacker);

		if (missileData is ClusterMisslleData clusterData)
		{
			_splitDistance = clusterData.splitDistance;
			_splitCount = clusterData.splitCount;
			_splitSpreadAngle = clusterData.splitSpreadAngle;
		}

		_hasSplit = false;
		// 풀링 재사용 시 이전 발사의 타겟이 남지 않도록 초기화 (필요하면 4-arg Init에서 다시 설정)
		_splitTargets = null;
	}

	// 락온된 타겟들까지 같이 넘기는 오버로드
	public void Init(Vector3 startPos, Vector3 dir, Unit attacker, List<Transform> targets)
	{
		Init(startPos, dir, attacker);
		_splitTargets = targets;
	}

	protected override void Update()
	{
		base.Update();

		if (!_hasSplit && _traveledDistance >= _splitDistance)
		{
			Split();
		}
	}

	// splitCount개의 HOMING 자탄을 부채꼴로 소환하고 본체는 소멸.
	private void Split()
	{
		_hasSplit = true;

		float angleStep = _splitCount > 1 ? _splitSpreadAngle / (_splitCount - 1) : 0f;
		float startAngle = -_splitSpreadAngle * 0.5f;

		for (int i = 0; i < _splitCount; i++)
		{
			Missile child = PoolManager.Instance.GetClusterMissileChild();

			// 부채꼴로 퍼지는 발사 방향 (가운데 기준 좌우대칭)
			float angle = startAngle + angleStep * i;
			Vector3 spreadDir = Quaternion.AngleAxis(angle, transform.up) * transform.forward;

			// 타겟 배정, 없으면 null(직진). 탄수 > 락온대상일시 같은대상에게 중복.
			Transform target = null;
			if (_splitTargets != null && _splitTargets.Count > 0)
			{
				target = _splitTargets[i % _splitTargets.Count];
			}

			child.Init(transform.position, spreadDir, attacker, target);
		}

		// 분열 지점에서 폭발 (explosionRadius범위)
		Explode(explosionInfo);
		ReturnToPool();
	}
}
