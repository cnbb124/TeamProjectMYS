using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplodeRadiusVisualizerGizmo : MonoBehaviour
{
	[Header("출력될 폭발이펙트 범위 시각화(빨강색)")]
	public float explosionRadius;
	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(transform.position, explosionRadius);
	}
}
