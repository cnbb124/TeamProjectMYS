using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Cluster Missile Data", menuName = "Create Data/Item/Projectile Data/Cluster Missile")]
public class ClusterMisslleData : MissileData
{
	[Header("분열 탄 갯수(락온 시스템에도 자동 등록됨. 다중 타겟될 갯수)")]
	public int splitCount = 4;

	[Header("분열이 시작될 비행거리")]
	public float splitDistance = 750f;

	[Header("분열 각도(도단위, 가운데 기준 좌우대칭")]
	[Range(10f, 180f)]
	public float splitSpreadAngle = 120f;

}

