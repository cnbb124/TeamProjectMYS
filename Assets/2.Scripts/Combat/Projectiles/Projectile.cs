using System.Collections;
using System.Collections.Generic;
using UnityEngine;



// 모든 투사체의 베이스 클래스.
// Unit처럼 공통 로직은 여기서, 세부 동작은 자식에서 override.
// PoolManager에서 꺼낼 때 Init()으로 초기화, 반납 시 OnDisable()로 처리.
public abstract class Projectile : MonoBehaviour
{

	[Header("투사체 공통스탯 기본 설정")]
	//스피드설정
	[Header("속도 설정")]
	public float speed;
	//최대사거리
	[Header(" 최대 사거리 설정")]
	public float maxRange;

	//데미지타입
	//자식 클래스 Awake에서 지정Bullet=BULLET, Missile=EXPLOSION, Laser=LASER
   [Header("데미지 타입")]
	public DAMAGE_TYPE dmgType;
	//데미지수치
	[Header("데미지 수치")]
	public int damage;
	//공격자
	[Header("공격한 유닛(발사)")]
	public Unit attacker;

	// 크리티컬 여부. Init()에서 attacker의 criChance로 판정.
	// 투사체가 크리 판정 담당 → DamageInfo.isCritical로 전달.
	protected bool critical;
	//발사좌표(사거리계산용.)
	private Vector3 startPos;

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
		//출발지점과 현재지점이>=최대사거리 도달혹은초과시
		if (Vector3.Distance(startPos, transform.position) >= maxRange)
		{
			ReturnToPool();//풀로 돌리기.
		}
	}

	//풀매니저에서 활성화시 넣을 정보.
	//꺼낼 때 호출. 매 발사마다 재초기화.
	//자식에서 오버라이드 시 base.Init() 반드시 호출.
	//출발좌표(firePos),향할방향, 공격자
	public virtual void Init(Vector3 pos, Vector3 dir, Unit attacker)
	{
		startPos = pos;//출발할좌표
		this.attacker = attacker;//공격자

		critical = Random.Range(0f, 100f) < attacker.criChance;//크리여부

		//출발할좌표로 초기화
		transform.position = pos;
		//향할 방향초기화
		transform.forward = dir;
		//활성화.
		gameObject.SetActive(true);

	}

	//온콜리전에서 호출할함수
	protected virtual void OnHit(Collider other)
	{
		IDamageable target = other.GetComponent<IDamageable>();
		if (target == null)
		{
			return;
		}

		//데미지인포구조체 임시생성
		DamageInfo damageInfo = new DamageInfo
		{
			//피해유형
			type = dmgType,
			//데미지수치
			damageAmount = damage,
			//크리티컬여부
			isCritical = critical,
			//맞은좌표=현재오브젝트(투사체,projectile,this)좌표
			hitPosition = transform.position,
			//맞은방향=현재오브젝트(투사체,projectile,this)의 앞방향에서.
			hitDiriection = transform.forward,
			//공격자정보=현재오브젝트의 gameobject
			attacker = this.attacker.gameObject


		};

		//맞은타겟에게 테이크 데미지 함수 호출(IDamageable)
		target.TakeDamage(damageInfo);
		//풀로 복귀
		ReturnToPool();

	}

	protected void ReturnToPool()
	{
		gameObject.SetActive(false);
	}

	protected virtual void OnDisable()
	{
		attacker = null;
	}


}
