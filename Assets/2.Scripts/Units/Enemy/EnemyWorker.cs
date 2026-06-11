using UnityEngine;

public class EnemyWorker : MonoBehaviour
{
    public enum State { Moving, Mining, Alert, Attack }

    [Header("기본설정")]
    public float moveSpeed = 5f;
    [Header("채취 설정")]
    public float miningDamage = 100f;
    [Header("자원 설정")]
    public float maxResource = 100f;      // 최대 보유량
    public float currentResource = 0f;   // 현재 보유량
    public float resourcePerDamage = 1f; // 데미지 1당 자원 획득량
    public GameObject dropPrefab;        // 격추시 드랍 프리팹

    [Header("플레이어 감지")]
    public float detectRange = 100f;    // 플레이어 감지 범위
    public float rotateSpeed = 3f;

    [Header("상태 (디버그용)")]
    public State currentState = State.Moving;

    private Transform targetAsteroid;
    private Transform player;

    private void Start()
    {
        player = GameObject.FindWithTag("Player").transform;
        FindNearestAsteroid();
    }

    private void Update()
    {
        switch (currentState)
        {
            case State.Moving: UpdateMoving(); break;
            case State.Mining: UpdateMining(); break;
            case State.Alert: UpdateAlert(); break;
        }
    }

    void UpdateMoving()
    {
        // 플레이어 감지 (먼저 체크)
        if (IsPlayerInRange())
        {
            currentState = State.Alert;
            return;
        }

        if (targetAsteroid == null)
        {
            FindNearestAsteroid();
            return;
        }

        // 소행성으로 이동
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetAsteroid.position,
            moveSpeed * Time.deltaTime
        );

        // 이동 방향 바라보기
        Vector3 dir = (targetAsteroid.position - transform.position).normalized;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir), rotateSpeed * Time.deltaTime);
        
        // 소행성 반지름 기준으로 도착 판정
        float asteroidRadius = targetAsteroid.localScale.x * 0.5f;
        float arrivalDist = asteroidRadius + 20f; // 소행성과 여유 거리

        float dist = Vector3.Distance(transform.position, targetAsteroid.position);
        if (dist <= arrivalDist)
            currentState = State.Mining;

        
    }

    void UpdateMining()
    {
        // 플레이어 감지
        if (IsPlayerInRange())
        {
            currentState = State.Alert;
            return;
        }

        // 소행성 파괴됐으면 다음 소행성 찾기
        if (targetAsteroid == null)
        {
            FindNearestAsteroid();
            currentState = State.Moving;
            return;
        }

        targetAsteroid.GetComponent<cs_Map_Asteroid>()?.TakeDamage(miningDamage * Time.deltaTime);

        // TODO: 채취 애니메이션 or 이펙트
    }

    void UpdateAlert()
    {
        if (!IsPlayerInRange())
        {
            // 플레이어 범위 벗어나면 다시 채취
            FindNearestAsteroid();
            currentState = State.Moving;
            return;
        }

        // 플레이어 바라보기
        Vector3 dir = (player.position - transform.position).normalized;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir), rotateSpeed * Time.deltaTime);

        // TODO: 공격 로직
    }

    void FindNearestAsteroid()
    {
        GameObject[] asteroids = GameObject.FindGameObjectsWithTag("Asteroid");
        float minDist = float.MaxValue;
        targetAsteroid = null;

        foreach (var a in asteroids)
        {
            float dist = Vector3.Distance(transform.position, a.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                targetAsteroid = a.transform; // 루트 오브젝트
            }
        }
    }

    bool IsPlayerInRange()
    {
        if (player == null) return false;
        return Vector3.Distance(transform.position, player.position) <= detectRange;
    }
}
