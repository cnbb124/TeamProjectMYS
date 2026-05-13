using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;




// =====================================================================
// 미사일
// 폭발형. 범위(스플)데미지. barrel때를 참고.
// 락온가능. 유도성능있음.
// 
// Projectile 상속. 범위 폭발 + 유도 비행.
//
// 
// - 타겟의 이동 방향 예측해서 꺾는 각도(turnRate) 제한 있음.
// - 발사 직후 armDistance 동안은 직진 (근거리 자폭 방지).
// - 타겟 소실 시 직진 유지. maxRange 도달하면 자동 소멸.
// - 사거리는 Projectile 누적 이동거리 기준.
//
// [발사 측 호출 예시]
//   Missile m = PoolManager.Instance.GetMissile();
//   m.Init(firePos.position, firePos.forward, this, lockOnSystem.LockedTarget);
// =====================================================================
public class Missile : Projectile, IExplodable
{

	[Space(5)]
	[Header("<size=18>[미사일 설정]</size>")]
	[Header("폭발 범위 세팅")]
	public float explosionRadius = 8f;

	[Space(5)]
	[Header("--유도 설정--")]
	[Tooltip("초당 최대 선회 각도 (도/초). 클수록 날카롭게 꺾음.")]
	public float turnRate = 120f;

	[Tooltip("발사 직후 직진 유지 거리. 근거리 자폭 방지.")]
	public float armDistance = 5.0f;


	[Tooltip("비례항법 계수 (1~5). 클수록 예측 추적 강화. 3 권장.")]
	[Range(1f, 5f)]
	public float navGain = 3f;


	[Header("락온 목표")]
	public Transform targetTr;


	[Space(5)]
	[Header("--속도설정--")]

	//자연스러운 미사일 연출을 위한 속도 미세조정. 시작속도, 최고속도, 가속시간
	[Tooltip("발사 시작 속도. accelerateTime 동안 maxSpeed로 가속.")]
	public float launchSpeed = 10f;

	[Tooltip("최고 속도.")]
	public float maxSpeed = 60f;

	//현재속도
	private float curSpeed;

	[Tooltip("최고 속도 도달까지 걸리는 시간 (초).")]
	public float accelerateTime = 0.8f;

	public ExplosionInfo explosionInfo;

	//발사후 경과시간
	private float aliveTime = 0f;
	//락온타겟 이전좌표(추적용)
	private Vector3 prevTargetPos;





	protected override void Awake()
	{
		base.Awake();
		dmgType = DAMAGE_TYPE.EXPLOSION;
		projectileType = PROJECTILE_TYPE.MISSILE;

	}

	//미사일 기본 Init (락온x)
	public override void Init(Vector3 startPos, Vector3 dir, Unit attacker)
	{
		base.Init(startPos, dir, attacker);

		// 풀에서 꺼낼 때마다 인스펙터의 최신 damage 값으로 갱신
		explosionInfo.explosionDamage = this.curDamage;
		explosionInfo.explosionRadius = this.explosionRadius;
		//발사후경과시간
		aliveTime = 0f;
		//현재스피드를 발사스피드로 입력
		curSpeed = launchSpeed;

		//타겟이 있을경우. 타겟의 전 좌표 초기화
		if (targetTr != null)
		{
			prevTargetPos = targetTr.position;
		}

	}

	// 타겟까지 같이 넘기는 오버로드(락온o)
	public void Init(Vector3 startPos, Vector3 dir, Unit attacker, Transform target)
	{
		targetTr = target;
		Init(startPos, dir, attacker);
	}


	// Update is called once per frame
	protected override void Update()
	{
		base.Update();//최대사거리로직

		// 발사후 경과시간 업데이트
		aliveTime += Time.deltaTime;

		// 속도 가속, 발사시간>최대속도
		curSpeed = Mathf.Lerp(launchSpeed, maxSpeed, Mathf.Clamp01(aliveTime / accelerateTime));

		//타겟이 비활성화시 소실처리

		if (targetTr != null && !targetTr.gameObject.activeInHierarchy)
		{
			targetTr = null;
		}
		// 현재이동거리<직진거리보다 작거나 타겟이없으면 그냥 직진으로 판정
		if (traveledDistance < armDistance || targetTr == null)
		{
			transform.position += transform.forward * curSpeed * Time.deltaTime;
			return;
		}

		Steer();
	}


	private void Steer()
	{
		//비례항법기반 미사일 유도 조종 메서드 AI참조
		Vector3 temp = transform.forward;
		//방향벡터설정
		Vector3 toTarget = targetTr.position - transform.position;
		float dist = toTarget.magnitude;

		//타겟의 속도추정
		Vector3 targetVelocity = (targetTr.position - prevTargetPos) / Time.deltaTime;
		prevTargetPos = targetTr.position;
		//상대속도 구하기타겟의 속도에서 미사일의 현재 속도를 빼서, 두 객체가 서로에게 접근하거나 멀어지는 상대적인 속도구하기
		Vector3 closingVelocity = targetVelocity - transform.forward * curSpeed;
		//LineofSight, 미사일에서 타겟을 바라보는 방향
		Vector3 los = toTarget.normalized;
		//시선변화율
		Vector3 losRate = Vector3.Cross(los, closingVelocity) / Mathf.Max(dist, 0.1f);
		//
		Vector3 accelCmd = navGain * curSpeed * losRate;
		
		Vector3 desiredDir = accelCmd.sqrMagnitude > 0.001f? (transform.forward + accelCmd * Time.deltaTime).normalized : los;

		// turnRate로 선회 각도 제한
		Vector3 newDir = Vector3.RotateTowards(transform.forward, desiredDir,turnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);

		transform.forward = newDir;
		transform.position += transform.forward * curSpeed * Time.deltaTime;
	}

	protected override void OnTriggerEnter(Collider other)
	{
		//최소거리 도달안했으면 트리거무시
		if (traveledDistance < armDistance)
		{
			return; 
		}
		//그게아니면 판정주기
		base.OnTriggerEnter(other);
	}


	//온트리거에 쓸 재정의함수

	protected override void OnHit(Collider other)
	{
		base.OnHit(other);
		// 폭발 실행 후 투사체 소멸
		Explode(explosionInfo);
		ReturnToPool();
	}


	public void Explode(ExplosionInfo explosionInfo)
	{
		//이펙트 출력 로직 추가

		//맞은것들의 충돌박스 싹다가져오기
		Collider[] hits = Physics.OverlapSphere(transform.position, explosionInfo.explosionRadius);


		// 중복 타격 방지를 위한 HashSet 추가//차후 확인및보강필요
		HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();


		//맞은것들 전부처리
		foreach (Collider hit in hits)
		{
			IDamageable target = hit.GetComponentInParent<IDamageable>();

			if (target != null && !damagedTargets.Contains(target))
			{
				ApplyDamage(hit, explosionInfo.explosionDamage, this.dmgType);
				damagedTargets.Add(target); // 타격 대상 기록 차후확인및보강필요
			}
		}
	}
}
