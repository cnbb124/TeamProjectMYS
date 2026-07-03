using UnityEngine;


public class VisualRandomize : MonoBehaviour
{
	// 자기 base 스케일(프리팹 원본)을 Awake에서 캡처 → OnEnable에서 base × random으로 절대 세팅.
	// 상대곱(*=)이 아니라 절대라서 풀 재사용해도 누적/드리프트 없음(외부 리셋에 의존 안 함).
	// ⚠ RandomScale은 "VFXManager가 scale을 안 넘기는 이펙트"(muzzle 등)에만 쓸 것.
	//    폭발처럼 VFXManager가 radius로 scale을 세팅하는 이펙트에 RandomScale을 켜면 그 radius를 덮어씀 →
	//    그런 이펙트는 RandomScale 끄고 RandomRotation만 사용(크기는 radius가 정확히 유지되어야 하니까).

	public bool RandomScale, RandomRotation; // Randomize flags
	public float MinScale, MaxScale; // Min/Max scale range
	public float MinRotation, MaxRotaion; // Min/Max rotation range

	private Vector3 _baseScale;   // 프리팹 원본 스케일(Awake 캡처, 불변)

	private void Awake()
	{
		_baseScale = transform.localScale;
	}

	// Randomize scale and rotation according to the values set in the inspector
	private void OnEnable()
	{
		if (RandomScale)
		{
			transform.localScale = _baseScale * Random.Range(MinScale, MaxScale);   // base 기준 절대 → 누적 없음
		}

		if (RandomRotation)
		{
			transform.rotation *= Quaternion.Euler(0, 0, Random.Range(MinRotation, MaxRotaion));
		}
	}
}
