using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

// ================================================================
// [LockOnSystem — 외부 참조 / 사용 가이드]
// ================================================================
// 발사 주체(Player, Enemy)에 붙이는 컴포넌트.
// 범위 내 적 자동 감지 → 락온 모드(SINGLE/MULTI)에 따라 락온 수행.
// 미사일 종류가 바뀌면 UpdateLockMode()가 자동으로 모드 전환.
// 락온레이어는 항상 LockOnBox설정해줄것. 필요시 다른것도 중복체크.
// ================================================================
// [HUD / UI팀 외부 참조용 (읽기 전용)]
// ================================================================
// IsLocked              : 락온 확정 여부
// LockOnProgress        : 락온 진행률 0~1 (게이지 표시용)
// LockOnCandidate       : 현재 락온 중인 단일 후보 (SINGLE 모드)
// LockedTarget          : 락온 확정된 단일 타겟   (SINGLE 모드)
// MultiLockCandidates   : 락온 중인 다중 후보 목록 (MULTI 모드)
// MultiLockedTargets    : 락온 확정된 다중 타겟 목록 (MULTI 모드)
// TargetsInLockonRange  : 범위+각도 안의 전체 감지 타겟 (거리 오름차순 정렬)
//
// ================================================================
// [작동 흐름]
// ================================================================
// Update()
//   └── FindAllTargets()          OverlapSphere로 범위 내 LockOnBox 탐색 → 거리순 정렬
//         UpdateDwellTimes()      타겟별 Angle 체류시간 갱신(벗어나면 리셋, 재진입 시 0부터)
//         UpdateLockMode()        미사일 종류에 따라 SINGLE/MULTI/NONE 자동 전환
//         UpdateSingleLockMode()  현재 후보 체류시간 >= lockOnRequiredTime 시 LockedTarget 확정
//         UpdateMultiLockMode()   후보별 체류시간 충족분을 MultiLockedTargets로 확정
//
// ================================================================
// [외부 호출용 메서드]
// ================================================================
// SwitchTarget(int direction)   타겟 전환. +1=오른쪽  -1=왼쪽 (스크린 X 기준)
// ClearLock()                   락온 전체 초기화
// ================================================================


public class LockOnSystem : MonoBehaviour
{
	[Space(5)]
	[Header("<size=22>[락온 시스템 설정]</size>")]

	[Header("현재 락온 모드 (미사일 종류에 따라 변경)")]
	//플레이어에서 장비되는 미사일따라 스위칭해서 입력할것.
	public LOCK_ON_MODE currentLockMode = LOCK_ON_MODE.SINGLE;
	private WeaponSystem _weaponSystem;

	//[Header("멀티 락온 시 최대 동시 락온 개수")]
	[HideInInspector]//현재 Missile So에서 받아옴(Cluster)
	public int maxMultiLockCount = 4;
	[SerializeField]
	[Tooltip("유닛 동시감지 최대수(최적화용)")]
	private int _targetInRadarRangeBufferSize = 64;
	
	private int _updateCount = 0;
	private const int UPDATE_INTERVAL = 3; // 3프레임마다 탐색
	[Header("락온 탐지 범위")]
	
	public float lockOnRange = 400f;

	[Header("락온 확정까지 필요한 시간초")]
	public float lockOnRequiredTime = 1.2f;

	[Header("락온 대상이될 레이어 마스크. 기본적으로 LockOnBox선택하고 필요")]
	//[HideInInspector] //Awake에서 할시
	public LayerMask targetLayerMask;

	[Header("락온 각도 제한(전방위터렛이면 360도)")]
	[Range(10f, 360f)]
	public float lockOnAngle = 60f;



	[Header("<size=14>==========현재 상태 (UI팀 외부 참조용, 입력x)</size>==========")] 
	//락온범위내의 락온가능상대
	public List<Transform> TargetsInLockonRange = new List<Transform>();
	//레이더범위내의 상대
	public Collider[] TargetsInRadarRange;
	//TargetsInRadarRange 중 이번 프레임 실제 감지된 유효 개수 (뒤쪽 인덱스는 이전 프레임 잔여값이므로 이 수까지만 순회할 것)
	public int RadarHitCount { get; private set; }


