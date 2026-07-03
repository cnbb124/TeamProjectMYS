using UnityEngine;

// ================================================================
// [LaserBeamVisual — 크로스 쿼드 빔 시각]
// ================================================================
// 추격 카메라(시선 ≈ 빔 축) 구도에서도 빔이 판때기로 안 보이게, 몸통을 서로 교차하는 쿼드(+ 단면)로 그림. 빌보드/LineRenderer의 "정면 붕괴" 한계를 없앤 버전.
// (이전엔 F3DBeam의 LineRenderer 방식을 이식했으나, 정면에서 단면이 평면으로 보이는
//  문제로 크로스 쿼드 메시 방식으로 교체함.)
//
// 판정(데미지·사거리·관통·막힘)은 LaserSkill이 하고, 그 결과 길이(length)만
// SetLength()로 넘겨받아 그리기만 함.
//
// ── 프리팹 구조 (인스펙터 연결) ────────────────────────────────
//   root(LaserBeamVisual)
//   ├── _visual        : 크로스 쿼드들을 담은 컨테이너. z=길이 / x·y=두께로 스케일됨.
//   │                    쿼드 blade는 +Z로 1유닛(z:0~1), 폭 1유닛 기준이어야 SetLength가 정확.
//   │                    blade 두 장은 빔 축(Z) 기준 90° 벌려 + 단면을 이룰 것.
//   ├── _impactPoint   : 빔 끝(타격점)에 둘 이펙트. z=길이로 이동. 크기는 두께 비례(옵션).
//   └── _muzzlePoint   : 빔 시작(총구)에 둘 이펙트. z=0 고정. 크기는 두께 비례(옵션).
//   _beamRenderers     : 몸통 blade들의 Renderer. UV는 MaterialPropertyBlock(_MainTex_ST)로
//                        먹여 머티리얼 인스턴스 복제 없이 렌더러별로 적용(GPU Instancing 호환).
//
// ⚠ 몸통 색/밝기: F3D/Additive 셰이더는 vertexColor를 곱하는데 프리미티브 Quad는
//   vertexColor가 흰색이라, 색은 머티리얼 _TintColor / 밝기는 _Boost로 조절할 것.
// ⚠ UV 길이 방향 = U축 기준으로 타일링/스크롤함(blade를 Y 90° 회전해 폭축(U)이 길이(Z)와
//   맞도록 만든 전제). 텍스처가 길이가 아니라 폭 방향으로 타일링되면 ApplyUV의 x/y만 바꾸면 됨.
// ================================================================
public class LaserBeamVisual : MonoBehaviour
{
	[Header("빔 몸통 (크로스 쿼드) — 스케일 대상")]
	[Tooltip("크로스 쿼드 컨테이너. z=길이 / x·y=두께로 스케일. blade base는 +Z 1유닛, 폭 1유닛")]
	[SerializeField] private Transform _visual;
	[Tooltip("몸통 blade Renderer들. UV 스크롤/타일링을 MaterialPropertyBlock으로 적용")]
	[SerializeField] private Renderer[] _beamRenderers;

	[Header("끝/시작점 이펙트 (위치 + 두께 비례 크기)")]
	[Tooltip("(선택) 빔 시작점(총구)에 둘 이펙트 transform. z=0 고정. 없으면 무시")]
	[SerializeField] private Transform _muzzlePoint;
	[Tooltip("(선택) 빔 끝점(타격점)에 둘 이펙트 transform. localPosition.z=길이. 없으면 무시")]
	[SerializeField] private Transform _impactPoint;
	[Tooltip("켜면 flare/muzzle 크기를 빔 두께에 비례해 조절. ⚠파티클 Scaling Mode=Hierarchy 필요")]
	[SerializeField] private bool _scaleEndpointsWithWidth = true;

	[Header("UV")]
	[Tooltip("UV 스크롤 속도. 양수 = 총구→타격점(바깥) 방향으로 흐름. 0이면 정지, 음수면 반대")]
	[SerializeField] private float _uvScrollSpeed = 5f;
	[Tooltip("빔 길이 1당 텍스처 반복 수. 빔이 늘어나도 텍스처가 안 늘어지게 길이에 비례해 타일링")]
	[SerializeField] private float _textureTilingPerUnit = 0.1f;

	[Header("회전")]
	[Tooltip("빔 축(Z) 기준 초당 회전 속도(도/초). 0이면 정지. 발사 중 크로스가 빙빙 도는 연출")]
	[SerializeField] private float _spinSpeed = 0f;

	private float _width = 1f;
	private float _length = 1f;
	private float _uvOffset;
	private MaterialPropertyBlock _mpb;
	private static readonly int _mainTexST = Shader.PropertyToID("_MainTex_ST");

	// 끝/시작 이펙트의 authored 스케일(두께 1 기준). SetWidth에서 width를 곱해 두께 비례로 키움.
	private Vector3 _impactBaseScale = Vector3.one;
	private Vector3 _muzzleBaseScale = Vector3.one;
	//  발사 시작 시 여기로 리셋해 풀 재사용에도 회전 누적 안 되게 함.
	private Quaternion _visualBaseRotation = Quaternion.identity;

