using UnityEngine;

// =====================================================================
// WarpTunnelVisual
//
// FORGE3D의 F3DWarpJumpTunnel을 VFXManager 풀링에 맞게 재작성한 버전.
// 워프 터널 '메시' 이펙트를 스케일 성장 + 알파 페이드인/아웃 + 회전으로 연출한다.
// (파티클이 아니라 메시+셰이더라서 코드가 직접 애니메이션을 굴려줘야 함)
//
// 원본과 달라진 점(풀링 대응):
//   - F3DTime 타이머 의존 제거 → OnEnable 기준 자체 경과시간(_elapsed)으로 페이즈 진행.
//   - OnSpawned() 외부 호출 불필요 → OnEnable에서 자동 리셋/시작(풀 재사용마다 처음부터).
//   - 반납은 VFXManager가 담당 → PlayEffectAtPosition의 duration으로 수명 지정(메시라 duration 방식).
//     (프리팹에 EffectAutoReturn 불필요. 이건 파티클 종료 콜백용이라 메시엔 안 맞음)
//   - 일시정지 인지: IsGameplayFrozen이면 애니메이션 정지(elapsed도 안 흐름).
//
// 타임라인:
//   [0 ~ startDelay]        : 대기(알파 0 유지, 회전만)
//   [startDelay ~ fadeDelay]: 성장 + 알파 페이드인
//   [fadeDelay ~ ]          : 알파 페이드아웃
// =====================================================================
[RequireComponent(typeof(MeshRenderer))]
public class WarpTunnelVisual : MonoBehaviour
{
	[Header("타이밍(초)")]
	[Tooltip("활성화 후 이 시간 뒤부터 성장/페이드인 시작")]
	public float startDelay = 0f;
	[Tooltip("활성화 후 이 시간 뒤부터 페이드아웃 시작(startDelay보다 커야 함)")]
	public float fadeDelay = 1f;

	[Header("성장(스케일)")]
	[Tooltip("성장 후 도달할 목표 로컬 스케일")]
	public Vector3 scaleTo = new Vector3(3f, 3f, 3f);
	[Tooltip("목표 스케일로 접근하는 속도(Lerp 계수)")]
	public float scaleTime = 3f;

	[Header("알파 페이드")]
	[Tooltip("페이드인 속도(Lerp 계수)")]
	public float colorTime = 3f;
	[Tooltip("페이드아웃 속도(Lerp 계수)")]
	public float colorFadeTime = 3f;

	[Header("회전")]
	[Tooltip("초당 회전 각도(Z축)")]
	public float rotationSpeed = 30f;

	private MeshRenderer _meshRenderer;
	private Material _material;          // 인스턴스 머티리얼(공유 머티리얼 오염 방지용으로 .material 사용)
	private int _alphaID;                // 셰이더 "_Alpha" 프로퍼티 ID
	private static readonly Vector3 _baseScale = Vector3.one;

	private float _elapsed;
	private float _alpha;

	private void Awake()
	{
		_meshRenderer = GetComponent<MeshRenderer>();
		_material = _meshRenderer.material;   // 인스턴스화(각 이펙트가 자기 알파를 독립적으로 가짐)
		_alphaID = Shader.PropertyToID("_Alpha");
	}

	// 풀에서 꺼내 활성화될 때마다 처음 상태로 리셋 + 랜덤 회전(원본 연출 유지).
	private void OnEnable()
	{
		_elapsed = 0f;
		_alpha = 0f;
		transform.localScale = _baseScale;
		transform.localRotation = transform.localRotation * Quaternion.Euler(0f, 0f, Random.Range(-360f, 360f));
		if (_material != null)
		{
			_material.SetFloat(_alphaID, 0f);
		}
	}

	private void Update()
	{
		// 일시정지/게임오버 중엔 연출 정지(경과시간도 안 흐름).
		if (GameManager.Instance != null && GameManager.Instance.IsGameplayFrozen)
		{
			return;
		}

		float dt = Time.deltaTime;

		// 회전은 항상.
		transform.Rotate(0f, 0f, rotationSpeed * dt);

		_elapsed += dt;

		// startDelay~fadeDelay 구간이면 성장 + 페이드인, 그 외엔 페이드아웃.
		bool grow = _elapsed >= startDelay && _elapsed < fadeDelay;
		if (grow)
		{
			transform.localScale = Vector3.Lerp(transform.localScale, scaleTo, dt * scaleTime);
			_alpha = Mathf.Lerp(_alpha, 1f, dt * colorTime);
		}
		else
		{
			_alpha = Mathf.Lerp(_alpha, 0f, dt * colorFadeTime);
		}

		if (_material != null)
		{
			_material.SetFloat(_alphaID, _alpha);
		}
	}
}
