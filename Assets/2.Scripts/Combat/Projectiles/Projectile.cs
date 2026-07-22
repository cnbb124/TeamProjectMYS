using UnityEngine;

// ================================================================
// [Projectile — 외부 참조 / 자식 구현 가이드]
// ================================================================
// 모든 투사체의 베이스 클래스.
// Unit처럼 공통 로직은 여기서, 세부 동작은 자식에서 override.
// PoolManager에서 꺼낼 때 Init()으로 초기화, 반납 시 OnDisable()로 처리.
//
// ================================================================
// [발사 ~ 소멸 흐름]
// ================================================================
// WeaponSystem.Shoot()
//   └── Init(startPos, dir, attacker)      발사 시 초기화 (자식 override 시 base.Init() 필수)
//         Update()                          매 프레임 이동거리 누적 → maxRange 도달 시 OnMaxRange()
//         OnTriggerEnter(Collider other)    HitBox 레이어 충돌 감지 → 대상 조회 1회 → HitTarget 만들어 OnHit() 호출
//           └── OnHit(HitTarget hit)       피격 처리 (자식에서 override)
//                 └── ApplyDamage(...)      HitInfo 생성 → target.TakeDamage()
//                       └── ReturnToPool() 투사체 풀 반납 (소멸은 항상 이걸로)
//
// 대상 조회는 OnTriggerEnter에서 IHittable로 딱 한 번만 함 — IDamageable이 IHittable을 상속하므로
// 유닛/환경이 한 번에 잡히고, 데미지 대상인지는 HitTarget 안에서 캐스트로 갈림.
//   IDamageable 있음 → 데미지 + (TakeDamage가 피격 반응까지 처리)
//   IHittable만 있음 → 환경 오브젝트. 데미지 없이 피격 반응(사운드/VFX)만
//
// ================================================================
// [자식 구현 시 override 포인트]
// ================================================================
// Init(startPos, dir, attacker)            초기화 추가 시 — base.Init() 반드시 첫 줄 호출
// OnHit(HitTarget hit)                    피격 시 동작 — ApplyDamage + 이펙트/사운드 + ReturnToPool
// OnMaxRange()                             사거리 초과 시 동작 — 기본은 ReturnToPool (폭발형은 Explode 추가)
// OnDisable()                              풀 반납 시 정리 — base.OnDisable() 호출
//
// ================================================================
// [ApplyDamage 2종류 오버로딩]
// ================================================================
// ApplyDamage(HitTarget, int, DAMAGE_TYPE)
//   기본 단일 피격. OnTriggerEnter가 만든 HitTarget를 그대로 전달(대상 재조회 없음).
//
// ApplyDamage(IDamageable, Collider, int, DAMAGE_TYPE, Vector3 explosionCenter, float aoeRadius)
//   범위피해 폭발 전용. 폭발 중심 좌표와 반경을 함께 전달해 파츠 범위 피격 처리.
//   OverlapSphere로 찾은 대상마다 부르므로 target을 직접 받음.
// ================================================================
public abstract class Projectile : MonoBehaviour
{

    [Header("<size=22>[투사체 공통 스탯 기본 설정]</size>")]


	//============자식 클래스 Data(SO)에서 자동입력==========
	[HideInInspector]
    public float maxRange;//최대사거리

	//데미지타입
	[HideInInspector]
	public DAMAGE_TYPE dmgType;

	//피격(실드 없을때) 사운드. Bullet은 BulletData에서 복사, Missile은 미사용이라 항상 SFX_NONE 고정(Missile.Awake 참고). SFX_NONE(미등록)이면 무음
	[HideInInspector]
	public SOUND_TYPE hitSoundType;
	// 실드 피격음은 여기 없음 — 탄종별로 안 나누고 Unit.GetPlaySoundTypeShield(HitInfo)에서 DAMAGE_TYPE 기준으로 일괄 처리.

	//피격 VFX. Bullet은 BulletData(hit/shieldHitEffectType)에서 복사. Missile은 미사용(Explode가 폭발VFX 처리)이라 기본값 유지.
	//기본값은 총알 기본 히트 — bulletData가 null이어도 환경 피격 시 폭발이 아닌 총알히트가 나오게.
	[HideInInspector]
	public EFFECT_TYPE hitVfxType = EFFECT_TYPE.VFX_BULLET_HIT;
	[HideInInspector]
	public EFFECT_TYPE shieldHitVfxType = EFFECT_TYPE.VFX_BULLET_HIT_SHIELD;

	//관통탄 여부, 실드 데미지 배율. Bullet은 BulletData, Missile은 MissileData에서 Init 시 복사됨.
	[HideInInspector]
	public bool ignoreArmor;
	[HideInInspector]
	public float shieldDamageMultiplier = 1f;

