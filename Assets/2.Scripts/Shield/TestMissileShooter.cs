using UnityEngine;

public class TestMissileShooter : MonoBehaviour
{
    [SerializeField] private Unit attacker;
    [SerializeField] private Transform target;
    [SerializeField] private float fireInterval = 2f;
    [Tooltip("직접 Init 호출용 — 주입할 MissileData(하드코딩). 없으면 미사일이 데이터 없이 발사됨.")]
    [SerializeField] private MissileData missileData;

    private float _lastFireTime = 0f;

    void Update()
    {
        if (Time.time >= _lastFireTime + fireInterval)
        {
            _lastFireTime = Time.time;
            
            // 타겟 방향으로 발사
            Vector3 dir = (target.position - transform.position).normalized;
            
            Missile m = PoolManager.Instance.GetProjectile(POOL_TYPE.PROJECTILE_MISSILE) as Missile;
            if (m != null)
            {
                m.missileData = missileData;   // [데이터 주입] 하드코딩한 MissileData 주입 후 발사
                m.Init(transform.position, dir, attacker, target);
            }
        }
    }
}