	[Header("Single (단일타겟락온)모드 전용 변수")]
	[Tooltip("현재 목표로 삼은락온되고있는 후보")]// 현재 목표로 삼은락온되고있는 후보
	public Transform LockOnCandidate;
	// 락온된 타겟
	[Tooltip("락온된 타겟")]
	public Transform LockedTarget;

	[Header("Multi (다중타겟락온)모드 전용 변수")]
	//현재 목표로 삼은 락온되고있는 후보들
	[Tooltip("현재 목표로 삼은 락온되고있는 후보들")]
	public List<Transform> MultiLockCandidates = new List<Transform>();
	//락온된 타겟들
	[Tooltip("락온된 타겟들")]
	public List<Transform> MultiLockedTargets = new List<Transform>();


	//락온 진행률(UI표현에 참조)
	public float LockOnProgress;
	//락온 여부
	public bool IsLocked;


	private int _currentTargetIndex = 0;
	private Unit _ownerUnit;

	// 타겟별 Angle 체류 누적시간. Angle 안에 머무는 동안 증가, 벗어나면 제거(재진입 시 0부터 다시 셈).
	private Dictionary<Transform, float> _dwellTimes = new Dictionary<Transform, float>();
	// 체류시간 제거 대상 임시 리스트(Dictionary 순회 중 제거 불가 회피 + 매프레임 new 방지)
	private List<Transform> _dwellRemoveCache = new List<Transform>();

	// 클러스터 미사일 락온 모드 (true = 단일타겟에 전탄 집중, false = 다중타겟 분산)
	private bool _clusterSingleLockMode = false;

	private void Awake()
	{
		_ownerUnit = GetComponent<Unit>();
		_weaponSystem = GetComponent<WeaponSystem>();
		TargetsInRadarRange = new Collider[_targetInRadarRangeBufferSize];
		// 설정 안할시 기본값
		if (targetLayerMask == 0)
		{
			targetLayerMask = 1 << LayerMask.NameToLayer("LockOnBox");
		}
	
	}

