/*
 * [HangarOrbitCamera]
 * 격납고 기체 뷰어용 궤도 카메라. 마우스 드래그로 타겟(기체) 주위를 360도 회전 + 휠 줌.
 * RenderTexture 전용 카메라에 부착해서 PlaneViewerPanel 안에서 기체를 돌려볼 때 사용.
 *
 * [부착] 기체를 찍는 전용 카메라(Target Texture 지정된 것)에 부착.
 *
 * [인스펙터 연결]
 * - target        : 회전 중심 (기체 Transform)
 * - distance      : 기체와의 거리 (기체가 패널에 꽉 차게 조절)
 * - rotateSpeed   : 드래그 회전 감도
 * - pitchMin/Max  : 상하 회전 제한 (기체 밑바닥 뒤집혀 보이는 것 방지)
 * - zoomSpeed / distanceMin/Max : 휠 줌 설정 (zoomSpeed 0이면 줌 비활성)
 *
 * [조작]
 * 마우스 좌클릭 드래그 = 회전 / 휠 = 줌
 * ※ 격납고 UI가 열려있는 동안에만 카메라가 활성화되도록 오브젝트 켜고 끄면 됨.
 */

using UnityEngine;

public class HangarOrbitCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;       // 회전 중심 (기체)

    [Header("거리 / 줌")]
    [SerializeField] private float distance    = 10f;
    [SerializeField] private float zoomSpeed   = 5f;  // 0이면 줌 끔
    [SerializeField] private float distanceMin = 4f;
    [SerializeField] private float distanceMax = 25f;

    [Header("회전")]
    [SerializeField] private float rotateSpeed = 200f;
    [SerializeField] private float pitchMin    = -20f; // 아래로 내려다보는 각 제한
    [SerializeField] private float pitchMax    = 60f;  // 위로 올려다보는 각 제한

    private float _yaw;    // 좌우 각도
    private float _pitch;  // 상하 각도

    private void Start()
    {
        // 시작 시 현재 카메라 방향을 기준 각도로 사용
        Vector3 angles = transform.eulerAngles;
        _yaw   = angles.y;
        _pitch = angles.x;
        UpdatePosition();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // 좌클릭 드래그 = 회전
        if (Input.GetMouseButton(0))
        {
            _yaw   += Input.GetAxis("Mouse X") * rotateSpeed * Time.deltaTime;
            _pitch -= Input.GetAxis("Mouse Y") * rotateSpeed * Time.deltaTime;
            _pitch  = Mathf.Clamp(_pitch, pitchMin, pitchMax);
        }

        // 휠 = 줌
        if (zoomSpeed > 0f)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                distance = Mathf.Clamp(distance - scroll * zoomSpeed, distanceMin, distanceMax);
            }
        }

        UpdatePosition();
    }

    // 각도/거리 기준으로 카메라 위치·시선 갱신
    private void UpdatePosition()
    {
        if (target == null) return;

        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        transform.position = target.position + rot * new Vector3(0f, 0f, -distance);
        transform.LookAt(target.position);
    }
}
