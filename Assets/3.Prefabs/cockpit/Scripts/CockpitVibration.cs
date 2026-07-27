using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 콕핏 진동 (엔진 아이들링 + 부스터 + 피격 충격 + 쉴드 상태 반영).
///
/// ★ 콕핏 "루트" 오브젝트에 그대로 붙이면 됨. 계층 정리 필요 없음.
///   카메라(XRRig / Camera Offset / Main Camera 등)는 자동으로 찾아서 제외하므로
///   "눈은 가만히, 눈앞 계기판만 흔들림" — VR 멀미 안 남.
///
/// 원리: 자식들을 각각 같은 양만큼 밀어서 통째로 흔드는 것처럼 보이게 함.
///       (부모를 흔들면 카메라까지 흔들리므로 그렇게 안 함)
///
/// ★★ 자동 연동 (autoTrackUnit = true, 기본값)
///   Player/Unit을 스스로 찾아서 매 프레임 상태를 읽는다. 팀 공유 파일
///   (Player.cs / Unit.cs)을 전혀 수정하지 않으므로 병합 충돌이 안 난다.
///     - 엔진    : vibrationMode 로 선택
///                 Acceleration = 가속도(속도 변화량)에 비례하는 연속 진동. 등속 활공은 자동으로 0.
///                                진공에서 몸이 느끼는 힘이 곧 가속도라 속도감이 가장 잘 산다.
///                 StateSteps   = Unit.curState 3단계(활공/추진/부스터). 값을 직접 통제할 때.
///     - 피격    : HP/실드가 줄어든 만큼 자동으로 충격 발생
///     - 쉴드    : 쉴드가 살아있으면 충격을 흡수(약하게), 깨지면 강하고 거칠게
///   ※ 자동 연동을 끄고 직접 호출하고 싶으면 autoTrackUnit 체크 해제 후
///      SetBoost() / AddImpact()를 외부에서 부르면 된다.
///
/// [외부 호출]
/// - SetBoost(true/false) : 부스터 On/Off (수동 모드용)
/// - AddImpact(0.05f)     : 폭발·피격 시 순간 충격 (숫자가 클수록 세게)
/// </summary>
public class CockpitVibration : MonoBehaviour
{
	/// <summary>엔진 진동을 무엇으로 결정할지.</summary>
	public enum VibrationMode
	{
		/// <summary>가속도 비례(연속). 몸이 실제로 느끼는 힘 = 가속도라 속도감이 가장 잘 산다.</summary>
		Acceleration,
		/// <summary>상태 3단계(활공/추진/부스터). 값을 직접 통제하고 싶을 때.</summary>
		StateSteps,
	}

	[Header("── 진동 방식 ──")]
	[Tooltip("Acceleration = 가속도에 비례(연속, 속도감 좋음)\n" +
	         "StateSteps  = 활공/추진/부스터 3단계(기존 방식)\n" +
	         "둘 다 타보고 고르면 됨.")]
	[SerializeField] private VibrationMode vibrationMode = VibrationMode.Acceleration;

	[Header("진동 크기 (미터 단위)")]
	[Tooltip("관성 활공/정지 중(엔진 꺼짐) 진동. 두 방식 공통으로 항상 깔리는 바닥값.\n" +
	         "0 = 완전 정적(대비 최대) / 0.0005 = 리액터 웅웅거림(장르 관습)")]
	[SerializeField] private float coastAmount = 0f;

	[Tooltip("[StateSteps 전용] 추진 중(W 등 입력으로 엔진 켜짐) 진동. 0.002 = 2mm")]
	[SerializeField] private float idleAmount  = 0.002f;

	[Tooltip("[StateSteps 전용] 부스터 켰을 때 진동. 추진 중보다 확실히 크게 잡을 것")]
	[SerializeField] private float boostAmount = 0.012f;

	// ───────────────────────────────────────────────
	[Header("── 가속도 방식 (Acceleration) ──")]
	[Tooltip("켜두면 기체 스탯에서 최대 가속도를 자동 계산한다(boostSpeed ÷ timeToMaxSpeed).\n" +
	         "→ 기체를 바꾸거나 스러스터 파츠로 속도가 변해도 자동으로 맞춰짐. 켜두는 걸 권장.")]
	[SerializeField] private bool autoDeriveAccelReference = true;

