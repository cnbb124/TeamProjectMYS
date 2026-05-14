using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//이 스크립트를 파티클 이펙트 프리팹에 부착하면, 씬 뷰에서 프리팹을 클릭할 때마다 설정한 반경이 원형으로 나타납니다.

//화면을 보며 수치를 직관적으로 조절한 뒤, 확정된 수치를 SoundManager에 기입하여 동기화합니다.

//혹은 해당 이펙트가 스폰되어 SoundManager를 호출할 때, minDistance와 maxDistance 값을 매개변수로 함께 넘겨주어 유동적으로 범위를 적용하게 할 수도 있습니다.
public class SoundRangeVisualizerGizmos : MonoBehaviour
{
	[Header("<size=15>이펙트 발생 시 출력될 사운드 범위 시각화</size>")]
	[Header("최소거리 하늘색")]
	public float minDistance = 1.0f;
	[Header("최대거리 파란색")]
	public float maxDistance = 50.0f;

	// 에디터의 씬 뷰에서만 실행되는 기즈모 그리기 함수
	private void OnDrawGizmosSelected()
	{
		// Min Distance 시각화 (하늘색 원)
		Gizmos.color = Color.cyan;
		Gizmos.DrawWireSphere(transform.position, minDistance);

		// Max Distance 시각화 (파란색 원)
		Gizmos.color = Color.blue;
		Gizmos.DrawWireSphere(transform.position, maxDistance);
	}
}
