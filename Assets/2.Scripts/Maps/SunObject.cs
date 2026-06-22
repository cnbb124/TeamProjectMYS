/*
 * [SunObject]
 * 월드의 한 지점에 고정된 "진짜 태양" 오브젝트.
 * 카메라(비행기)가 다가가면 크게, 멀어지면 작게 보임 (일반 3D 오브젝트처럼).
 *
 * [사용법]
 * 1. Sphere 메시 오브젝트에 부착 (태양 본체)
 * 2. 위치는 씬에서 한 번만 배치 — 이 스크립트는 위치를 건드리지 않음
 * 3. SunSurface 셰이더 머티리얼을 적용 (표면 흐름 + 차등 자전)
 * 4. spinSpeed로 본체 메시를 아주 천천히 자전
 *
 * [중요 — Far Clip Plane]
 * 태양이 카메라 Far Clip(기본 1000) "안쪽"에 있어야 잘리지 않음.
 * - 태양을 1000 유닛 안에 두거나
 * - Main Camera의 Far Clip을 태양 거리보다 크게 키울 것
 * (Far를 너무 키우면 Near와의 비율 때문에 z-fighting 발생 주의)
 */

using UnityEngine;

public class SunObject : MonoBehaviour
{
    [Header("자전")]
    [SerializeField] private Vector3 spinAxis  = Vector3.up; // 자전 축
    [SerializeField] private float   spinSpeed = 0.2f;       // 자전 속도(도/초) — 정말 느리게

    private void Update()
    {
        // 자전 — 본체 메시를 로컬 회전 (위치는 고정, 건드리지 않음)
        if (spinSpeed != 0f)
            transform.Rotate(spinAxis.normalized, spinSpeed * Time.deltaTime, Space.Self);
    }
}
