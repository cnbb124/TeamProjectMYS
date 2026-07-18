using Photon.Pun;
using UnityEngine;

public class MidBoss : Enemy
{
    [Header("�ڿ� ������")]
    public float currentResource = 1f;  /// ���۽� �ٷ� �ϲ� ����

    [Header("�ϲ� ���� ����")]
    public GameObject workerPrefab;
    public float resourceCostPerWorker = 1f; // �ϲ� 1������ �ڿ� �Ҹ�
    public float spawnInterval = 60f;         // ���� ���� (��)
    public int maxWorkers = 10;               // �ִ� �ϲ� ��

    private float spawnTimer = 0f;

    protected override void Start()
    {
        base.Start();
        TrySpawnWorker();
    }

    void Update()
    {
        if (!IsMine) return; // 워커 스폰은 Master 권위 (남 클라는 스폰 안 함, 네트워크로 받음)
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
        Debug.Log($"���� �ڿ� ����: +{amount} / �� ����: {currentResource}");
    }

    void TrySpawnWorker()
    {
        if (!IsMine) return; // 워커 스폰은 Master 권위 (Start/Update 양쪽 진입 모두 커버)
        // ���� �ϲ� �� üũ
        EnemyWorker[] workers = FindObjectsOfType<EnemyWorker>();
        if (workers.Length >= maxWorkers) return;

        // �ڿ� �Ҹ�
        if (currentResource < resourceCostPerWorker) return;
        currentResource -= resourceCostPerWorker;

        // �ϲ� ����
        Vector3 spawnPos = transform.position + Random.insideUnitSphere * 30f;
        // 네트워크 스폰 — Master가 만들면 전원에게 동기화. workerPrefab은 Resources 폴더 + PhotonView 필요.
        // 일꾼도 '룸 종속' — RoomObject로 만들어야 방장이 나가도 안 사라짐.
        // 일꾼도 스폰 씬을 같이 보냄 — 다른 씬 클라에서 숨기기 위함(EnemySceneVisibility).
        PhotonNetwork.InstantiateRoomObject(workerPrefab.name, spawnPos, Quaternion.identity, 0,
            new object[] { UnityEngine.SceneManagement.SceneManager.GetActiveScene().name });
        Debug.Log($"�ϲ� ����! ���� �ڿ� �ܷ�: {currentResource}");
    }
}