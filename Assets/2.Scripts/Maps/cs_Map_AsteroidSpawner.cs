using UnityEngine;

public class cs_Map_AsteroidSpawner : MonoBehaviour
{

    [Header("소행성 설정")]
    public int asteroidCount = 50;
    public float spawnRadius = 500f;
    public float minSize = 10f;
    public float maxSize = 20f;

    [Header("이동 설정")]
    public float minSpeed = 0.1f;
    public float maxSpeed = 0.5f;

    [Header("메시 설정")]
    public Mesh[] asteroidMeshes;

    [Header("머티리얼")]
    public Material asteroidMaterial;

    [Header("스폰 포인트")]
    public Transform[] spawnPoints;

    void Awake()
    {
        /// 메쉬 데이터 자동 넣기
        asteroidMeshes = new Mesh[]
        {
        Resources.Load<Mesh>("Meshes/Asteroid-001-MediumPoly"),
        Resources.Load<Mesh>("Meshes/Asteroid-002-MediumPoly"),
        Resources.Load<Mesh>("Meshes/Asteroid-003-MediumPoly")
        };

        /// 스폰 포인트 자동 넣기
        
        Transform[] all = GetComponentsInChildren<Transform>();
        spawnPoints = new Transform[all.Length - 1];
        for (int i = 0; i < spawnPoints.Length; i++)
            spawnPoints[i] = all[i + 1];

        /// 머티리얼 자동 넣기
        asteroidMaterial = Resources.Load<Material>("Materials/Mat_AsteroidMaterial");
    }

    private void Start()
    {
        for (int i = 0; i < asteroidCount; i++)
        {
            SpawnAsteroid();
        }
    }

    void SpawnAsteroid()
    {
        // 스폰 포인트 중 랜덤 선택
        Vector3 center = transform.position;
        if (spawnPoints != null && spawnPoints.Length > 0)
            center = spawnPoints[Random.Range(0, spawnPoints.Length)].position;

        Vector3 pos = center + Random.insideUnitSphere * spawnRadius;

        /// 소행성 생성
        GameObject asteroid = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        asteroid.transform.position = pos;


        // 랜덤 크기
        float size = Random.Range(minSize, maxSize);
        asteroid.transform.localScale = Vector3.one * size;


        // 랜덤 회전
        asteroid.transform.rotation = Random.rotation;

        /// 부모 설정
        asteroid.transform.parent = this.transform;

        // 메시 붙이기
        MeshFilter mf = asteroid.GetComponent<MeshFilter>();
        MeshRenderer mr = asteroid.GetComponent<MeshRenderer>();

        if (asteroidMeshes != null && asteroidMeshes.Length > 0)
            {
                mf.mesh = asteroidMeshes[Random.Range(0, asteroidMeshes.Length)];
            }

        if (asteroidMaterial != null)
            {
                mr.material = asteroidMaterial;
            }

        // 콜라이더
        asteroid.AddComponent<MeshCollider>().sharedMesh = mf.mesh;

        // 스크립트 연결
        asteroid.AddComponent<cs_Map_Asteroid>();
    }
}
