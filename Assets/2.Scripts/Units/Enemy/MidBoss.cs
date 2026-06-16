using UnityEngine;

public class MidBoss : Enemy
{
    [Header("자원 보유량")]
    public float currentResource = 1f;  /// 시작시 바로 일꾼 생산

    [Header("일꾼 생산 설정")]
    public GameObject workerPrefab;
    public float resourceCostPerWorker = 1f; // 일꾼 1마리당 자원 소모량
    public float spawnInterval = 60f;         // 생산 간격 (초)
    public int maxWorkers = 10;               // 최대 일꾼 수

    private float spawnTimer = 0f;

    protected override void Start()
    {
        base.Start();
        TrySpawnWorker();
    }

    void Update()
    {
        if (currentResource <= 0) return;

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            TrySpawnWorker();
            spawnTimer = 0f;
        }
    }
    public void ReceiveResource(float amount)
    {
        currentResource += amount;
        Debug.Log($"보스 자원 수신: +{amount} / 총 보유: {currentResource}");
    }

    void TrySpawnWorker()
    {
        // 현재 일꾼 수 체크
        EnemyWorker[] workers = FindObjectsOfType<EnemyWorker>();
        if (workers.Length >= maxWorkers) return;

        // 자원 소모
        if (currentResource < resourceCostPerWorker) return;
        currentResource -= resourceCostPerWorker;

        // 일꾼 스폰
        Vector3 spawnPos = transform.position + Random.insideUnitSphere * 30f;
        Instantiate(workerPrefab, spawnPos, Quaternion.identity);
        Debug.Log($"일꾼 생산! 보스 자원 잔량: {currentResource}");
    }
}