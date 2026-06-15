using UnityEngine;

public class EnemyWorker : Enemy
{
    public enum State { Moving, Mining, Alert, Attack }

    [Header("채굴 설정")]
    public float miningDamage = 100f;
    [Header("자원 설정")]
    public float maxResource = 100f;      // 최대 자원량
    public float currentResource = 0f;   // 현재 자원량
    public float resourcePerDamage = 1f; // 데미지 1당 자원 획득량
    public GameObject dropPrefab;        // 파괴시 드랍 프리팹

    [Header("AI상태 (디버그용)")]
    public State currentState = State.Moving;

    private Transform targetAsteroid;
    private Transform player;

    // EnemyWorker는 Enemy의 범용 전투/순찰 AI를 쓰지 않고 자체 채굴 AI를 사용
    protected override bool UseGenericAI => false;

    protected override void Start()
    {
        base.Start();

        player = GameObject.FindWithTag("Player").transform;
        FindNearestAsteroid();
    }

    // 피격 데미지만큼 자원 누적. 데미지 계산/사망판정은 Unit.TakeDamage()가 그대로 처리.
    public override void TakeDamage(DamageInfo info)
    {
        base.TakeDamage(info);
        //이 로직은 종찬님이 보시고 더 수정
        currentResource = Mathf.Min(maxResource, currentResource + info.damageAmount * resourcePerDamage);
    }

    // 사망 시 자원 드랍 + 킬카운트 누적
    protected override void Die()
    {
        if (dropPrefab != null)
        {
            GameObject drop = Instantiate(dropPrefab, transform.position, Quaternion.identity);
            ItemPickup pickup = drop.GetComponent<ItemPickup>();
            if (pickup != null)
            {
                pickup.Init(Mathf.RoundToInt(currentResource));
            }
        }

        GameManager.Instance.OnEnemyKilled();
        Destroy(gameObject);
    }

    protected override void Update()
    {
        base.Update();

        if (ShouldPause || CurState == UNIT_STATE.DIE)
        {
            return;
        }

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
            baseMoveSpeed * Time.deltaTime
        );

        // 이동 방향 바라보기
        Vector3 dir = (targetAsteroid.position - transform.position).normalized;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir), rotateSpeed * Time.deltaTime);

        // 소행성 반지름 기준으로 도착 판정
        float asteroidRadius = targetAsteroid.localScale.x * 0.5f;
        float arrivalDist = asteroidRadius + 20f; // 소행성과 도착 거리

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

        // 소행성 파괴됐으면 새 소행성 찾기
        if (targetAsteroid == null)
        {
            FindNearestAsteroid();
            currentState = State.Moving;
            return;
        }

        targetAsteroid.GetComponent<cs_Map_Asteroid>()?.TakeDamage(miningDamage * Time.deltaTime);

        // TODO: 채굴 애니메이션 or 이펙트
    }

    void UpdateAlert()
    {
        if (!IsPlayerInRange())
        {
            // 플레이어 감지 안되면 다시 채굴
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