	[Tooltip("[자동 계산을 껐을 때만 사용] 이 가속도(m/s²)에서 진동이 최대가 된다.\n" +
	         "기준 잡는 법: 기체의 boostSpeed ÷ timeToMaxSpeed. 예) 150 ÷ 1.2 = 125")]
	[SerializeField] private float accelForFullVibration = 125f;

	[Tooltip("가속도가 최대일 때의 진동 크기(미터).")]
	[SerializeField] private float maxAccelAmount = 0.012f;

	[Tooltip("가속도 값 평활화. 클수록 즉각 반응(거칠고 튐), 작을수록 부드럽게 따라감. 8~15 권장")]
	[SerializeField] private float accelSmoothing = 10f;

	[Header("진동 빠르기")]
	[Tooltip("클수록 잘게 떨림. 25=엔진 부르르, 3=우주 표류 느낌")]
	[SerializeField] private float speed = 3f;

	[Tooltip("부스터 중 진동 빠르기. 대기보다 크게 잡으면 '엔진이 으르렁'거린다.")]
	[SerializeField] private float boostSpeed = 18f;

	[Header("크기 전환 속도 (부스터 켜고 끌 때 부드럽게)")]
	[SerializeField] private float blendSpeed = 5f;

	[Header("피격·폭발 충격이 가라앉는 속도")]
	[Tooltip("클수록 빨리 잦아듦")]
	[SerializeField] private float impactDecay = 4f;

	// ───────────────────────────────────────────────
	[Header("── 자동 연동 (팀 파일 수정 없이 동작) ──")]
	[Tooltip("체크하면 Player/Unit 상태를 스스로 읽어 부스터·피격·쉴드를 자동 반영한다.")]
	[SerializeField] private bool autoTrackUnit = true;

	[Tooltip("감시할 기체. 비워두면 부모에서 자동 탐색 → 그래도 없으면 씬에서 Player를 찾는다.")]
	[SerializeField] private Unit trackedUnit;

	[Header("── 피격 충격 ──")]
	[Tooltip("이 정도 데미지를 한 번에 받으면 충격이 최대가 된다(절대 수치).\n" +
	         "※ HP 비율이 아니라 절대값인 이유: 테스트로 HP를 크게 올려두면 비율 방식은 진동이 사라지고,\n" +
	         "   기체마다 HP가 달라도 같은 공격은 같은 충격이어야 하기 때문.\n" +
	         "프로젝트 기준 데미지 — 적벌컨 3 / 보스탄 8 / 클러스터 10 / 유도미사일 15 / 덤미사일 50")]
	[SerializeField] private float damageForFullImpact = 50f;

	[Tooltip("충격 반응 곡선. 1 = 직선(데미지에 정비례).\n" +
	         "0.5 = 작은 데미지 쪽을 넓게 펴줌 — 실제로는 작은 피격(3~12)이 대부분이라 이쪽이 잘 구분된다.\n" +
	         "사람은 세기를 로그처럼 느끼기 때문에, 직선으로 매핑하면 약한 공격들이 전부 뭉쳐 보인다.")]
	[Range(0.2f, 1f)]
	[SerializeField] private float impactCurve = 0.5f;

	[Tooltip("피격 충격 최대 크기(미터). 0.06 = 6cm")]
	[SerializeField] private float maxImpactAmount = 0.06f;

	[Tooltip("쉴드가 살아있을 때 충격을 얼마나 흡수하는지. 0.35 = 원래의 35%만 전달(=65% 흡수)")]
	[Range(0f, 1f)]
	[SerializeField] private float shieldedImpactScale = 0.35f;

	[Header("── 쉴드 파괴 시 거친 진동 ──")]
	[Tooltip("쉴드가 깨졌을 때 더해지는 불규칙한 떨림 크기. 0이면 사용 안 함.")]
	[SerializeField] private float shieldDownRoughness = 0.0035f;

	[Tooltip("거친 떨림의 빠르기. 대기 speed보다 훨씬 크게 잡아야 '덜컹'거린다.")]
	[SerializeField] private float roughnessSpeed = 30f;

	[Tooltip("쉴드가 최대치의 이 비율 이상 회복돼야 '정상'으로 돌아온다. 0 근처에서 깜빡이는 것 방지(히스테리시스).")]
	[Range(0f, 1f)]
	[SerializeField] private float shieldRecoverRatio = 0.2f;

