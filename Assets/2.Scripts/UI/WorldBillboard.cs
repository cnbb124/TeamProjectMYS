using UnityEngine;

// 월드 표시물을 카메라 쪽으로 돌리고, 화면에 보이는 크기를 상·하한 안에서 제한함.
public class WorldBillboard : MonoBehaviour
{
    [Tooltip("마커 크기. 키우면 화면에서 크게 보임.")]
    [SerializeField] private float size = 0.05f;

    [Tooltip("마커가 최대로 커지는 거리. 이보다 가까워져도 더 커지지 않음. 0이면 안 씀.")]
    [SerializeField] private float maxSizeDistance = 50f;

    [Tooltip("마커가 최대로 작아지는 거리. 이보다 멀어져도 더 작아지지 않음. 0이면 안 씀.")]
    [SerializeField] private float minSizeDistance = 800f;

    [Tooltip("켜면 카메라 위치를 바라봄. 끄면 카메라 정면에 평면 정렬됨.")]
    [SerializeField] private bool faceCameraPosition = true;

    private static Camera _sharedCamera;
    private static int _sharedCameraFrame = -1;

    public void Configure(float newSize, float newMaxSizeDistance, float newMinSizeDistance)
    {
        size = newSize;
        maxSizeDistance = newMaxSizeDistance;
        minSizeDistance = newMinSizeDistance;
    }

    private void LateUpdate()
    {
        Camera cam = GetSharedCamera();
        if (cam == null)
        {
            return;
        }

        Transform camTr = cam.transform;

        if (faceCameraPosition)
        {
            Vector3 fromCamera = transform.position - camTr.position;
            if (fromCamera.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(fromCamera, camTr.up);
            }
        }
        else
        {
            transform.rotation = camTr.rotation;
        }

        float distance = Vector3.Distance(transform.position, camTr.position);
        float scale = size;

        if (maxSizeDistance > 0f && distance < maxSizeDistance)
        {
            scale = size * (distance / maxSizeDistance);
        }
        else if (minSizeDistance > 0f && distance > minSizeDistance)
        {
            scale = size * (distance / minSizeDistance);
        }

        transform.localScale = Vector3.one * scale;
    }

    private static Camera GetSharedCamera()
    {
        if (_sharedCameraFrame == Time.frameCount)
        {
            return _sharedCamera;
        }

        _sharedCameraFrame = Time.frameCount;
        if (_sharedCamera == null || !_sharedCamera.isActiveAndEnabled)
        {
            _sharedCamera = Camera.main;
        }

        return _sharedCamera;
    }
}
