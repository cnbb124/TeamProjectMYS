using UnityEngine;

// ================================================================
// [LaserBeamVisual — 빔 시각 전용 컴포넌트]
// ================================================================
// FORGE3D의 F3DBeam에서 "빔 시각"(LineRenderer 길이 + UV 스크롤 + 텍스처 스케일 +
// 끝/시작점 이펙트 위치)만 가져오고, 자체 Raycast/판정/힘/외부매니저(F3DFXController,
// F3DTime) 의존은 전부 제거한 버전.
//
// 판정(데미지·사거리·관통·막힘)은 LaserSkill이 하고, 그 결과 길이(length)만
// SetLength()로 넘겨받아 그리기만 함.
//
// [사용]
// - 빔 프리팹(plasma_beam 등)에서 F3DBeam 컴포넌트를 제거하고 이 컴포넌트를 부착.
// - LineRenderer/머티리얼/텍스처/부속 파티클은 그대로 유지 → 보이는 건 동일.
// - owner에 부착(SetParent)된 상태로 쓰므로 LineRenderer는 로컬 좌표(전방 +z) 기준.
// ================================================================
[RequireComponent(typeof(LineRenderer))]
public class LaserBeamVisual : MonoBehaviour
{
	[Tooltip("UV 스크롤 속도(레이저 흐르는 느낌). 0이면 정지")]
	[SerializeField] private float _uvScrollSpeed = 5f;
	[Tooltip("빔 길이 1당 텍스처 반복 수. 빔이 늘어나도 텍스처가 안 늘어지게 길이에 비례해 타일링")]
	[SerializeField] private float _textureTilingPerUnit = 0.1f;

	[Tooltip("(선택) 빔 끝점에 둘 이펙트 transform. 프리팹 자식 파티클 연결. 없으면 무시")]
	[SerializeField] private Transform _impactPoint;
	[Tooltip("(선택) 빔 시작점(총구)에 둘 이펙트 transform. 없으면 무시")]
	[SerializeField] private Transform _muzzlePoint;

	private LineRenderer _line;
	private float _uvOffset;

	private void Awake()
	{
		_line = GetComponent<LineRenderer>();
		_line.useWorldSpace = false;               // owner 부착 기준 로컬 좌표(전방 +z)
		_uvOffset = Random.Range(0f, 5f);          // 시작 UV 랜덤(여러 빔 겹칠 때 패턴 분산)
		_line.positionCount = 2;
	}

	/// <summary>
	/// LaserSkill이 매 틱 호출. 전방(+z)으로 length만큼 빔을 그림.
	/// length는 LaserSkill의 판정 결과(hit 거리 또는 최대 사거리)와 동일 → 시각과 판정이 일치.
	/// </summary>
	public void SetLength(float length)
	{
		_line.SetPosition(0, Vector3.zero);
		_line.SetPosition(1, new Vector3(0f, 0f, length));

		// 길이 비례 텍스처 스케일 — 빔이 늘어나도 텍스처가 늘어져 보이지 않게
		if (_line.material != null)
		{
			_line.material.SetTextureScale("_MainTex", new Vector2(length * _textureTilingPerUnit, 1f));
		}

		// 끝/시작점 이펙트 위치(로컬)
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
	/// LineRenderer의 widthMultiplier를 조절하므로, 프리팹의 widthCurve(끝이 가늘어지는 등) 형태는 유지되고 전체 굵기만 스케일됨.
	/// </summary>
	public void SetWidth(float width)
	{
		_line.widthMultiplier = width;
	}

	private void Update()
	{
		// UV 스크롤(레이저 흐르는 애니메이션)
		if (_uvScrollSpeed != 0f && _line.material != null)
		{
			_uvOffset += Time.deltaTime * _uvScrollSpeed;
			_line.material.SetTextureOffset("_MainTex", new Vector2(_uvOffset, 0f));
		}
	}
}
