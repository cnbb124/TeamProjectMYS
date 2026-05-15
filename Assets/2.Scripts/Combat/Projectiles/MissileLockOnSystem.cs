using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// MissileLockOnSystem
// 발사 주체(Player, Enemy 유닛)에 붙이는 컴포넌트.
// 범위 내 적 자동 감지 → 락온 유지 시간 채우면 락온 확정.
// 발사 시 Missile.Init()에 targetTr을 넘겨주면 됨.
// =====================================================================

public class MissileLockOnSystem : MonoBehaviour
{
	[Space(5)]
	[Header("<size=18>[락온 시스템 설정]</size>")]

	[Header("락온 탐지 범위")]
	public float lockOnRange = 80f;

	[Header("락온 확정까지 필요한 시간초")]
	public float lockOnRequiredTime = 1.2f;

	[Header("락온 대상이될 레이어 마스크 (상대 Unit HitBox 레이어만 지정)")]
	public LayerMask targetLayerMask;

	[Header("락온 전방 각도 제한 (이 각도 안에 있어야 락온 가능)")]
	[Range(10f, 180f)]
	public float lockOnAngle = 60f;

	// ── 현재 상태 (읽기 전용으로 외부 참조 가능) ──
	// 현재 락온 진행 중인 후보 타겟
	public Transform LockOnCandidate;// { get; private set; }
	// 락온 완전히 확정된 타겟
	public Transform LockedTarget;// { get; private set; }
	// 락온 진행률 0~1 (UI 게이지 연동용)
	public float LockOnProgress; //{ get; private set; }
	// 락온 완전 확정 여부
	public bool IsLocked; //{ get; private set; }

	private float lockOnTimer = 0f;
	private Unit ownerUnit; // 이 시스템의 소유 유닛 (자기 자신과 충돌 방지용)

	private void Awake()
	{
		ownerUnit = GetComponent<Unit>();
	}

	private void Update()
	{
		// 범위+각도 안의 가장 가까운 적 탐지
		Transform candidate = FindBestTarget();

		// 후보가 바뀌면 타이머 리셋
		if (candidate != LockOnCandidate)
		{
			LockOnCandidate = candidate;
			lockOnTimer = 0f;
			IsLocked = false;
			LockedTarget = null;
		}

		// 후보가 있으면 타이머 누적
		if (LockOnCandidate != null)
		{
			lockOnTimer += Time.deltaTime;
			LockOnProgress = Mathf.Clamp01(lockOnTimer / lockOnRequiredTime);

			// 시간 채우면 락온 확정
			if (lockOnTimer >= lockOnRequiredTime)
			{
				IsLocked = true;
				LockedTarget = LockOnCandidate;
			}
		}
		else
		{
			// 후보 없으면 전부 초기화
			lockOnTimer = 0f;
			LockOnProgress = 0f;
			IsLocked = false;
			LockedTarget = null;
		}
	}

	// 범위 + 각도 조건을 만족하는 가장 가까운 타겟 반환
	private Transform FindBestTarget()
	{
		// OverlapSphere로 범위 내 콜라이더 전부 수집
		Collider[] hits = Physics.OverlapSphere(transform.position, lockOnRange, targetLayerMask);

		Transform best = null;
		float closestDist = float.MaxValue;

		foreach (Collider hit in hits)
		{
			// 자기 자신 제외
			if (ownerUnit != null && hit.gameObject == ownerUnit.gameObject)
			{
				continue;
			}

			Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;
			float angle = Vector3.Angle(transform.forward, dirToTarget);

			// 전방 각도 안에 있어야만 락온 가능
			if (angle > lockOnAngle * 0.5f)
			{
				continue;
			}

			// 시야 차단 체크 (장애물이 막고 있으면 스킵)
			if (Physics.Raycast(transform.position, dirToTarget,
				out RaycastHit rayHit, lockOnRange))
			{
				// 레이캐스트가 타겟 레이어 아닌 다른 걸 먼저 맞으면 차단된 것
				if (rayHit.collider != hit)
				{
					continue;
				}
			}

			float dist = Vector3.Distance(transform.position, hit.transform.position);
			if (dist < closestDist)
			{
				closestDist = dist;
				best = hit.transform;
			}
		}

		return best;
	}

	// 락온 강제 해제 (발사 후 슬롯 해제 등 필요 시 외부에서 호출)
	public void ClearLock()
	{
		LockOnCandidate = null;
		LockedTarget = null;
		lockOnTimer = 0f;
		LockOnProgress = 0f;
		IsLocked = false;
	}

	// 에디터에서 범위/각도 기즈모 표시
	private void OnDrawGizmosSelected()
	{
		// 탐지 범위 구체
		Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
		Gizmos.DrawSphere(transform.position, lockOnRange);

		// 락온 각도 부채꼴 (전방 좌우)
		Gizmos.color = Color.yellow;
		Vector3 leftDir = Quaternion.Euler(0, -lockOnAngle * 0.5f, 0) * transform.forward;
		Vector3 rightDir = Quaternion.Euler(0, lockOnAngle * 0.5f, 0) * transform.forward;
		Gizmos.DrawRay(transform.position, leftDir * lockOnRange);
		Gizmos.DrawRay(transform.position, rightDir * lockOnRange);
		Gizmos.DrawRay(transform.position, transform.forward * lockOnRange);

		// 현재 락온 후보 표시
		if (LockOnCandidate != null)
		{
			Gizmos.color = IsLocked ? Color.red : Color.cyan;
			Gizmos.DrawWireSphere(LockOnCandidate.position, 2f);
			Gizmos.DrawLine(transform.position, LockOnCandidate.position);
		}
	}
}