    //=======================================================


	//발사좌표(사거리계산용.)
	protected Vector3 _startPos;
    //이동거리 구하기위한 이전좌표
    protected Vector3 _prevPos;
    //실제 투사체가 이동한거리
    protected float _traveledDistance = 0f;


	//데미지수치
	//인스펙터에서 설정하는 투사체 고유의 기본 데미지
	[Header("투사체 기본 데미지(출력용)")]
	public int baseDamage;
	//스탯과 버프가 적용된 실제 적용 데미지 (변동 값)
	[Header("투사체 현재 실제 데미지(출력용)")]
	public int curDamage;
	//공격자
	[Header("공격한 유닛(참조, 확인용)")]
	public Unit attacker;

	// 멀티: 이 투사체가 데미지 판정 권위를 갖는지. 내가 직접 쏜 총알만 true.
	// RpcShoot로 복제된 '남의 발사 연출' 총알은 false → 맞아도 데미지를 주지 않는다(연출·소멸만).
	// 안 그러면 각 클라의 총알 사본이 모두 데미지를 넘겨 N배로 적용됨. 싱글/오프라인은 항상 true.
	protected bool _hasDamageAuthority = true;
	// WeaponSystem이 발사 직후 설정(로컬발사=true, 복제=false).
	public void SetDamageAuthority(bool hasAuthority) { _hasDamageAuthority = hasAuthority; }


    //풀매니저에서 식별할 투사체 타입
    [HideInInspector]
    public PROJECTILE_TYPE projectileType;

	// 크리티컬 여부. Init()에서 attacker의 criChance로 판정.
	// 투사체가 크리 판정 담당 → HitInfo.isCritical로 전달.
	protected bool _critical;

	// 크리 시 데미지 배율. Init()에서 '공격자'의 criDamageMultiplier를 복사 → HitInfo.critMultiplier로 전달.
	// (크리 판정도 공격자 criChance 기준이므로 배율도 공격자 값이어야 짝이 맞음. 맞는 쪽 값 쓰던 버그 수정)
	protected float _critMultiplier = 1f;


    protected virtual void Awake()
    {

    }
    // Start is called before the first frame update
    protected virtual void Start()
    {

    }

    // Update is called once per frame
    protected virtual void Update()
    {

        //사거리 벗어날시
        //출발지점과 현재이동한거리가>=최대사거리 도달혹은초과시
        _traveledDistance += (transform.position - _prevPos).magnitude;
        _prevPos = transform.position;

        if (_traveledDistance >= maxRange)
        {
            OnMaxRange();
        }


    }


	/// <summary>
	/// 활성화시 넣을 정보. 플레이어에서 호출
	/// 꺼낼 때 호출. 매 발사마다 재초기화.
	/// 자식에서 오버라이드 시 base.Init() 반드시 호출.
	/// </summary>
	/// <param name="startPos">출발좌표(firePos)</param>
	/// <param name="dir">향할 방향(보통 forward)</param>
	/// <param name="attacker">발사하는 유닛(공격자, 쏜사람)</param>
	public virtual void Init(Vector3 startPos, Vector3 dir, Unit attacker)
	{
		//출발한 좌표 저장
		_startPos = startPos;
		//공격자 저장
		this.attacker = attacker;
		// 풀 재사용 대비 기본값(권위 있음)으로 리셋 — 복제탄이면 WeaponSystem이 Init 후 false로 덮음.
		_hasDamageAuthority = true;
		_traveledDistance = 0f;
		_prevPos = startPos;
		//출발할좌표로 현재좌표 초기화
		transform.position = startPos;
		//향할 방향초기화
		transform.forward = dir;

		//투사체 데미지 최신화 (풀링오염방지)

		curDamage = baseDamage;//차후 로직 추가 필요.(배율증가있을시)

		_critical = Random.Range(0f, 100f) < attacker.criChance;//크리여부
		_critMultiplier = attacker.criDamageMultiplier;//크리 배율은 공격자 기준(맞는 쪽 값 쓰던 버그 수정)


		//물리처리를 위한 레이어 입력
		if (attacker.gameObject.layer == (int)LAYER_TYPE.Unit_Player)

		{
			gameObject.layer = (int)LAYER_TYPE.Projectile_Player;
		}
		else
		{
			gameObject.layer = (int)LAYER_TYPE.Projectile_Enemy;
		}
	}

	//최대사거리 도달시 호출. 기본은 풀반납. 자식에서 폭발등 추가동작 필요시 오버라이드.
	protected virtual void OnMaxRange()
    {
        ReturnToPool();
    }



