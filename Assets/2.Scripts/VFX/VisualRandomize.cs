using UnityEngine;
using System.Collections;


public class VisualRandomize : MonoBehaviour
{

	// VFXManager가 SetActive 전에 세팅한 scale/rotation 위에 곱해서 흔드는 방식.
	// (F3DRandomize처럼 defaultScale 절대대입을 하면 VFXManager가 radius로 넣은 scale이 덮여 사라지므로 상대곱으로 함.
	//  풀 재사용해도 VFXManager가 매 스폰 scale/rotation을 먼저 리셋하니 누적/드리프트 없음.)

	public bool RandomScale, RandomRotation; // Randomize flags
	public float MinScale, MaxScale; // Min/Max scale range
	public float MinRotation, MaxRotaion; // Min/Max rotation range

	// Randomize scale and rotation according to the values set in the inspector
	void OnEnable()
	{
		if (RandomScale)
			transform.localScale *= Random.Range(MinScale, MaxScale);   // 현재 scale(=VFXManager가 넣은 radius)에 곱해 흔듦

		if (RandomRotation)
			transform.rotation *= Quaternion.Euler(0, 0, Random.Range(MinRotation, MaxRotaion));
	}
}
