/*
 * [EnergyOrb]
 * 보스 탄막용 에너지 구체 (니어 오토마타식). Projectile 상속 — 직진 이동 + 접촉 데미지.
 * 데미지 적용은 팀 공통 경로(ApplyDamage → Unit.TakeDamage)를 그대로 사용하므로
 * "쉴드 먼저 깎이고 HP로 넘어가는" 분기는 Unit 쪽에서 자동 처리됨.
 *
 * [프리팹 세팅]
 * 1. 에너지 구체 프리팹 루트에 이 스크립트 부착
 * 2. Collider(Sphere 권장) + isTrigger 체크, Rigidbody(Kinematic) 필요 시 추가
 * 3. 인스펙터에서 speed / baseDamage / maxRange 조절
 *    (또는 orbData(BulletData SO)를 연결하면 SO 값 우선 적용 — 탄 종류별 에셋 관리 가능)
 *
 * [발사 측 호출]
 *   EnergyOrb orb = ...(풀 또는 Instantiate);
 *   orb.Init(firePos, dir, bossUnit);   // TestBoss.FirePoint와 동일한 방식
 *
 * [특징]
 * - 니어식 탄막 특성: 총알보다 느리고 큼직 (기본 speed 30)
 * - 회전 연출: spinSpeed로 구체가 빙글빙글 (0이면 끔)
 * - 사거리(maxRange) 도달 시 자동 풀 반납 (Projectile 기본 동작)
 */

using UnityEngine;

public class EnergyOrb : Projectile
{
    [Space(5)]
    [Header("<size=18>[에너지 구체 설정]</size>")]

    [Tooltip("이동 속도. 니어식 탄막은 느릿하게 (총알보다 낮게)")]
    [SerializeField] private float speed = 30f;

    [Tooltip("구체 회전 연출 속도(도/초). 0이면 회전 없음")]
    [SerializeField] private float spinSpeed = 90f;

    [Header("투사체 데이터(SO) — 연결 시 인스펙터 값 대신 SO 값 사용")]
    public BulletData orbData;

    protected override void Awake()
    {
        base.Awake();
        dmgType        = DAMAGE_TYPE.BULLET;      // 쉴드/HP 분기는 Unit.TakeDamage가 처리
        projectileType = PROJECTILE_TYPE.BULLET;  // 풀 반납 식별용
    }

    public override void Init(Vector3 startPos, Vector3 dir, Unit attacker)
    {
        base.Init(startPos, dir, attacker);

        // SO 연결 시 데이터 우선 (탄 종류별 에셋 관리)
        if (orbData != null)
        {
            speed                  = orbData.speed;
            baseDamage             = orbData.damage;
            maxRange               = orbData.maxRange;
            hitSoundType           = orbData.hitSoundType;
            hitVfxType             = orbData.hitEffectType;
            shieldHitVfxType       = orbData.shieldHitEffectType;
            ignoreArmor            = orbData.ignoreArmor;
            shieldDamageMultiplier = orbData.shieldDamageMultiplier;
        }
    }

    protected override void Update()
    {
        // 일시정지/게임오버 중엔 정지 (재개 시 그 자리서 계속) — Bullet과 동일 규약
        if (GameManager.Instance != null && GameManager.Instance.IsGameplayFrozen) return;

        // 직진 이동
        transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.Self);

        // 구체 회전 연출 (자식 메시가 있으면 시각적으로만 돎 — 이동 방향엔 영향 없음)
        if (spinSpeed != 0f)
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);

        // 사거리 누적/판정 (Projectile 기본)
        base.Update();
    }

    protected override void OnHit(HitTarget hit)
    {
        base.OnHit(hit);

        // 피격 반응은 각 클라 로컬 재생 — 데미지는 소유자만(권위), 연출은 전원(Bullet과 동일 처리).
        // 환경은 항상 로컬, 유닛은 소유자 클라가 ApplyHitDamage → OnHitReaction으로 처리하므로
        // 비소유자(원격 유닛)에서만 여기서 로컬 재생함(이중 방지).
        Unit hitUnit = hit.damageable as Unit;
        if (hit.damageable == null || (hitUnit != null && !hitUnit.IsMine))
        {
            hit.hittable.OnHitReaction(BuildHitInfo(hit));
        }

        // 공통 데미지 적용 — 쉴드 흡수/HP 차감은 Unit.TakeDamage에서 자동 분기
        ApplyDamage(hit, this.curDamage, this.dmgType);

        ReturnToPool();
    }
}