    //같은팀인지 체크 (attacker와 피격 Unit 태그 비교)
    protected bool IsSameTeam(Unit targetUnit)
    {
        if (attacker == null || targetUnit == null) return false;
        return attacker.CompareTag(targetUnit.tag);
    }







    protected virtual void OnTriggerEnter(Collider other)
    {

        //Debug.Log($"OnTrigger발생, 레이어: {LayerMask.LayerToName(other.gameObject.layer)}");

        //다른 시야감지용 트리거와 충돌방지.차후 수정필요할수도.

        ////발사자 본인과의 즉각 충돌 방지, 맞은게 히트박스면
        //if (other.transform.root != attacker.transform.root && other.gameObject.layer == (int)LAYER_TYPE.Trigger_HitBox)
        //{
        //    //온힛발생
        //    OnHit(other);
        //}
        //else//히트박스외엔 싹무시
        //{
        //    return;
        //}

        //맞은게 히트박스가 아니면 죄다 무시
        if (other.gameObject.layer != (int)LAYER_TYPE.HitBox)
        {
            return;
        }

        // 대상 조회는 여기서 한 번만. IDamageable이 IHittable을 상속하므로 이 한 번으로 유닛/환경이 다 잡힘.
        // 데미지 대상인지는 HitTarget가 캐스트로 판별함(추가 조회 없음).
        IHittable hittable = other.GetComponentInParent<IHittable>();
        if (hittable == null)
        {
            return;
        }

        // 자기충돌 제외를 '루트(transform.root) 기준'으로 함 — 스테이션+터렛처럼 한 구조물에
        // 여러 유닛이 붙어 있을 때, 그중 하나가 쏜 탄이 같은 구조물의 다른 유닛(부모 스테이션/형제 터렛)
        // 히트박스에 총구에서 즉시 맞아 소멸하던 문제 방지. 계층은 물리 무시를 안 해주므로 코드로 처리.
        // 같은 루트 소속이면 관통, 다른 구조물(적/플레이어/환경)이면 명중.
        // attacker는 풀 투사체(DDOL)가 쏜 유닛보다 오래 살아 이미 파괴됐을 수 있음(null 가능).
        Transform attackerRoot = attacker != null ? attacker.transform.root : null;
        Transform hitRoot = (hittable as MonoBehaviour)?.transform.root;

        //같은 구조물(루트) 소속이 아닐 때만 OnHit 발생
        if (hitRoot != attackerRoot)
        {
            OnHit(new HitTarget(other, hittable, other.ClosestPoint(transform.position)));
        }


    }

    //온트리거 재정의할 함수들
    protected virtual void OnHit(HitTarget hit)
    {
        //디버그용
  //      IDamageable target = other.GetComponentInParent<IDamageable>();
  //      Unit unit = target as Unit;
  //      string targetName = (target as MonoBehaviour)?.gameObject.name ?? other.gameObject.name;
		//string hp = unit != null ? unit.curHpRemaining.ToString() : "N/A";
		//string shield = unit != null ? unit.curShieldRemaining.ToString() : "N/A";
		//Debug.Log($"[OnHit] 피격 대상: {targetName} | 레이어: {LayerMask.LayerToName(other.gameObject.layer)} | HP: {hp} | SHIELD : {shield}");
    }



    /// <summary>
    /// 데미지 허용 메서드(온힛에서호출)
    ///  </summary>
    /// <param name="hit"> OnTriggerEnter가 만든 피격 대상 정보</param>
    /// <param name="damage"> 계산된 최종 데미지</param>
    /// <param name="currentDmgType"> 데미지 타입정보</param>
    ///
    protected void ApplyDamage(in HitTarget hit, int damage, DAMAGE_TYPE currentDmgType)
    {
        // 복제탄(남의 발사 연출)은 데미지 판정 권위 없음 — 소멸/이펙트만 하고 데미지는 스킵.
        if (!_hasDamageAuthority)
        {
            return;
        }

        // 데미지를 안 받는 대상(환경 오브젝트 등)이면 데미지 로직 생략. 피격 반응은 호출부가 따로 처리함.
        if (hit.damageable == null)
        {
            return;
        }

        // 아군 타격 방지
        if (IsSameTeam(hit.damageable as Unit))
        {
            //Debug.Log("ApplyDamage 상대가 같은팀");
            return;
        }
		//Debug.Log("ApplyDamage 실제 데미지발생");
		// 데미지 정보 생성 및 전달
		HitInfo hitInfo = new HitInfo
		{
            type = currentDmgType,
            damageAmount = damage,
            isCritical = _critical,
            critMultiplier = _critMultiplier,
            hitPosition = hit.point,
            hitDiriection = (hit.point - transform.position).normalized,
            attacker = this.attacker != null ? this.attacker.gameObject : null,
            hitSoundType = this.hitSoundType,
            hitVfxType = this.hitVfxType,
            shieldHitVfxType = this.shieldHitVfxType,
            ignoreArmor = this.ignoreArmor,
            shieldDamageMultiplier = this.shieldDamageMultiplier
        };

        hit.damageable.TakeDamage(hitInfo);
    }