	[Header("── 디버그 (튜닝용) ──")]
	[Tooltip("체크하면 1초마다 현재/최대 가속도를 콘솔에 출력한다.\n" +
	         "부스터로 전력 가속해보고 찍힌 '최대'값을 Accel For Full Vibration에 넣으면 딱 맞는다.\n" +
	         "튜닝 끝나면 반드시 끌 것.")]
	[SerializeField] private bool logAcceleration = false;

	[Tooltip("체크하면 피격할 때마다 받은 데미지와 충격 세기(%)를 콘솔에 출력한다.\n" +
	         "총알/폭탄이 각각 몇 %로 잡히는지 보고 Damage For Full Impact를 조절하면 된다.")]
	[SerializeField] private bool logImpact = false;

	[Header("추가로 제외할 오브젝트 이름 (부분 일치)")]
	[Tooltip("카메라는 자동 제외됨. 그 외에 안 흔들리게 할 부품이 있으면 여기에 이름 일부를 적기")]
	[SerializeField] private string[] extraExcludeNames = new string[0];

	// 흔들 대상(자식들)과 각자의 원래 위치
	private readonly List<Transform> _targets        = new List<Transform>();
	private readonly List<Vector3>   _startPositions = new List<Vector3>();

	private float _currentAmount;
	private float _targetAmount;
	private float _impact;         // 피격 충격분 (시간이 지나면 0으로 감쇠)

	private float _currentSpeed;   // 부스터에 따라 speed↔boostSpeed 사이를 오감
	private float _targetSpeed;

	// 자동 연동용 — 지난 프레임의 체력/실드를 기억해두고 줄어든 만큼을 충격으로 변환
	private int  _prevHp;
	private int  _prevShield;
	private bool _statsInitialized;
	private bool _shieldDown;      // 히스테리시스가 적용된 "쉴드 깨짐" 상태

	// 가속도 방식용 — 물리 갱신(FixedUpdate)에서 속도 변화량을 재서 가속도를 구한다.
	private Rigidbody _rb;
	private Vector3   _prevVelocity;
	private float     _smoothedAccel;
	private bool      _velocityInitialized;
	private float     _peakAccel;      // 디버그 표시용 최대 가속도
	private float     _lastLogTime;

	private void Start()
	{
		CollectTargets();

		// 시작은 정지 상태이므로 활공값에서 출발 (자동 모드면 첫 프레임에 실제 상태로 갱신됨)
		_currentAmount = autoTrackUnit ? coastAmount : idleAmount;
		_targetAmount  = _currentAmount;
		_currentSpeed  = speed;
		_targetSpeed   = speed;

		if (autoTrackUnit)
		{
			ResolveUnit();
		}
	}

	/// <summary>감시할 Unit 찾기 — 부모 우선, 없으면 씬에서 Player 탐색.</summary>
	private void ResolveUnit()
	{
		if (trackedUnit != null)
		{
			return;
		}

		// 1) 콕핏이 기체 자식으로 들어가 있는 경우
		trackedUnit = GetComponentInParent<Unit>();

		// 2) 콕핏이 기체와 분리돼 씬에 따로 있는 경우 — 씬에서 플레이어를 찾는다.
		if (trackedUnit == null)
		{
			trackedUnit = FindObjectOfType<Player>();
		}

		if (trackedUnit == null)
		{
			Debug.LogWarning($"[CockpitVibration] {name}: 감시할 기체(Unit)를 못 찾았습니다. " +
			                 "인스펙터의 Tracked Unit에 직접 연결하거나, 자동 연동을 끄고 SetBoost/AddImpact를 직접 호출하세요.", this);
			return;
		}

		// 가속도 방식용 Rigidbody 캐시
		_rb = trackedUnit.GetComponent<Rigidbody>();
		_velocityInitialized = false;

		if (_rb == null && vibrationMode == VibrationMode.Acceleration)
		{
			Debug.LogWarning($"[CockpitVibration] {name}: 기체에 Rigidbody가 없어 가속도 방식을 쓸 수 없습니다. " +
			                 "Vibration Mode를 StateSteps로 바꾸세요.", this);
		}
	}

