using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// 콕핏의 조종간(스틱) / 스로틀 레버를 실제 입력에 맞춰 움직이는 연출 스크립트.
// InputManager의 공개 출력값만 읽으므로 팀 공유 파일을 수정하지 않는다.
//
//   StickGrabbed   / ThrottleGrabbed   : VR에서 손으로 잡았을 때 true로 켜면
//                                        그 부품은 이 스크립트가 건드리지 않는다(그랩이 우선).
//   SetStickGrabbed(bool) / SetThrottleGrabbed(bool)
//
// [입력 매핑 — 실제 조종간과 같은 방식]
//   스틱 앞뒤 기울기 ← lookInput.y   (기수 상하 = 피치)
//   스틱 좌우 기울기 ← rollInput     (Q/E 롤)
//   스틱 비틀기      ← lookInput.x   (좌우 선회 = 요) ※ 실제 HOTAS도 비틀어서 요를 낸다
//   스로틀 밀기      ← moveInput.z   (전후 추력) + 부스터면 끝까지
//
// ================================================================
// ⚠ [피벗 주의] 가장 흔한 실수
//   스틱은 "밑동"을 축으로 기울어져야 한다. 그런데 메시의 피벗이 한가운데 있으면
//   공중에서 빙글 도는 것처럼 보인다.
//   → 해결: 스틱 밑동 위치에 빈 오브젝트를 만들고, 스틱 메시를 그 자식으로 넣은 뒤
//           이 스크립트의 _stickPivot 에 "빈 오브젝트"를 연결한다.
//   (스로틀도 같은 원리 — 레버가 꺾이는 축에 빈 오브젝트를 둔다)
//
// [에디터 세팅]
//   1. 콕핏 아무 오브젝트에 이 스크립트 부착
//   2. _stickPivot    : CockpitEquipments_Joystick2-Handle (또는 위에서 만든 밑동 피벗)
//   3. _throttlePivot : CockpitEquipments_ThrottleControl1-Handle1 (레버)
//   4. Play → 마우스/Q/E/W 조작하면 따라 움직임
//
// [나중에 VR 그랩 붙일 때]
//   XRGrabInteractable 의 Select Entered → SetStickGrabbed(true)
//                        Select Exited  → SetStickGrabbed(false)
//   이러면 잡는 동안은 손이, 놓으면 다시 입력이 조종간을 움직인다.
//   여기서 정한 최대 각도(_stickMaxPitch 등)를 그랩 회전 제한값으로 그대로 쓰면 된다.
// ================================================================
public class CockpitControlsAnimator : MonoBehaviour
{
	[Header("── 조종간(스틱) ──")]
	[Tooltip("기울일 대상. 반드시 '밑동'이 회전축이어야 한다(위 피벗 주의 참고).")]
	[SerializeField] private Transform _stickPivot;

	[Tooltip("앞뒤(피치) 최대 기울기 각도. 20~25도가 자연스럽다.")]
	[SerializeField] private float _stickMaxPitch = 20f;

	[Tooltip("좌우(롤) 최대 기울기 각도.")]
	[SerializeField] private float _stickMaxRoll = 20f;

	[Tooltip("비틀기(요) 최대 각도. 0으로 두면 비틀기 안 함.")]
	[SerializeField] private float _stickMaxYaw = 10f;

	[Tooltip("각 축의 방향이 반대로 움직이면 체크해서 뒤집는다.")]
	[SerializeField] private bool _invertPitch = false;
	[SerializeField] private bool _invertRoll  = false;
	[SerializeField] private bool _invertYaw   = false;

	[Header("── 스로틀 레버 ──")]
	[Tooltip("밀 대상. 회전축(레버가 꺾이는 지점)이 피벗이어야 한다.")]
	[SerializeField] private Transform _throttlePivot;

	[Tooltip("스로틀을 끝까지 밀었을 때의 각도. 후진(-1)일 땐 반대로 -값까지 간다.")]
	[SerializeField] private float _throttleMaxAngle = 25f;

	[Tooltip("스로틀이 회전하는 축(로컬 기준). 보통 X축(1,0,0). 엉뚱하게 돌면 여기를 바꾼다.")]
	[SerializeField] private Vector3 _throttleAxis = Vector3.right;

	[Tooltip("부스터일 때 최대치를 넘어 얼마나 더 미는지(배율). 1.2 = 20% 더")]
	[SerializeField] private float _boostOverpush = 1.2f;

	[SerializeField] private bool _invertThrottle = false;

