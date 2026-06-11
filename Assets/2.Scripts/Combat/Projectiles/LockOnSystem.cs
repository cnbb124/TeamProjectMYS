using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

// =====================================================================
// MissileLockOnSystem
// 발사 주체(Player, Enemy 유닛)에 붙이는 컴포넌트.
// 범위 내 적 자동 감지 → 락온 모드(Single/Multi)에 따라 락온 수행.
// =====================================================================


public class LockOnSystem : MonoBehaviour
{
	[Space(5)]
	[Header("<size=18>[락온 시스템 설정]</size>")]

	[Header("현재 락온 모드 (미사일 종류에 따라 변경)")]
	//플레이어에서 장비되는 미사일따라 스위칭해서 입력할것.
	public LOCK_ON_MODE currentLockMode = LOCK_ON_MODE.SINGLE;
	private WeaponSystem weaponSystem;

	[Header("멀티 락온 시 최대 동시 락온 개수")]
	public int maxMultiLockCount = 4;

	[Header("락온 탐지 범위")]
	public float lockOnRange = 80f;

	[Header("락온 확정까지 필요한 시간초")]
	public float lockOnRequiredTime = 1.2f;

	[Header("락온 대상이될 레이어 마스크")]
	public LayerMask targetLayerMask;

	[Header("락온 전방 각도 제한")]
	[Range(10f, 180f)]
	public float lockOnAngle = 60f;

	// =============현재 상태 (UI팀 외부 참조용)==================
	//락온범위내의 락온가능상대
	public List<Transform> TargetsInLockonRange = new List<Transform>();
	//레이더범위내의 상대
	public Collider[] TargetsInRadarRange;


	// Single (단일타겟락온)모드 전용 변수
	// 현재 목표로 삼은락온되고있는 후보
	public Transform LockOnCandidate;
	// 락온된 타겟
	public Transform LockedTarget;

	// Multi (다중타겟락온)모드 전용 변수
	//현재 목표로 삼은 락온되고있는 후보들
	public List<Transform> MultiLockCandidates = new List<Transform>();
	//락온된 타겟들
	public List<Transform> MultiLockedTargets = new List<Transform>();

	
	//락온 진행률(UI표현에 참조)
	public float LockOnProgress;
	//락온 여부
	public bool IsLocked;


	private float lockOnTimer = 0f;
	private int _currentTargetIndex = 0;
	private Unit ownerUnit;

	private void Awake()
	{
		ownerUnit = GetComponent<Unit>();
		weaponSystem = GetComponent<WeaponSystem>();
	}

	private void Update()
	{
		FindAllTargets();

		// 타겟 없으면 전부 초기화
		if (TargetsInLockonRange.Count == 0)
		{
			ClearLock();
			return;
		}
		UpdateLockMode();

		// 선택된 모드에 따라 처리 로직 분리
		switch (currentLockMode)
		{
			case LOCK_ON_MODE.SINGLE:
				UpdateSingleLockMode();
				break;

			case LOCK_ON_MODE.MULTI:
				UpdateMultiLockMode();
				break;
		}
	}

	/// <summary>
	/// 락온 모드 변경 업데이트 로직. 모드가 실제로 바뀐 시점에만 처리.
	/// </summary>
	void UpdateLockMode()
	{
		LOCK_ON_MODE newMode = currentLockMode;

		switch(weaponSystem.curMissileType)
		{
			case MISSILE_TYPE.HOMING:
				newMode = LOCK_ON_MODE.SINGLE;
				break;

			case MISSILE_TYPE.CLUSTER:
				newMode = LOCK_ON_MODE.MULTI;
				break;

			case MISSILE_TYPE.DUMB:
				newMode = LOCK_ON_MODE.NONE;
				break;
		}

		if (newMode == currentLockMode)
		{
			return;
		}

		currentLockMode = newMode;

		if (currentLockMode == LOCK_ON_MODE.NONE)
		{
			ClearLock();
		}
	}
	/// <summary>
	/// 단일 락온 모드 업데이트 로직
	/// </summary>
	private void UpdateSingleLockMode()
	{
		if (_currentTargetIndex >= TargetsInLockonRange.Count)
		{
			_currentTargetIndex = TargetsInLockonRange.Count - 1;
		}

		LockOnCandidate = TargetsInLockonRange[_currentTargetIndex];

		lockOnTimer += Time.deltaTime;
		LockOnProgress = Mathf.Clamp01(lockOnTimer / lockOnRequiredTime);

		if (!IsLocked && lockOnTimer >= lockOnRequiredTime)
		{
			IsLocked = true;
			LockedTarget = LockOnCandidate;
			// SoundManager.Instance.PlaySFXUI(SOUND_TYPE.SFX_UI_LOCKON_COMPLETE);
		}

		if (IsLocked)
		{
			LockedTarget = LockOnCandidate;
		}
	}

	/// <summary>
	/// 다중 락온 모드 업데이트 로직
	/// </summary>
	private void UpdateMultiLockMode()
	{
		MultiLockCandidates.Clear();

		// 탐지된 타겟 중 최대 개수(maxMultiLockCount)만큼만 후보로 등록
		int count = Mathf.Min(TargetsInLockonRange.Count, maxMultiLockCount);
		for (int i = 0; i < count; i++)
		{
			MultiLockCandidates.Add(TargetsInLockonRange[i]);
		}

		lockOnTimer += Time.deltaTime;
		LockOnProgress = Mathf.Clamp01(lockOnTimer / lockOnRequiredTime);

		if (!IsLocked && lockOnTimer >= lockOnRequiredTime)
		{
			IsLocked = true;
			MultiLockedTargets.Clear();
			MultiLockedTargets.AddRange(MultiLockCandidates);
			// SoundManager.Instance.PlaySFXUI(SOUND_TYPE.SFX_UI_LOCKON_COMPLETE);
		}

		if (IsLocked)
		{
			MultiLockedTargets.Clear();
			MultiLockedTargets.AddRange(MultiLockCandidates);
		}
	}