	private void Update()
	{
		// 클러스터 단일/다중 락온 모드 전환 (플레이어 입력만 반영)
		if (_ownerUnit is Player && InputManager.Instance != null && InputManager.Instance.toggleClusterLockMode)
		{
			_clusterSingleLockMode = !_clusterSingleLockMode;
		}


		_updateCount++;
		if (_updateCount >= UPDATE_INTERVAL)
		{
			FindAllTargets();
			_updateCount = 0;
		}

		// 타겟 없으면 전부 초기화
		if (TargetsInLockonRange.Count == 0)
		{
			ClearLock();
			return;
		}

		// 타겟별 체류시간부터 갱신(락온 판정의 근거)
		UpdateDwellTimes();
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

		switch (_weaponSystem.curMissileType)
		{
			case MISSILE_TYPE.HOMING:
				newMode = LOCK_ON_MODE.SINGLE;
				break;

			case MISSILE_TYPE.CLUSTER:
				newMode = _clusterSingleLockMode ? LOCK_ON_MODE.SINGLE : LOCK_ON_MODE.MULTI;
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
		ClearLock();
	}
	/// <summary>
	/// 타겟별 Angle 체류시간 갱신.
	/// Angle+Range 안(= TargetsInLockonRange)에 있는 타겟은 시간 누적, 벗어난 타겟은 제거해 재진입 시 0부터 다시 세게 함.
	/// </summary>
	private void UpdateDwellTimes()
	{
		// Angle 안에 머무는 타겟들 체류시간 증가
		for (int i = 0; i < TargetsInLockonRange.Count; i++)
		{
			Transform t = TargetsInLockonRange[i];
			if (t == null)
			{
				continue;
			}

			if (_dwellTimes.ContainsKey(t))
			{
				_dwellTimes[t] += Time.deltaTime;
			}
			else
			{
				_dwellTimes[t] = Time.deltaTime;
			}
		}

		// Angle에서 벗어난(리스트에 없는) 타겟은 체류시간 제거 → 재진입 시 처음부터
		_dwellRemoveCache.Clear();
		foreach (KeyValuePair<Transform, float> kv in _dwellTimes)
		{
			if (kv.Key == null || !TargetsInLockonRange.Contains(kv.Key))
			{
				_dwellRemoveCache.Add(kv.Key);
			}
		}
		for (int i = 0; i < _dwellRemoveCache.Count; i++)
		{
			_dwellTimes.Remove(_dwellRemoveCache[i]);
		}
	}

	/// <summary>
	/// 해당 타겟이 체류시간(lockOnRequiredTime)을 채워 락온 자격이 있는지
	/// </summary>
	private bool IsDwellComplete(Transform t)
	{
		return t != null && _dwellTimes.TryGetValue(t, out float d) && d >= lockOnRequiredTime;
	}

	/// <summary>
	/// 해당 타겟의 락온 진행률 0~1 (UI 게이지용)
	/// </summary>
	private float GetDwellProgress(Transform t)
	{
		if (t != null && _dwellTimes.TryGetValue(t, out float d))
		{
			return Mathf.Clamp01(d / lockOnRequiredTime);
		}
		return 0f;
	}

	/// <summary>
	/// 외부(UI팀) 참조용 — 특정 타겟의 락온 진행률 0~1. 후보마다 게이지를 개별 표시할 때 사용.
	/// </summary>
	public float GetLockOnProgress(Transform t)
	{
		return GetDwellProgress(t);
	}

	/// <summary>
	/// 단일 락온 모드 업데이트 로직.
	/// 현재 후보가 체류시간을 채우면 확정. 후보가 아직이면 기존 락온을 자격 유지되는 한 유지(switch로 후보만 옮긴 경우 대응).
	/// </summary>
	private void UpdateSingleLockMode()
	{
		if (_currentTargetIndex >= TargetsInLockonRange.Count)
		{
			_currentTargetIndex = TargetsInLockonRange.Count - 1;
		}
		if (_currentTargetIndex < 0)
		{
			_currentTargetIndex = 0;
		}

		LockOnCandidate = TargetsInLockonRange[_currentTargetIndex];
		LockOnProgress = GetDwellProgress(LockOnCandidate);

		// 현재 후보가 체류시간을 다 채웠으면 그 후보로 락온 확정(대상 이동 포함)
		if (IsDwellComplete(LockOnCandidate))
		{
			IsLocked = true;
			LockedTarget = LockOnCandidate;
			// SoundManager.Instance.PlaySFXUI(SOUND_TYPE.SFX_UI_LOCKON_COMPLETE);
			return;
		}

		// 현재 후보는 아직 미완성 — 기존 LockedTarget이 여전히 Angle 안+자격 유지면 그대로 유지(switch로 후보만 바뀐 상황)
		if (LockedTarget != null && TargetsInLockonRange.Contains(LockedTarget) && IsDwellComplete(LockedTarget))
		{
			IsLocked = true;
			return;
		}

		// 유효한 락온 없음
		IsLocked = false;
		LockedTarget = null;
	}

	/// <summary>
	/// 다중 락온 모드 업데이트 로직.
	/// 후보별 체류시간을 독립적으로 판정 → 채운 것만 MultiLockedTargets에 확정. 벗어난 타겟은 자동 해제.
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

		// 후보별 체류시간 기준으로 확정 타겟 매프레임 재구성(Angle 벗어난 타겟은 dwell이 없으므로 자동 제외)
		MultiLockedTargets.Clear();
		for (int i = 0; i < MultiLockCandidates.Count; i++)
		{
			if (IsDwellComplete(MultiLockCandidates[i]))
			{
				MultiLockedTargets.Add(MultiLockCandidates[i]);
			}
		}
		IsLocked = MultiLockedTargets.Count > 0;

		// 진행률은 가장 가까운 후보([0]) 기준 대표값(UI 게이지용)
		LockOnProgress = MultiLockCandidates.Count > 0 ? GetDwellProgress(MultiLockCandidates[0]) : 0f;
	}

