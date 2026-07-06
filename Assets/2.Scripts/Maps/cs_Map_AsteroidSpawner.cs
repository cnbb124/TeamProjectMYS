using System.Collections.Generic;
using UnityEngine;

public class cs_Map_AsteroidSpawner : MonoBehaviour
{
    public ResourceData resourceData;

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

    [Header("소행성이 그려질 거리")]
    public float maxVisibleDistance = 1000f; // 소행성이 그려질 최대 거리
    private Transform cameraTransform;      // 플레이어 카메라 위치 기준점
    private List<MeshRenderer> asteroidRenderers = new List<MeshRenderer>(); // 렌더러만 빠르게 검사할 리스트
    private float optimizeTimer = 0f;
    private float optimizeInterval = 0.1f;  // 매 프레임 연산 방지 (0.1초마다 검사)

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

        if (asteroidMaterial != null)
        {
            asteroidMaterial.enableInstancing = true;
        }

    }

    private void Start()
    {
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        for (int i = 0; i < asteroidCount; i++)
        {
            SpawnAsteroid();
        }
    }

    private void Update()
    {
        if (cameraTransform == null) return;

        
        optimizeTimer += Time.deltaTime;
        if (optimizeTimer >= optimizeInterval)
        {
            optimizeTimer = 0f;
            OptimizeAsteroidsDistance();
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
                mr.sharedMaterial = asteroidMaterial;


            SphereCollider col = asteroid.GetComponent<SphereCollider>();
            col.radius = 0.55f;
            cs_Map_Asteroid asteroidScript = asteroid.AddComponent<cs_Map_Asteroid>();
            asteroidScript.resourceData = resourceData;

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

            if (mr != null)
            {
                mr.enabled = true;
                asteroidRenderers.Add(mr);
            }
    }

    void OptimizeAsteroidsDistance()
    {
        Vector3 cameraPos = cameraTransform.position;

        for (int i = asteroidRenderers.Count - 1; i >= 0; i--)
        {
            // 혹시 게임 도중 파괴된 소행성이 있다면 리스트에서 제거 처리
            if (asteroidRenderers[i] == null)
            {
                asteroidRenderers.RemoveAt(i);
                continue;
            }

            // 제곱근 연산을 피하기 위해 sqrMagnitude를 사용해 연산 속도를 극대화합니다.
            float sqrDistance = (asteroidRenderers[i].transform.position - cameraPos).sqrMagnitude;
            float sqrMaxDistance = maxVisibleDistance * maxVisibleDistance;

            // 설정한 거리(700m)보다 멀어지면 렌더러를 꺼버려서 드로우콜에서 아예 제외시킵니다.
            asteroidRenderers[i].enabled = (sqrDistance <= sqrMaxDistance);
        }
    }
}
