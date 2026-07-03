using UnityEngine;

// =====================================================================
// LaserChargeVisual
//
// F3DPulsewave의 "파동" 연출(ScaleSize에서 0으로 축소 + 지연 후 페이드아웃)을
// 우리식으로 정리해서 레이저 차지 이펙트에 쓰는 버전.
//
// - F3DTime 타이머 / OnSpawned·OnDespawned(F3D 풀 훅) / DebugLoop 제거.
// - 활성화(OnEnable)되면 한 사이클을 시작하고, 끝나면 자동으로 다시 시작해 계속 반복.
// - 반환/비활성은 풀매니저(VFXManager)가 SetActive(false)로 처리 → 여기선 enable만 구분.
//   비활성이면 Update가 안 돌아 자동으로 멈추고, 재사용(재활성) 시 OnEnable이 새 사이클로 리셋.
//
// [프리팹] _TintColor를 가진 머티리얼의 MeshRenderer가 붙은 오브젝트 루트에 부착.
// =====================================================================
public class LaserChargeVisual : MonoBehaviour
{
	[Tooltip("축소 시작 후 페이드 시작까지 지연(초)")]
	public float FadeOutDelay;
	[Tooltip("페이드 속도")]
	public float FadeOutTime;
	[Tooltip("축소 속도(0으로 수렴하는 Lerp 계수)")]
	public float ScaleTime;
	[Tooltip("사이클 시작 크기(여기서 0으로 줄어듦)")]
	public Vector3 ScaleSize;

	private Transform _tr;
	private MeshRenderer _renderer;
	private int _tintColorRef;
	private Color _defaultColor;   // 원본 파동 색
	private Color _color;          // 현재 색

	private float _fadeStartTime;  // 페이드 시작 예정 시각
	private bool _isFadeOut;

	private void Awake()
	{
		_tr = transform;
		_renderer = GetComponent<MeshRenderer>();
		_tintColorRef = Shader.PropertyToID("_TintColor");
		_defaultColor = _renderer.material.GetColor(_tintColorRef);
	}

	private void OnEnable()
	{
		StartCycle();
	}

	// 한 사이클 초기화. 활성화 시 + 사이클이 끝날 때마다 호출해 반복.
	private void StartCycle()
	{
		_tr.localScale = ScaleSize;
		_isFadeOut = false;
		_fadeStartTime = Time.time + FadeOutDelay;

		_renderer.material.SetColor(_tintColorRef, _defaultColor);
		_color = _defaultColor;
	}

	private void Update()
	{
		// 파동 축소(ScaleSize → 0)
		_tr.localScale = Vector3.Lerp(_tr.localScale, Vector3.zero, Time.deltaTime * ScaleTime);

		// 지연 시간 지나면 페이드 시작
		if (!_isFadeOut && Time.time >= _fadeStartTime)
		{
			_isFadeOut = true;
		}

		if (_isFadeOut)
		{
			_color = Color.Lerp(_color, new Color(0f, 0f, 0f, -0.1f), Time.deltaTime * FadeOutTime);
			_renderer.material.SetColor(_tintColorRef, _color);

			// 사이클 끝(거의 투명) → 다시 시작해서 반복
			if (_color.a <= 0.1f)
			{
				StartCycle();
			}
		}
	}
}
