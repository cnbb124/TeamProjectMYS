using UnityEngine;

public class cs_Map_Asteroid : MonoBehaviour
{

    [Header("소행성 설정")]
    public float hp = 1000;
    public int dropCount = 3;

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

    public void TakeDamage(float dmg)
    {
        hp -= dmg;
        if (hp <= 0) Break();
    }

    void Break()
    {
        Debug.Log("소행성 파괴!");
        for (int i = 0; i < dropCount; i++)
        {
            GameObject item = PoolManager.Instance.Get(POOL_TYPE.ITEM_ASTEROID);
            Debug.Log($"아이템 가져옴: {item}");

            if (item == null) break;
            item.transform.position = transform.position + Random.insideUnitSphere * 50f;
            Debug.Log($"아이템 위치: {item.transform.position}");
        }
        Destroy(gameObject);
    }
}
