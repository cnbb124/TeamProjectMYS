using UnityEngine;

public class TestMissileShooter : MonoBehaviour
{
    [SerializeField] private Unit attacker;
    [SerializeField] private Transform target;
    [SerializeField] private float fireInterval = 2f;
    
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
                m.Init(transform.position, dir, attacker, target);
        }
    }
}