	/// <summary>
	/// 가속도 측정. 속도는 물리 스텝(FixedUpdate)에서 갱신되므로 여기서 재야 값이 안정적이다.
	/// 진공에서 몸이 느끼는 힘 = 가속도 → 등속 활공은 0, 추진/감속 중에만 값이 생긴다.
	/// </summary>
	private void FixedUpdate()
	{
		if (!autoTrackUnit || vibrationMode != VibrationMode.Acceleration || _rb == null)
		{
			return;
		}

		Vector3 velocity = _rb.velocity;

		if (!_velocityInitialized)
		{
			_prevVelocity = velocity;
			_velocityInitialized = true;
			return;
		}

		float dt = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
		float accel = (velocity - _prevVelocity).magnitude / dt;
		_prevVelocity = velocity;

		// 프레임마다 튀는 값을 부드럽게 — 안 하면 진동이 지직거린다.
		_smoothedAccel = Mathf.Lerp(_smoothedAccel, accel, Mathf.Clamp01(accelSmoothing * dt));

		if (logAcceleration)
		{
			_peakAccel = Mathf.Max(_peakAccel, _smoothedAccel);

			if (Time.time - _lastLogTime >= 1f)
			{
				_lastLogTime = Time.time;
				float reference = GetAccelReference();
				float ratio = Mathf.Clamp01(_smoothedAccel / Mathf.Max(1f, reference));
				string source = autoDeriveAccelReference ? "자동" : "수동";
				Debug.Log($"[CockpitVibration] 가속도 현재={_smoothedAccel:F1} 최대={_peakAccel:F1} " +
				          $"(기준 {reference:F0}[{source}] → 진동 {ratio * 100f:F0}%)");
			}
		}
	}

	/// <summary>자식들 중 카메라가 아닌 것만 흔들 대상으로 수집.</summary>
	private void CollectTargets()
	{
		_targets.Clear();
		_startPositions.Clear();

		foreach (Transform child in transform)
		{
			if (IsExcluded(child)) continue;

			_targets.Add(child);
			_startPositions.Add(child.localPosition);
		}

		if (_targets.Count == 0)
			Debug.LogWarning($"[CockpitVibration] {name}: 흔들 대상이 없습니다. 콕핏 루트(메시들의 부모)에 붙였는지 확인하세요.", this);
	}

	/// <summary>카메라가 딸린 가지는 절대 흔들지 않음(VR 멀미 방지).</summary>
	private bool IsExcluded(Transform child)
	{
		// 1) 이 가지 안에 카메라가 하나라도 있으면 제외 — 가장 확실한 판별
		if (child.GetComponentInChildren<Camera>(true) != null) return true;

		// 2) 이름으로도 한 번 더 거름 (카메라 컴포넌트가 아직 없는 리그 대비)
		string lower = child.name.ToLower();
		if (lower.Contains("xr") || lower.Contains("camera") || lower.Contains("rig") || lower.Contains("offset"))
			return true;

		// 3) 인스펙터에서 지정한 예외
		for (int i = 0; i < extraExcludeNames.Length; i++)
		{
			string ex = extraExcludeNames[i];
			if (!string.IsNullOrEmpty(ex) && child.name.Contains(ex)) return true;
		}

		return false;
	}

	private void Update()
	{
		if (autoTrackUnit)
		{
			TrackUnitState();
		}

		// 부스터 On/Off에 따라 크기와 빠르기를 부드럽게 전환 (뚝 끊기지 않게)
		_currentAmount = Mathf.Lerp(_currentAmount, _targetAmount, blendSpeed * Time.deltaTime);
		_currentSpeed  = Mathf.Lerp(_currentSpeed,  _targetSpeed,  blendSpeed * Time.deltaTime);

		// 피격 충격은 시간이 지나면 0으로 잦아듦
		if (_impact > 0.0001f)
			_impact = Mathf.Lerp(_impact, 0f, impactDecay * Time.deltaTime);
		else
			_impact = 0f;

		float amount = _currentAmount + _impact;

		// PerlinNoise: 랜덤이지만 부드럽게 이어지는 값 → 덜덜거리지 않고 웅웅거리는 진동
		float t = Time.time * _currentSpeed;
		Vector3 offset = new Vector3(
			(Mathf.PerlinNoise(t,   0f) - 0.5f) * amount,
			(Mathf.PerlinNoise(0f,  t ) - 0.5f) * amount,
			(Mathf.PerlinNoise(t,  99f) - 0.5f) * amount * 0.5f);

		// 쉴드가 깨졌으면 빠르고 불규칙한 떨림을 덧씌운다 — "크게"가 아니라 "거칠게"(VR 멀미 완화)
		if (_shieldDown && shieldDownRoughness > 0f)
		{
			float rt = Time.time * roughnessSpeed;
			offset += new Vector3(
				(Mathf.PerlinNoise(rt, 31f) - 0.5f) * shieldDownRoughness,
				(Mathf.PerlinNoise(53f, rt) - 0.5f) * shieldDownRoughness,
				(Mathf.PerlinNoise(rt, 77f) - 0.5f) * shieldDownRoughness * 0.5f);
		}

		// 모든 대상에 같은 offset을 적용 → 계기판 전체가 한 덩어리로 흔들리는 것처럼 보임
		for (int i = 0; i < _targets.Count; i++)
		{
			Transform target = _targets[i];
			if (target == null) continue;   // 도중에 파괴된 경우 방어

			target.localPosition = _startPositions[i] + offset;
		}
	}