	private void FindAllTargets()
	{
		////현재 유닛에서 락온사거리까지, 락온목표레이어를 저장
		//Collider[] hits = Physics.OverlapSphere(transform.position, lockOnRange, targetLayerMask);
		//TargetsInRadarRange = hits;


		//foreach (Collider hit in hits)
		//{
		//	if (_ownerUnit != null && hit.GetComponentInParent<Unit>() == _ownerUnit)
		//	{
		//		continue;
		//	}

		//	Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;
		//	float angle = Vector3.Angle(transform.forward, dirToTarget);
		//	if (angle > lockOnAngle * 0.5f)
		//	{
		//		continue;
		//	}

		//	// 해당 객체가 HitBox 컴포넌트를 가지고 있는지 우선 확인
		//	LockOnBox lockOnBox = hit.GetComponent<LockOnBox>();
		//	if (lockOnBox == null)
		//	{
		//		continue;
		//	}

		//	//Unit에 소속된 것인지
		//	Unit parentUnit = hit.GetComponentInParent<Unit>();
		//	if (parentUnit == null || (_ownerUnit != null && parentUnit == _ownerUnit))
		//	{
		//		continue;
		//	}
		//	//같은팀인지 아닌지(태그로)
		//	if (_ownerUnit != null && parentUnit.gameObject.tag == _ownerUnit.gameObject.tag)
		//	{
		//		continue;
		//	}

		//	// 검증이 완료되면 HitBox의 좌표를 락온 대상으로 등록
		//	if (!TargetsInLockonRange.Contains(lockOnBox.transform))
		//	{
		//		TargetsInLockonRange.Add(lockOnBox.transform);
		//	}
		//}


		int hitCount = Physics.OverlapSphereNonAlloc(transform.position, lockOnRange, TargetsInRadarRange, targetLayerMask);
		RadarHitCount = hitCount;

		for (int i = 0; i < hitCount; i++)
		{

			Collider hit = TargetsInRadarRange[i];
			if (_ownerUnit != null && hit.GetComponentInParent<Unit>() == _ownerUnit)
			{
				continue;
			}



			Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;
			float angle = Vector3.Angle(transform.forward, dirToTarget);
			if (angle > lockOnAngle * 0.5f) continue;

			LockOnBox lockOnBox = hit.GetComponent<LockOnBox>();
			if (lockOnBox == null) continue;

			//기존 감지 로직 그대로 사용(hits[i] 를 _hitBuffer[i] 로 대체)

			//Unit에 소속된 것인지
			Unit parentUnit = hit.GetComponentInParent<Unit>();
			if (parentUnit == null || (_ownerUnit != null && parentUnit == _ownerUnit))
			{
				continue;
			}

			//같은팀인지 아닌지(태그로)
			if (_ownerUnit != null && parentUnit.gameObject.tag == _ownerUnit.gameObject.tag)
			{
				continue;
			}

			// 검증이 완료되면 HitBox의 좌표를 락온 대상으로 등록
			if (!TargetsInLockonRange.Contains(lockOnBox.transform))
			{
				TargetsInLockonRange.Add(lockOnBox.transform);
			}
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

		// 정렬 전 현재 타겟 기억 — 정렬 후 인덱스 복원용
		Transform currentCandidate = (_currentTargetIndex >= 0 && _currentTargetIndex < TargetsInLockonRange.Count)
			? TargetsInLockonRange[_currentTargetIndex]
			: null;

		TargetsInLockonRange.Sort(
			(a, b) => Vector3.Distance(transform.position, a.position).CompareTo(Vector3.Distance(transform.position, b.position))
		);

		if (currentCandidate != null)
		{
			int newIndex = TargetsInLockonRange.IndexOf(currentCandidate);
			if (newIndex >= 0)
			{
				_currentTargetIndex = newIndex;
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

		if (Camera.main == null)
		{
			return;
		}

		float currentScreenX = Camera.main.WorldToScreenPoint(TargetsInLockonRange[_currentTargetIndex].position).x;

		// 현재 타겟 기준으로 direction 방향에서 가장 가까운 스크린X 타겟 탐색
		int bestIndex = -1;
		float bestDiff = float.MaxValue;

		for (int i = 0; i < TargetsInLockonRange.Count; i++)
		{
			if (i == _currentTargetIndex)
			{
				continue;
			}

			float screenX = Camera.main.WorldToScreenPoint(TargetsInLockonRange[i].position).x;
			float diff = (screenX - currentScreenX) * direction;

			if (diff > 0 && diff < bestDiff)
			{
				bestDiff = diff;
				bestIndex = i;
			}
		}

		if (bestIndex != -1)
		{
			_currentTargetIndex = bestIndex;
		}
	}

	/// <summary>
	/// 락온 상태 초기화
	/// </summary>
	public void ClearLock()
	{
		// 공통 초기화
		_dwellTimes.Clear();
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