	[Header("── 공통 ──")]
	[Tooltip("입력을 따라가는 속도. 클수록 즉각적, 작을수록 묵직하다. 8~15 권장")]
	[SerializeField] private float _followSpeed = 10f;

	[Tooltip("비워두면 InputManager.Instance 를 자동으로 쓴다.")]
	[SerializeField] private InputManager _input;

	// VR에서 손으로 잡고 있는 동안은 이 스크립트가 손을 뗀다.
	public bool StickGrabbed    { get; private set; }
	public bool ThrottleGrabbed { get; private set; }

	// 시작 시의 로컬 회전 — 여기서부터 상대적으로 기울인다(원래 배치각을 보존).
	private Quaternion _stickRestRotation;
	private Quaternion _throttleRestRotation;

	// 부드럽게 따라가기용 현재값
	private float _curPitch, _curRoll, _curYaw, _curThrottle;

	private void Start()
	{
		if (_stickPivot != null)
		{
			_stickRestRotation = _stickPivot.localRotation;
		}

		if (_throttlePivot != null)
		{
			_throttleRestRotation = _throttlePivot.localRotation;
		}

		if (_stickPivot == null && _throttlePivot == null)
		{
			Debug.LogWarning($"[CockpitControlsAnimator] {name}: 스틱/스로틀이 하나도 연결되지 않았습니다.", this);
		}
	}

	private void Update()
	{
		InputManager input = _input != null ? _input : InputManager.Instance;
		if (input == null)
		{
			return;   // 아직 초기화 전이거나 씬에 없음 — 조용히 대기
		}

		// ── 목표값 계산 (-1 ~ 1) ──
		float targetPitch = Mathf.Clamp(input.lookInput.y, -1f, 1f) * (_invertPitch ? -1f : 1f);
		float targetRoll  = Mathf.Clamp(input.rollInput,   -1f, 1f) * (_invertRoll  ? -1f : 1f);
		float targetYaw   = Mathf.Clamp(input.lookInput.x, -1f, 1f) * (_invertYaw   ? -1f : 1f);

		float targetThrottle = Mathf.Clamp(input.moveInput.z, -1f, 1f) * (_invertThrottle ? -1f : 1f);
		// 부스터면 최대치를 살짝 넘겨서 "끝까지 밀어붙인" 느낌
		if (input.isBoosting && targetThrottle > 0f)
		{
			targetThrottle *= _boostOverpush;
		}

		// ── 부드럽게 따라가기 ──
		float t = Mathf.Clamp01(_followSpeed * Time.deltaTime);
		_curPitch    = Mathf.Lerp(_curPitch,    targetPitch,    t);
		_curRoll     = Mathf.Lerp(_curRoll,     targetRoll,     t);
		_curYaw      = Mathf.Lerp(_curYaw,      targetYaw,      t);
		_curThrottle = Mathf.Lerp(_curThrottle, targetThrottle, t);

		ApplyStick();
		ApplyThrottle();
	}

	private void ApplyStick()
	{
		// VR에서 손으로 잡고 있으면 손이 우선 — 건드리지 않는다.
		if (_stickPivot == null || StickGrabbed)
		{
			return;
		}

		// 원래 배치각(_stickRestRotation)에서 상대적으로 기울인다.
		// 순서: 피치(X) → 롤(Z) → 요(Y). 실제 조종간의 짐벌 순서와 같다.
		Quaternion tilt = Quaternion.Euler(
			_curPitch * _stickMaxPitch,
			_curYaw   * _stickMaxYaw,
			-_curRoll * _stickMaxRoll);   // 롤은 부호를 뒤집어야 "오른쪽 입력 = 오른쪽으로 기움"이 된다

		_stickPivot.localRotation = _stickRestRotation * tilt;
	}

	private void ApplyThrottle()
	{
		if (_throttlePivot == null || ThrottleGrabbed)
		{
			return;
		}

		Vector3 axis = _throttleAxis.sqrMagnitude > 0.0001f ? _throttleAxis.normalized : Vector3.right;
		Quaternion push = Quaternion.AngleAxis(_curThrottle * _throttleMaxAngle, axis);

		_throttlePivot.localRotation = _throttleRestRotation * push;
	}

	/// <summary>VR에서 스틱을 잡았을 때 true. XRGrabInteractable의 Select Entered/Exited에 연결.</summary>
	public void SetStickGrabbed(bool grabbed)
	{
		StickGrabbed = grabbed;
	}

	/// <summary>VR에서 스로틀을 잡았을 때 true.</summary>
	public void SetThrottleGrabbed(bool grabbed)
	{
		ThrottleGrabbed = grabbed;
	}
}
