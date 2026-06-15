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

    [Header("머티리얼(자동연결)")]
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
            Vector3 center = transform.position;
            if (spawnPoints != null && spawnPoints.Length > 0)
                center = spawnPoints[Random.Range(0, spawnPoints.Length)].position;
            Vector3 pos = center + Random.insideUnitSphere * spawnRadius;

            GameObject asteroid = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            asteroid.transform.position = pos;
            float size = Random.Range(minSize, maxSize);
            asteroid.transform.localScale = Vector3.one * size;
            asteroid.transform.rotation = Random.rotation;
            asteroid.transform.parent = this.transform;

            MeshFilter mf = asteroid.GetComponent<MeshFilter>();
            MeshRenderer mr = asteroid.GetComponent<MeshRenderer>();
            if (asteroidMeshes != null && asteroidMeshes.Length > 0)
                mf.mesh = asteroidMeshes[Random.Range(0, asteroidMeshes.Length)];
            if (asteroidMaterial != null)
                mr.material = asteroidMaterial;

            Destroy(asteroid.GetComponent<SphereCollider>());
            asteroid.AddComponent<MeshCollider>().sharedMesh = mf.mesh;
            cs_Map_Asteroid asteroidScript = asteroid.AddComponent<cs_Map_Asteroid>();

            asteroid.name = "Asteroid";
            asteroid.tag = "Asteroid";

            // HitBox 자식 오브젝트
            GameObject hitboxObj = new GameObject("HitBox");
            hitboxObj.transform.parent = asteroid.transform;
            hitboxObj.transform.localPosition = Vector3.zero;
            hitboxObj.transform.localScale = Vector3.one;
            hitboxObj.layer = LayerMask.NameToLayer("HitBox");
            SphereCollider sc = hitboxObj.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.5f;
            //hitboxObj.AddComponent<HitBox>();
        }
}