	private void FindAllTargets()
	{
		//현재 유닛에서 락온사거리까지, 락온목표레이어를 저장
		Collider[] hits = Physics.OverlapSphere(transform.position, lockOnRange, targetLayerMask);
		TargetsInRadarRange = hits;

		foreach (Collider hit in hits)
		{
			if (ownerUnit != null && hit.GetComponentInParent<Unit>() == ownerUnit)
			{
				continue;
			}

			Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;
			float angle = Vector3.Angle(transform.forward, dirToTarget);
			if (angle > lockOnAngle * 0.5f)
			{
				continue;
			}

			// 해당 객체가 HitBox 컴포넌트를 가지고 있는지 우선 확인
			HitBox hitbox = hit.GetComponent<HitBox>();
			if (hitbox == null)
			{
				continue;
			}

			//Unit에 소속된 것인지
			Unit parentUnit = hit.GetComponentInParent<Unit>();
			if (parentUnit == null || (ownerUnit != null && parentUnit == ownerUnit))
			{
				continue;
			}

			// 검증이 완료되면 HitBox의 좌표를 락온 대상으로 등록
			if (!TargetsInLockonRange.Contains(hitbox.transform))
			{
				TargetsInLockonRange.Add(hitbox.transform);
			}
			//Transform unitTr = hit.GetComponentInParent<Unit>()?.transform;
			//if (unitTr == null)
			//{
			//	continue;
			//}

			//if (!TargetsInRange.Contains(unitTr))
			//{
			//	TargetsInRange.Add(unitTr);
			//}
		}

		// 범위 벗어나거나 비활성화된 타겟 제거
		for (int i = TargetsInLockonRange.Count - 1; i >= 0; i--)
		{
			Transform t = TargetsInLockonRange[i];
			if (t == null || !t.gameObject.activeInHierarchy)
			{
				TargetsInLockonRange.RemoveAt(i);
				if (i <= _currentTargetIndex && _currentTargetIndex > 0) _currentTargetIndex--;
				{
					continue;
				}
			}

			float dist = Vector3.Distance(transform.position, t.position);
			float angle = Vector3.Angle(transform.forward, (t.position - transform.position).normalized);
			if (dist > lockOnRange || angle > lockOnAngle * 0.5f)
			{
				TargetsInLockonRange.RemoveAt(i);
				if (i <= _currentTargetIndex && _currentTargetIndex > 0)
				{
					_currentTargetIndex--;
				}
			}
		}
	}

	/// <summary>
	/// 락온 대상 전환 (Single 모드에서만 작동)
	/// </summary>
	public void SwitchTarget(int direction)
	{
		if (currentLockMode == LOCK_ON_MODE.MULTI)
		{
			// 멀티 락온 모드에서는 전체를 동시 조준하므로 개별 스위치 기능을 제한.
			return;
		}

		if (TargetsInLockonRange.Count <= 1)
		{
			return;
		}
		_currentTargetIndex = (_currentTargetIndex + direction + TargetsInLockonRange.Count) % TargetsInLockonRange.Count;
	}

	/// <summary>
	/// 락온 상태 초기화
	/// </summary>
	public void ClearLock()
	{
		// 공통 초기화
		lockOnTimer = 0f;
		LockOnProgress = 0f;
		IsLocked = false;
		_currentTargetIndex = 0;

		// Single 초기화
		LockOnCandidate = null;
		LockedTarget = null;

		// Multi 초기화
		MultiLockCandidates.Clear();
		MultiLockedTargets.Clear();
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
		Gizmos.DrawSphere(transform.position, lockOnRange);

		Gizmos.color = Color.yellow;
		Vector3 leftDir = Quaternion.Euler(0, -lockOnAngle * 0.5f, 0) * transform.forward;
		Vector3 rightDir = Quaternion.Euler(0, lockOnAngle * 0.5f, 0) * transform.forward;
		Gizmos.DrawRay(transform.position, leftDir * lockOnRange);
		Gizmos.DrawRay(transform.position, rightDir * lockOnRange);
		Gizmos.DrawRay(transform.position, transform.forward * lockOnRange);

		// 기즈모: Single 모드 시각화
		if (currentLockMode == LOCK_ON_MODE.SINGLE && LockOnCandidate != null)
		{
			Gizmos.color = IsLocked ? Color.red : Color.cyan;
			Gizmos.DrawWireSphere(LockOnCandidate.position, 2f);
			Gizmos.DrawLine(transform.position, LockOnCandidate.position);
		}
		// 기즈모: Multi 모드 시각화
		else if (currentLockMode == LOCK_ON_MODE.MULTI && MultiLockCandidates.Count > 0)
		{
			Gizmos.color = IsLocked ? Color.red : Color.green;
			foreach (Transform t in MultiLockCandidates)
			{
				Gizmos.DrawWireSphere(t.position, 2f);
				Gizmos.DrawLine(transform.position, t.position);
			}
		}

		// 전체 감지 타겟 시각화
		Gizmos.color = Color.white;
		foreach (Transform t in TargetsInLockonRange)
		{
			if (currentLockMode == LOCK_ON_MODE.SINGLE && t == LockOnCandidate)
			{
				continue;
			}
			if (currentLockMode == LOCK_ON_MODE.MULTI && MultiLockCandidates.Contains(t))
			{
				continue;
			}

			Gizmos.DrawWireSphere(t.position, 1.5f);
		}
	}
}