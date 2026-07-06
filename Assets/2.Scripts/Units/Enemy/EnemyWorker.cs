using UnityEngine;

public class EnemyWorker : Enemy
{
    public enum State { Moving, Mining, Returning, Collecting, Alert, Attack }

    [Header("채굴 설정")]
    public float miningDamage = 100f;
    [Header("자원 설정")]
    public float maxResource = 100f;
    public float currentResource = 0f;
    public float resourcePerDamage = 1f;
    public GameObject dropPrefab;

    [Header("AI상태 (디버그용)")]
    public State currentState = State.Moving;

    private Transform targetAsteroid;
    private Transform targetItem;
    private Transform player;
    private MidBoss midBoss;

    protected override bool UseGenericAI => false;

    protected override void Start()
    {
        base.Start();
        player = UnitManager.Instance?.GetNearestPlayer(transform.position);
        midBoss = FindObjectOfType<MidBoss>();
        FindNearestAsteroid();
    }

    public override void TakeDamage(HitInfo info)
    {
        base.TakeDamage(info);
        currentResource = Mathf.Min(maxResource, currentResource + info.damageAmount * resourcePerDamage);
    }

    protected override void Die()
    {
        if (dropPrefab != null)
        {
            GameObject drop = Instantiate(dropPrefab, transform.position, Quaternion.identity);
            ItemPickup pickup = drop.GetComponent<ItemPickup>();
            if (pickup != null)
                pickup.Init(Mathf.RoundToInt(currentResource));
        }
        // 킬카운트 + 사망 애니 후 풀 반납은 base(Enemy.Die)가 처리 — 다른 적과 동일하게 반납.
        base.Die();
    }

    protected override void Update()
    {
        base.Update();
        if (ShouldPause || CurState == UNIT_STATE.DIE) return;

        switch (currentState)
        {
            case State.Moving: UpdateMoving(); break;
            case State.Mining: UpdateMining(); break;
            case State.Collecting: UpdateCollecting(); break;
            case State.Returning: UpdateReturning(); break;
            case State.Alert: UpdateAlert(); break;
        }
    }

    void UpdateMoving()
    {
        if (IsPlayerInRange()) 
        { 
            currentState = State.Alert; 
            return; 
        }

        // 소행성보다 아이템 우선
        GameObject nearestItem = FindNearestItem();
        if (nearestItem != null)
        {
            targetItem = nearestItem.transform;
            currentState = State.Collecting;
            return;
        }

        if (targetAsteroid == null)
        {
            FindNearestAsteroid();
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, targetAsteroid.position, baseMoveSpeed * Time.deltaTime);

        Vector3 dir = (targetAsteroid.position - transform.position).normalized;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotateSpeed * Time.deltaTime);

        float asteroidRadius = targetAsteroid.localScale.x * 0.5f;
        float dist = Vector3.Distance(transform.position, targetAsteroid.position);
        if (dist <= asteroidRadius + 20f) currentState = State.Mining;
        if (currentResource >= maxResource) currentState = State.Returning;
    }

    void UpdateMining()
    {
        if (IsPlayerInRange()) { currentState = State.Alert; return; }

        if (targetAsteroid == null)
        {
            GameObject nearestItem = FindNearestItem();
            if (nearestItem != null) { targetItem = nearestItem.transform; currentState = State.Collecting; }
            else { FindNearestAsteroid(); currentState = State.Moving; }
            return;
        }

        targetAsteroid.GetComponent<cs_Map_Asteroid>()?.TakeDamage(miningDamage * Time.deltaTime);
    }

    void UpdateCollecting()
    {
        if (IsPlayerInRange()) { currentState = State.Alert; return; }

        if (targetItem == null || !targetItem.gameObject.activeInHierarchy)
        {
            FindNearestAsteroid();
            currentState = State.Moving;
            return;
        }

        Vector3 dir = (targetItem.position - transform.position).normalized;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotateSpeed * Time.deltaTime);

        transform.position = Vector3.MoveTowards(transform.position, targetItem.position, baseMoveSpeed * Time.deltaTime);

        float dist = Vector3.Distance(transform.position, targetItem.position);
        if (dist <= 10f)
        {
            currentResource = Mathf.Min(maxResource, currentResource + 1f);
            PoolManager.Instance.Return(targetItem.gameObject);
            targetItem = null;

            if (currentResource >= maxResource) { currentState = State.Returning; return; }

            GameObject nextItem = FindNearestItem();
            if (nextItem != null) targetItem = nextItem.transform;
            else { FindNearestAsteroid(); currentState = State.Moving; }
        }
    }

    void UpdateReturning()
    {
        if (midBoss == null) return;

        Vector3 dir = (midBoss.transform.position - transform.position).normalized;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotateSpeed * Time.deltaTime);

        transform.position = Vector3.MoveTowards(transform.position, midBoss.transform.position, baseMoveSpeed * Time.deltaTime);

        float dist = Vector3.Distance(transform.position, midBoss.transform.position);
        if (dist <= 30f)
        {
            midBoss.ReceiveResource(currentResource);
            currentResource = 0f;
            FindNearestAsteroid();
            currentState = State.Moving;
        }
    }

    void UpdateAlert()
    {
        if (!IsPlayerInRange()) { FindNearestAsteroid(); currentState = State.Moving; return; }

        Vector3 dir = (player.position - transform.position).normalized;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotateSpeed * Time.deltaTime);
    }

    void FindNearestAsteroid()
    {
        GameObject[] asteroids = GameObject.FindGameObjectsWithTag("Asteroid");
        float minDist = float.MaxValue;
        targetAsteroid = null;

        foreach (var a in asteroids)
        {
            float dist = Vector3.Distance(transform.position, a.transform.position);
            if (dist < minDist) { minDist = dist; targetAsteroid = a.transform; }
        }
    }

    GameObject FindNearestItem()
    {
        GameObject[] items = GameObject.FindGameObjectsWithTag("Item");
        float minDist = float.MaxValue;
        GameObject nearest = null;

        foreach (var item in items)
        {
            if (!item.activeInHierarchy) continue;
            float dist = Vector3.Distance(transform.position, item.transform.position);
            if (dist < minDist) { minDist = dist; nearest = item; }
        }
        return nearest;
    }

    bool IsPlayerInRange()
    {
        if (player == null) return false;
        return Vector3.Distance(transform.position, player.position) <= detectRange;
    }
}