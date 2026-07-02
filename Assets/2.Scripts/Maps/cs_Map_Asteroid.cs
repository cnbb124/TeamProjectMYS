using UnityEngine;

public class cs_Map_Asteroid : MonoBehaviour, IDamageable
{
    public ResourceData resourceData;

    [Header("소행성 설정")]
    public float hp = 1000;
    public int CurHp => (int)hp;  /// 인터페이스용
    public int dropCount = 3;

    private Vector3 moveDirection;
    private float moveSpeed;
    private Vector3 rotationAxis;
    private float rotationSpeed;

    private bool isDead = false;

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

    public void TakeDamage(HitInfo info)
    {
        OnHitReaction(info);           // 피격 VFX
        TakeDamage(info.damageAmount);
    }

	// IHittable 구현 — 소행성 피격 VFX. 총알이 HitInfo에 실어준 hitVfxType 재생.
	// 소행성은 실드가 없으므로 일반 히트만(shieldHitVfxType 미사용).
	public void OnHitReaction(HitInfo info)
    {
        VFXManager.Instance.PlayEffectAtPosition(info.hitVfxType, info.hitPosition, Quaternion.identity);
    }


    public bool BlocksBeam => true;

    public void TakeDamage(float dmg)
    {
        if (isDead) return;
        hp -= dmg;
        if (hp <= 0)
        {
            isDead = true;
            Break();
        }
    }

    void Break()
    {
        Debug.Log("소행성 파괴!");
        for (int i = 0; i < dropCount; i++)
        {
            GameObject item = PoolManager.Instance.Get(POOL_TYPE.ITEM_ASTEROID);

            if (item == null) break;

            item.tag = "Item";
            item.transform.position = transform.position + Random.insideUnitSphere * 50f;

            ItemPickup pickup = item.GetComponent<ItemPickup>();

            if (pickup == null) { Debug.LogWarning("ItemPickup 없음: " + item.name); continue; }

            pickup.Init(resourceData, 1); // resourceData 필요
        }
        Destroy(gameObject);
    }
}