	private void Awake()
	{
		_mpb = new MaterialPropertyBlock();
		_uvOffset = Random.Range(0f, 5f);          // 시작 UV 랜덤(여러 빔 겹칠 때 패턴 분산)
		_visualBaseRotation = _visual.localRotation;
		// authored 스케일 캡처(풀 재사용에도 불변). 이후 SetWidth에서 이 값 × width로 적용.
		if (_impactPoint != null)
		{
			_impactBaseScale = _impactPoint.localScale;
		}
		if (_muzzlePoint != null)
		{
			_muzzleBaseScale = _muzzlePoint.localScale;
		}
		if (_visual != null)
		{
			_visualBaseRotation = _visual.localRotation;
		}
	}

	/// <summary>
	/// LaserSkill이 매 틱 호출. 전방(+z)으로 length만큼 빔을 그림.
	/// length는 LaserSkill의 판정 결과(hit 거리 또는 최대 사거리)와 동일 → 시각과 판정이 일치.
	/// </summary>
	public void SetLength(float length)
	{
		_length = length;
		ApplyScale();
		ApplyUV();

		// 끝/시작점 이펙트 위치(로컬) — 컨테이너 스케일과 분리해서 플레어 크기 왜곡 방지
		if (_impactPoint != null)
		{
			_impactPoint.localPosition = new Vector3(0f, 0f, length);
		}
		if (_muzzlePoint != null)
		{
			_muzzlePoint.localPosition = Vector3.zero;
		}
	}

	/// <summary>
	/// 빔 시각 두께 설정. LaserSkill이 발사 시 1회 호출(판정 SphereCast 반경 × 2 = 지름).
	/// _visual의 x·y 스케일을 조절 → 프리팹 blade base 폭(1유닛)에 곱해져 실제 두께가 됨.
	/// </summary>
	public void SetWidth(float width)
	{
		_width = width;
		ApplyScale();

		// 발사 시작마다 회전을 base로 리셋(풀 재사용 시 이전 발사의 누적 회전 제거).
		if (_visual != null)
		{
			_visual.localRotation = _visualBaseRotation;
		}

		// flare/muzzle 크기도 두께 비례로 (base 폭 1 기준 authored 스케일 × width)
		if (_scaleEndpointsWithWidth)
		{
			if (_impactPoint != null)
			{
				_impactPoint.localScale = _impactBaseScale * width;
			}
			if (_muzzlePoint != null)
			{
				_muzzlePoint.localScale = _muzzleBaseScale * width;
			}
		}
	}

	/// <summary>
	/// 빔 끝(타격점) 이펙트 표시 여부. LaserSkill이 매 프레임 호출 —
	/// BlocksBeam 대상에 실제로 막혔을 때만 true(허공 max range면 false로 공중에 안 뜨게).
	/// 파티클 재시작을 막기 위해 상태가 바뀔 때만 SetActive.
	/// </summary>
	public void SetImpactActive(bool active)
	{
		if (_impactPoint != null && _impactPoint.gameObject.activeSelf != active)
		{
			_impactPoint.gameObject.SetActive(active);
		}
	}

	// _visual 컨테이너를 z=길이 / x·y=두께로 스케일. blade들이 90° 축정렬이라 비균등 스케일에도 스큐 없음.
	private void ApplyScale()
	{
		if (_visual != null)
		{
			_visual.localScale = new Vector3(_width, _width, _length);
		}
	}

	private void Update()
	{
		// UV 스크롤(레이저 흐르는 애니메이션)
		if (_uvScrollSpeed != 0f)
		{
			_uvOffset -= Time.deltaTime * _uvScrollSpeed;   // 양수 = 바깥(총구→타격점) 방향
			ApplyUV();
			
		}
		// 빔 축(Z) 기준 회전 — 크로스 단면이 빙빙 돎. 음수면 반대방향. x=y(두께 균등)라 스큐 없음.
		if (_spinSpeed != 0f && _visual != null)
		{
			_visual.Rotate(0f, 0f, _spinSpeed * Time.deltaTime, Space.Self);
		}
	}

	// 길이비례 타일링 + 스크롤을 MaterialPropertyBlock으로 각 몸통 렌더러에 적용.
	// .material(인스턴스 복제) 대신 MPB라 GPU Instancing과 호환되고 머티리얼 누수 없음.
	private void ApplyUV()
	{
		if (_beamRenderers == null)
		{
			return;
		}

		// _MainTex_ST = (scaleX, scaleY, offsetX, offsetY). U축(x)을 길이 방향으로 타일링/스크롤.
		Vector4 st = new Vector4(_length * _textureTilingPerUnit, 1f, _uvOffset, 0f);

		for (int i = 0; i < _beamRenderers.Length; i++)
		{
			Renderer r = _beamRenderers[i];
			if (r == null)
			{
				continue;
			}
			r.GetPropertyBlock(_mpb);
			_mpb.SetVector(_mainTexST, st);
			r.SetPropertyBlock(_mpb);
		}
	}

	public void ResetVisualRotation()
	{
		if (_visual != null)
		{
			_visual.localRotation = _visualBaseRotation;
		}
	}
}
