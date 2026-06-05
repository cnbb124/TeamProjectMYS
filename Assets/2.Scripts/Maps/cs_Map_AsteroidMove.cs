using UnityEngine;

public class cs_Map_AsteroidMove : MonoBehaviour
{
    [Header("패트롤 설정")]
    public Transform[] waypoints;              // 이동할 웨이포인트 리스트
    public float speed = 0.3f;                // 이동 속도
    public float waypointArrivalThreshold = 5f; // 도달 판정 거리

    private int currentIndex = 0;

    void Update()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Transform target = waypoints[currentIndex];

        // 목표 방향으로 이동
        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            speed * Time.deltaTime
        );

        // 도달하면 다음 웨이포인트
        if (Vector3.Distance(transform.position, target.position) <= waypointArrivalThreshold)
        {
            currentIndex = (currentIndex + 1) % waypoints.Length;
        }
    }
}