    /// <summary>
    /// 데미지를 안 받는 환경 대상에 넘길 피격 정보. 데미지 관련 필드는 안 씀.
    /// </summary>
    protected HitInfo BuildHitInfo(in HitTarget hit)
    {
        return new HitInfo
        {
            type = dmgType,
            hitPosition = hit.point,
            hitDiriection = (hit.point - transform.position).normalized,
            attacker = attacker != null ? attacker.gameObject : null,
            hitVfxType = this.hitVfxType,
            shieldHitVfxType = this.shieldHitVfxType,
        };
    }


    /// <summary>
    /// 스플뎀용 오버라이드>>레거시 현재 사용안함
    /// </summary>
    /// <param name="target"> 피격자</param>
    /// <param name="targetCollider"> 피격대상의 collider정보</param>
    /// <param name="damage"> 계산된 최종 데미지</param>
    /// <param name="currentDmgType"> 데미지 타입정보</param>
  //  protected void ApplyDamage(IDamageable target, Collider targetCollider, int damage, DAMAGE_TYPE currentDmgType)
  //  {
  //      // 데미지를 받을 수 없는 대상(벽 등)이면 데미지 로직 생략
  //      if (target == null)
  //      {
  //          Debug.Log("ApplyDamage 상대가 null");
  //          return;
  //      }

  //      // 아군 타격 방지
  //      if (IsSameTeam(target as Unit))
  //      {
  //          Debug.Log("ApplyDamage 상대가 같은팀");
  //          return;
  //      }
		//HitInfo hitInfo = new HitInfo
		//{
  //          type = currentDmgType,
  //          damageAmount = damage,
  //          isCritical = _critical,
  //          hitPosition = targetCollider.ClosestPoint(transform.position),
  //          hitDiriection = (targetCollider.ClosestPoint(transform.position) - transform.position).normalized,
  //          attacker = this.attacker != null ? this.attacker.gameObject : null,
  //          hitSoundType = this.hitSoundType,
  //          hitVfxType = this.hitVfxType,
  //          shieldHitVfxType = this.shieldHitVfxType,
  //          ignoreArmor = this.ignoreArmor,
  //          shieldDamageMultiplier = this.shieldDamageMultiplier
  //      };
  //      target.TakeDamage(hitInfo);
  //  }

    /// <summary>
    /// AOE 스플뎀 전용. 폭발 중심 좌표와 반경을 함께 전달해 파츠 범위 피격 처리.
    /// </summary>
    protected void ApplyDamage(IDamageable target, Collider targetCollider, int damage, DAMAGE_TYPE currentDmgType, Vector3 explosionCenter, float aoeRadius)
    {
        // 복제탄(남의 발사 연출)은 데미지 판정 권위 없음 — 데미지 스킵(폭발 연출은 호출부에서 별도 처리).
        if (!_hasDamageAuthority)
        {
            return;
        }
        if (target == null)
        {
            return;
        }
        if (IsSameTeam(target as Unit))
        {
            return;
        }
		HitInfo hitInfo = new HitInfo
		{
            type = currentDmgType,
            damageAmount = damage,
            isCritical = _critical,
            critMultiplier = _critMultiplier,
            hitPosition = targetCollider.ClosestPoint(explosionCenter),  // 변경 260611
            hitDiriection = (targetCollider.ClosestPoint(explosionCenter) - explosionCenter).normalized,  // 변경 260611
            attacker = this.attacker != null ? this.attacker.gameObject : null,
            hitSoundType = this.hitSoundType,
            hitVfxType = this.hitVfxType,
            shieldHitVfxType = this.shieldHitVfxType,
            ignoreArmor = this.ignoreArmor,
            shieldDamageMultiplier = this.shieldDamageMultiplier,
            aoeRadius = aoeRadius
        };
        target.TakeDamage(hitInfo);
    }



    /// <summary>
    /// 투사체를 풀로 반납. 소멸 시 반드시 이걸로 처리.
    /// </summary>
    protected void ReturnToPool()
    {
        //gameObject.SetActive(false);풀매니저에서 비활성화로 변경
        PoolManager.Instance.ReturnProjectile(this);
    }

    protected virtual void OnDisable()
    {
        attacker = null;
    }


}
