using UnityEngine;

public class cs_Map_Asteroid : MonoBehaviour
{
    private Vector3 moveDirection;
    private float moveSpeed;
    private Vector3 rotationAxis;
    private float rotationSpeed;

    private void Start()
    {
        //// 랜점하게 이동 및 방향
        moveDirection = Random.onUnitSphere;
        moveSpeed = Random.Range(0.1f, 0.5f);


        //// 랜덤하게 자전
        rotationAxis = Random.onUnitSphere;
        rotationSpeed = Random.Range(1f, 10f);
    }

    private void Update()
    {
        /// 이동
        transform.position += moveDirection * moveSpeed * Time.deltaTime;

        /// 자전
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
    }
}