	/// <summary>
	/// 기체 상태를 읽어 부스터/피격/쉴드를 자동 반영.
	/// Player.cs·Unit.cs를 수정하지 않고 public 필드만 읽는다(병합 충돌 방지).
	/// </summary>
	private void TrackUnitState()
	{
		if (trackedUnit == null)
		{
			// 멀티에선 플레이어가 런타임에 스폰되므로 아직 없을 수 있음 — 계속 재시도
			ResolveUnit();
			if (trackedUnit == null) return;
		}

		// ── 엔진 진동 ──
		if (vibrationMode == VibrationMode.Acceleration && _rb != null)
		{
			ApplyAccelerationVibration();
		}
		else
		{
			ApplyStateVibration(trackedUnit.curState);
		}

		int hp     = trackedUnit.curHpRemaining;
		int shield = trackedUnit.curShieldRemaining;

		// 첫 프레임엔 "줄어든 양"을 계산할 기준이 없으므로 기억만 하고 넘어간다.
		if (!_statsInitialized)
		{
			_prevHp = hp;
			_prevShield = shield;
			_statsInitialized = true;
			return;
		}

		// ── 쉴드 깨짐 판정 (히스테리시스) ──
		// 0 근처에서 회복/피격이 반복될 때 진동 모드가 깜빡이는 것을 막는다.
		// (Player.cs의 BRAKE 상태가 쓰는 것과 같은 기법)
		int maxShield = trackedUnit.maxShieldCapacity;
		if (_shieldDown)
		{
			// 충분히 회복돼야 정상 복귀
			if (maxShield > 0 && shield >= maxShield * shieldRecoverRatio)
			{
				_shieldDown = false;
			}
		}
		else
		{
			if (shield <= 0)
			{
				_shieldDown = true;
			}
		}

		// ── 피격 충격 ──
		// 이번 프레임에 줄어든 HP/실드를 합쳐서 충격 크기로 환산.
		int hpLost     = Mathf.Max(0, _prevHp - hp);
		int shieldLost = Mathf.Max(0, _prevShield - shield);
		int totalLost  = hpLost + shieldLost;

		if (totalLost > 0)
		{
			// 받은 데미지 절대값으로 충격 크기를 정한다 — 기체 HP가 얼마든 "총알은 총알, 폭탄은 폭탄".
			float raw = Mathf.Clamp01(totalLost / Mathf.Max(1f, damageForFullImpact));
			// 곡선 적용 — 실제 피격은 대부분 작은 값(3~12)이라 직선으로 매핑하면 전부 뭉쳐서 구분이 안 된다.
			float ratio = Mathf.Pow(raw, impactCurve);
			float strength = ratio * maxImpactAmount;

			if (logImpact)
			{
				Debug.Log($"[CockpitVibration] 피격 데미지={totalLost} (HP {hpLost} + 실드 {shieldLost}) " +
				          $"→ 충격 {ratio * 100f:F0}%{(_shieldDown ? "" : " (쉴드 흡수 적용)")}");
			}

			// 쉴드가 살아있으면 충격을 흡수 — 쉴드 유무 차이가 여기서 확실히 난다.
			if (!_shieldDown)
			{
				strength *= shieldedImpactScale;
			}

			AddImpact(strength);
		}

		_prevHp = hp;
		_prevShield = shield;
	}

