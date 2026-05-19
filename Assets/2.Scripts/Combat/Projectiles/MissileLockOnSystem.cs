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

	//=============== 현재 상태 (UI팀 외부 참조용) ==============
	// 범위 내 감지된 전체 타겟 리스트 (UI 마커용)
	public List<Transform> TargetsInRange = new List<Transform>();

	// 현재 락온 진행 중인 후보 타겟
	public Transform LockOnCandidate;
	// 락온 완전히 확정된 타겟
	public Transform LockedTarget;
	// 락온 진행률 0~1 (UI 연동용)
	public float LockOnProgress; 
	// 락온 완전 확정 여부
	public bool IsLocked;
//마커 표시 - TargetsInRange 리스트 순회하면서 각 타겟 월드 좌표를 Camera.main.WorldToScreenPoint()로 스크린 좌표 변환 후
//Canvas에 마커 오브젝트 붙이기.
//락온 진행 표시 - LockOnCandidate 위치에 회전하는 삼각형 마커.LockOnProgress 값으로 회전 속도나 크기 조절. 0이면 크고 느리게, 1에 가까울수록 작고 빠르게.
//락온 확정 표시 - IsLocked가 true면 마커 색 변경 + 사운드는 MissileLockOnSystem에서 이미 호출.

	private float lockOnTimer = 0f;
	// 현재 선택된 타겟 인덱스 (SwitchTarget용)
	private int _currentTargetIndex = 0;
	private Unit ownerUnit; // 이 시스템의 소유 유닛 (자기 자신과 충돌 방지용)

	private void Awake()
	{
		ownerUnit = GetComponent<Unit>();
	}

	private void Update()
	{
		// 범위+각도 안의 타겟 전체 갱신
		FindAllTargets();

		// 현재 인덱스 유효성 체크 (타겟 수 변동 시 보정)
		if (TargetsInRange.Count == 0)
		{
			_currentTargetIndex = 0;
			LockOnCandidate = null;
		}
		else
		{
			_currentTargetIndex = Mathf.Clamp(_currentTargetIndex, 0, TargetsInRange.Count - 1);
			LockOnCandidate = TargetsInRange[_currentTargetIndex];
		}

		// 후보가 없으면 전부 초기화
		if (LockOnCandidate == null)
		{
			lockOnTimer = 0f;
			LockOnProgress = 0f;
			IsLocked = false;
			LockedTarget = null;
			return;
		}

		// 후보 있으면 타이머 누적
		lockOnTimer += Time.deltaTime;
		LockOnProgress = Mathf.Clamp01(lockOnTimer / lockOnRequiredTime);

		// 시간 채우면 락온 확정 (확정 순간 한 번만)
		if (!IsLocked && lockOnTimer >= lockOnRequiredTime)
		{
			IsLocked = true;
			LockedTarget = LockOnCandidate;
			SoundManager.Instance.PlaySFXUI(SOUND_TYPE.SFX_UI_LOCKON_COMPLETE);
		}
	}

	// 범위 + 각도 조건을 만족하는 가장 가까운 타겟 반환 현재 미사용
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



	// 범위 + 각도 조건을 만족하는 타겟 전부 리스트로 갱신
	private void FindAllTargets()
	{
		TargetsInRange.Clear();

		Collider[] hits = Physics.OverlapSphere(transform.position, lockOnRange, targetLayerMask);

		foreach (Collider hit in hits)
		{
			// 자기 자신 제외
			if (ownerUnit != null && hit.transform.root == ownerUnit.transform.root)
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

			// 시야 차단 체크
			if (Physics.Raycast(transform.position, dirToTarget, out RaycastHit rayHit, lockOnRange))
			{
				if (rayHit.collider != hit)
				{
					continue;
				}
			}

			// 루트 트랜스폼 기준으로 중복 방지
			Transform root = hit.transform.root;
			if (!TargetsInRange.Contains(root))
			{
				TargetsInRange.Add(root);
			}
		}
	}

	/// <summary>
	/// 락온 대상 전환. 마우스휠 입력 시 호출.
	/// direction: 1 = 다음, -1 = 이전
	/// </summary>
	public void SwitchTarget(int direction)
	{
		if (TargetsInRange.Count <= 1) return;

		_currentTargetIndex = (_currentTargetIndex + direction + TargetsInRange.Count) % TargetsInRange.Count;

		// 대상 바뀌면 타이머 리셋
		lockOnTimer = 0f;
		LockOnProgress = 0f;
		IsLocked = false;
		LockedTarget = null;
	}

	/// <summary>
	/// 락온 강제 해제. 필요 시 외부에서 호출.
	/// </summary>
	public void ClearLock()
	{
		LockOnCandidate = null;
		LockedTarget = null;
		lockOnTimer = 0f;
		LockOnProgress = 0f;
		IsLocked = false;
		_currentTargetIndex = 0;
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

		// 범위 내 전체 타겟 표시
		Gizmos.color = Color.white;
		foreach (Transform t in TargetsInRange)
		{
			if (t != LockOnCandidate)
			{
				Gizmos.DrawWireSphere(t.position, 1.5f);
			}
		}
	}
}
