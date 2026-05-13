using System.Collections;
using System.Collections.Generic;
using UnityEngine;



// 모든 투사체의 베이스 클래스.
// Unit처럼 공통 로직은 여기서, 세부 동작은 자식에서 override.
// PoolManager에서 꺼낼 때 Init()으로 초기화, 반납 시 OnDisable()로 처리.
public abstract class Projectile : MonoBehaviour
{

	[Header("<size=18>[투사체 공통 스탯 기본 설정]</size>")]
	//스피드설정
	[Header("속도 설정")]
	public float speed;
	//최대사거리
	[Header("최대 사거리 설정")]
	public float maxRange;


	//발사좌표(사거리계산용.)
	protected Vector3 startPos;
	//이동거리 구하기위한 이전좌표
	protected Vector3 prevPos;
	//실제 투사체가 이동한거리
	protected float traveledDistance = 0f;


	[Header("팀킬 가능 여부")]
	public bool friendlyFire = false;
	//데미지타입
	//자식 클래스 Awake에서 지정Bullet=BULLET, Missile=EXPLOSION, Laser=LASER
	[Header("데미지 타입")]
	public DAMAGE_TYPE dmgType;
	//데미지수치
	//인스펙터에서 설정하는 투사체 고유의 기본 데미지
	[Header("투사체 기본 데미지")]
	public int baseDamage;
	//스탯과 버프가 적용된 실제 적용 데미지 (변동 값)
	[Header("투사체 현재 실제 데미지")]
	public int curDamage;
	//공격자
	[Header("공격한 유닛(발사)")]
	public Unit attacker;

	//풀매니저에서 식별할 투사체 타입
	[HideInInspector]
	public PROJECTILE_TYPE projectileType;

	// 크리티컬 여부. Init()에서 attacker의 criChance로 판정.
	// 투사체가 크리 판정 담당 → DamageInfo.isCritical로 전달.
	protected bool critical;


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
		traveledDistance += (transform.position - prevPos).magnitude;
		prevPos = transform.position;

		if (traveledDistance >= maxRange)
		{
			ReturnToPool();
		}
	
		//직선투사체에만 현재 사용안함.
		//if (Vector3.Distance(startPos, transform.position) >= maxRange)
		//{
		//	ReturnToPool();//풀로 돌리기.
		//}
	}

	//활성화시 넣을 정보. 플레이어에서 호출
	//꺼낼 때 호출. 매 발사마다 재초기화.
	//자식에서 오버라이드 시 base.Init() 반드시 호출.
	//출발좌표(firePos),향할방향, 공격자(쏜사람)
	public virtual void Init(Vector3 startPos, Vector3 dir, Unit attacker)
	{
		this.startPos = startPos;//출발할좌표
		this.attacker = attacker;//공격자(쏜사람)
		traveledDistance = 0f;
		prevPos = startPos;



		//투사체 데미지 최신화 (풀링오염방지)

		curDamage = baseDamage;//차후 로직 추가 필요.

		critical = Random.Range(0f, 100f) < attacker.criChance;//크리여부


		//물리처리를 위한 레이어 입력
		if (attacker.gameObject.layer == (int)LAYER_TYPE.Unit_Player)

		{
			gameObject.layer = (int)LAYER_TYPE.Projectile_Player;
		}
		else
		{
			gameObject.layer = (int)LAYER_TYPE.Projectile_Enemy;
		}
		//출발할좌표로 초기화
		transform.position = startPos;
		//향할 방향초기화
		transform.forward = dir;


	}


	//같은팀인지 체크
	protected bool IsSameTeam(Collider other)
	{

		//이 투사체가 플레이어 투사체고,부딪힌놈이 플레이어면 무ㅡ시
		if (gameObject.layer == (int)LAYER_TYPE.Projectile_Player &&
			other.gameObject.layer == (int)LAYER_TYPE.Unit_Player)
		{
			return true;
		}
		//이투사체가 적의 투사체고 맞은놈이 적이면
		if (gameObject.layer == (int)LAYER_TYPE.Projectile_Enemy &&
			other.gameObject.layer == (int)LAYER_TYPE.Unit_Enemy)
		{
			return true;
		}
		
		return false;
	}







	protected virtual void OnTriggerEnter(Collider other)
	{
		//Debug.Log("OnTrigger발생");
		
		//다른 시야감지용 트리거와 충돌방지.차후 수정필요할수도.
		if (other.isTrigger)
		{
			return;
		}
		//발사자 본인과의 즉각 충돌 방지
		if (attacker != null && other.gameObject == attacker.gameObject)
		{
			return;
		}

		// 부딪힌 대상에 대한 타격 처리 (적, 아군, 지형지물 구분 없이 실행)
		OnHit(other);
	}

	//온트리거 재정의할 함수들
	protected virtual void OnHit(Collider other)
	{
		//Debug.Log($"[OnHit 발생] 충돌 대상: {other.gameObject.name} | 레이어: {LayerMask.LayerToName(other.gameObject.layer)}");
	}


	
	/// <summary>
	/// 데미지 허용 메서드(온힛에서호출)
	///  </summary>
	/// <param name="targetCollider"> 충돌대상의 collider정보</param>
	/// <param name="damage"> 계산된 최종 데미지</param>
	/// <param name="currentDmgType"> 데미지 타입정보</param>
	///
	protected void ApplyDamage(Collider targetCollider, int damage, DAMAGE_TYPE currentDmgType)
	{
		
		IDamageable target = targetCollider.GetComponentInParent<IDamageable>();

		// 데미지를 받을 수 없는 대상(벽 등)이면 데미지 로직 생략
		if (target == null)
		{
			Debug.Log("ApplyDamage 상대가 null");
			return;
		}

		// 아군 타격 방지 (오인사격 Off 상태일 때 데미지 생략)
		if (IsSameTeam(targetCollider) && !friendlyFire)
		{
			Debug.Log("ApplyDamage 상대가 같은팀");
			return;
		}
		Debug.Log("ApplyDamage 실제 데미지발생");
		// 데미지 정보 생성 및 전달
		DamageInfo damageInfo = new DamageInfo
		{
			type = currentDmgType,
			damageAmount = damage,
			isCritical = critical,
			hitPosition = targetCollider.transform.position,
			hitDiriection = (targetCollider.transform.position - transform.position).normalized,
			attacker = this.attacker != null ? this.attacker.gameObject : null
		};

		target.TakeDamage(damageInfo);
	}

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