	/// <summary>
	/// 가속도에 비례해 진동을 정한다(연속).
	/// 단계가 없으므로 경계 깜빡임이 원천적으로 없고, "살짝 밀기 ~ 전력 부스터" 사이가 전부 표현된다.
	/// 등속 활공(가속도 0)은 자동으로 coastAmount만 남아 고요해진다 — 진공 물리와 일치.
	/// </summary>
	private void ApplyAccelerationVibration()
	{
		float ratio = Mathf.Clamp01(_smoothedAccel / Mathf.Max(1f, GetAccelReference()));

		_targetAmount = coastAmount + ratio * maxAccelAmount;
		// 세게 밀수록 빨리 떨림 — 크기와 결이 같이 변해야 "으르렁"거리는 느낌이 난다.
		_targetSpeed  = Mathf.Lerp(speed, boostSpeed, ratio);
	}

	/// <summary>
	/// "진동 100%가 되는 가속도" 기준값.
	///
	/// 자동 계산이 켜져 있으면 기체 스탯에서 직접 뽑는다 —
	///   최대 가속도 = boostSpeed ÷ timeToMaxSpeed
	/// 이건 Player.cs가 실제로 가속할 때 쓰는 식(targetSpeed ÷ timeToMaxSpeed)과 같으므로,
	/// 기체가 바뀌든 스러스터 파츠로 속도가 바뀌든 항상 "그 기체의 전력 가속 = 진동 100%"가 된다.
	/// 매 프레임 다시 계산하는 이유: 파츠 장착/파괴로 스탯이 런타임에 변하기 때문(나눗셈 1회라 부담 없음).
	/// </summary>
	private float GetAccelReference()
	{
		if (!autoDeriveAccelReference || trackedUnit == null)
		{
			return accelForFullVibration;
		}

		float boost = trackedUnit.boostSpeed;
		float time  = trackedUnit.timeToMaxSpeed;

		// 스탯이 아직 안 채워진 기체(0)면 수동값으로 대체 — 0으로 나누는 사고 방지
		if (boost <= 0f || time <= 0f)
		{
			return accelForFullVibration;
		}

		return boost / time;
	}

	/// <summary>
	/// 엔진 상태에 따라 진동 목표치를 정한다.
	///   IDLE/BRAKE  = 엔진 꺼짐(관성 활공) → coastAmount(기본 0). 진공엔 저항이 없으므로 고요한 게 맞다.
	///   MOVING/DODGE= 추진 중              → idleAmount
	///   BOOSTING    = 최대 출력            → boostAmount
	/// 바닥을 조용하게 두는 이유: 사람은 절대 크기가 아니라 '변화량'을 느끼므로,
	/// 항상 떨리면 뇌가 배경으로 걸러내서 부스터·피격의 체감이 반감된다.
	/// </summary>
	private void ApplyStateVibration(UNIT_STATE state)
	{
		switch (state)
		{
			case UNIT_STATE.BOOSTING:
				_targetAmount = boostAmount;
				_targetSpeed  = boostSpeed;
				break;

			case UNIT_STATE.MOVING:
			case UNIT_STATE.DODGE:
				_targetAmount = idleAmount;
				_targetSpeed  = speed;
				break;

			// IDLE / BRAKE / HIT / DIE — 엔진이 일하지 않는 상태
			default:
				_targetAmount = coastAmount;
				_targetSpeed  = speed;
				break;
		}
	}

	/// <summary>
	/// 부스터 켜짐/꺼짐 수동 전환 (autoTrackUnit을 껐을 때 외부에서 호출).
	/// 자동 모드에선 ApplyStateVibration이 매 프레임 덮어쓰므로 효과 없음.
	/// </summary>
	public void SetBoost(bool on)
	{
		_targetAmount = on ? boostAmount : idleAmount;
		_targetSpeed  = on ? boostSpeed  : speed;
	}

	/// <summary>폭발·피격 시 순간 충격. 예) AddImpact(0.05f) — 숫자가 클수록 세게.</summary>
	public void AddImpact(float strength)
	{
		_impact = Mathf.Max(_impact, strength);
	}

	/// <summary>런타임에 콕핏 자식 구성이 바뀌었을 때 다시 수집.</summary>
	public void Refresh()
	{
		CollectTargets();
	}
}
