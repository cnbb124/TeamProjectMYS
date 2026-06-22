/*
 * [SunObject]
 * 카메라와 무관하게 항상 같은 방향·같은 크기로 보이는 태양 오브젝트.
 *
 * [사용법]
 * 1. 빈 오브젝트(또는 Quad/Sprite)에 부착
 * 2. sunDirection : 태양이 위치할 월드 방향 (예: 비스듬한 위쪽)
 * 3. distance     : 카메라로부터 떨어뜨릴 거리 (far clip보다 약간 안쪽)
 * 4. 태양 메시는 Billboard로 항상 카메라를 향하게 처리
 */

using UnityEngine;

[ExecuteAlways]
public class SunObject : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Header("태양 위치")]
    [SerializeField] private Vector3 sunDirection = new Vector3(0.3f, 0.5f, 1f); // 태양 방향
    [SerializeField] private float   distance     = 5000f;  // 카메라로부터 거리

    [Header("이글거림 (스케일 펄스)")]
    [SerializeField] private float baseScale   = 800f;
    [SerializeField] private float pulseAmount = 30f;   // 크기 진동 폭
    [SerializeField] private float pulseSpeed  = 2f;    // 진동 속도

    private void LateUpdate()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        // 카메라 기준 항상 같은 방향·거리에 배치 → 거리 무관 동일 크기
        Vector3 dir = sunDirection.normalized;
        transform.position = targetCamera.transform.position + dir * distance;

        // 빌보드 — 항상 카메라를 정면으로
        transform.rotation = Quaternion.LookRotation(
            transform.position - targetCamera.transform.position);

        // 이글이글 — 미세한 크기 진동
        float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount
                    + Mathf.PerlinNoise(Time.time * pulseSpeed * 1.7f, 0f) * pulseAmount;
        float s = baseScale + pulse;
        transform.localScale = new Vector3(s, s, s);
    }
}