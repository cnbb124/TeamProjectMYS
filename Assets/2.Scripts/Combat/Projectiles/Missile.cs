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
	//[SerializeField]
	//private int hitsArraySize = 30;
	[Space(5)]
	[Header("<size=18>[미사일 설정]</size>")]
	[Header("폭발 범위 세팅")]
	[Tooltip("Missile의 실제 피해 범위. 변경시 이펙트 크기도 같이 변경됨.")]
	public float explosionRadius = 8f;

	[Tooltip("VFXManager에 연결된 폭발이펙트용 파티클 원본의 범위 입력. 원본값 입력 후 수정X.")]
	public float vfxBaseRadius = 8f;

	private Collider[] explosionHits = new Collider[30]; //맞은것들 콜라이더 체크할배열 필요하면 스타트나 이닛쪽으로
	private HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>(); //중복데미지를 방지하기위한 해쉬셋

	[Space(5)]
	[Header("<size=14>=====유도 설정=====</size>")]
	[Tooltip("초당 최대 선회 각도 (도/초). 클수록 날카롭게 꺾음.")]
	public float turnRate = 120f;

	[Tooltip("발사 직후 직진 유지 거리. 근거리 자폭 방지.")]
	public float armDistance = 5.0f;


	[Tooltip("비례항법 계수 (1~5). 클수록 예측 추적 강화. 3 권장.")]
	[Range(1f, 5f)]
	public float navGain = 3f;


	[Header("락온되는 목표(확인용)")]
	public Transform targetTr;


	[Space(5)]
	[Header("<size=14>=====속도 설정=====</size>")]

	
	//자연스러운 미사일 연출을 위한 속도 미세조정. 시작속도, 최고속도, 가속시간
	[Tooltip("발사 시작 속도. accelerateTime 동안 maxSpeed로 가속.")]
	public float launchSpeed = 10f;

	//스피드설정
	[Tooltip("최대 도달 속도")]
	public float maxSpeed;

	[Header("현재 미사일 속도(입력x 참고용)")]
	//현재속도
	public float curSpeed;

	[Tooltip("최고 속도 도달까지 걸리는 시간 (초).")]
	public float accelerateTime = 0.8f;

	public ExplosionInfo explosionInfo;

	//발사후 경과시간
	private float aliveTime = 0f;
	//락온타겟 이전좌표(추적용)
	private Vector3 prevTargetPos;

	//계산된 미사일의 추진 속도
	private float thrustSpeed;




	protected override void Awake()
	{
		base.Awake();
		dmgType = DAMAGE_TYPE.EXPLOSION;
		projectileType = PROJECTILE_TYPE.MISSILE;

	}

	/// <summary>
	/// 미사일 기본 Init (락온x)
	/// </summary>
	/// <param name="startPos"> 발사되는좌표</param>
	/// <param name="dir"> 발사되는방향</param>
	/// <param name="attacker">쏜유닛</param>
	public override void Init(Vector3 startPos, Vector3 dir, Unit attacker)
	{
		base.Init(startPos, dir, attacker);

		// 풀에서 꺼낼 때마다 인스펙터의 최신 damage 값으로 갱신
		explosionInfo.explosionDamage = this.curDamage;
		explosionInfo.explosionRadius = this.explosionRadius;
		//발사후경과시간
		aliveTime = 0f;
		//현재 추진 스피드를 발사스피드로 입력
		thrustSpeed = launchSpeed;
		curSpeed = 0f;
		
		//타겟이 있을경우. 타겟의 전 좌표 초기화
		if (targetTr != null)
		{
			prevTargetPos = targetTr.position;
		}

	}

	/// <summary>
	/// 타겟까지 같이 넘기는 오버로드(락온o)
	/// </summary>
	/// <param name="startPos"> 발사되는좌표</param>
	/// <param name="dir"> 발사되는방향</param>
	/// <param name="attacker">쏜유닛</param>
	/// <param name="target"> 락온된 타겟, null허용.</param>
	public void Init(Vector3 startPos, Vector3 dir, Unit attacker, Transform target)
	{
		targetTr = target;
		Init(startPos, dir, attacker);
	}


	// Update is called once per frame
	protected override void Update()
	{
		//프레임따른 튐현상방지
		if (Time.deltaTime <= 0f)
		{
			return;
		}

		// 발사후 경과시간 업데이트
		aliveTime += Time.deltaTime;

		// 속도 가속, 발사시간>최대속도
		thrustSpeed = Mathf.Lerp(launchSpeed, maxSpeed, Mathf.Clamp01(aliveTime / accelerateTime));


		// [추가수정]Steer() 진입 여부와 무관하게 매 프레임 타겟의 속도를 계산하고 이전 좌표를 갱신
		Vector3 targetVelocity = Vector3.zero;
		if (targetTr != null)
		{
			float dt = Mathf.Max(Time.deltaTime, 0.001f);
			targetVelocity = (targetTr.position - prevTargetPos) / dt;
			prevTargetPos = targetTr.position; // 직진(armDistance) 기간에도 정상 갱신됨
		}

		if (traveledDistance < armDistance || targetTr == null)
		{
			transform.position += transform.forward * thrustSpeed * Time.deltaTime;
		}
		else
		{
			// 계산된 정상 속도를 유도 로직에 전달
			Steer(targetVelocity);
		}

		curSpeed = Vector3.Distance(transform.position, prevPos) / Time.deltaTime;
		base.Update();
	}


	private void Steer(Vector3 targetVelocity)
	{
		Vector3 toTarget = targetTr.position - transform.position;
		float dist = toTarget.magnitude;

		//타겟과 일정 거리 이내로 좁혀지면 미사일이 맴도는 현상(Orbiting) 방지
		//거리가 가까울 때는 복잡한 예측을 버리고 타겟을 향해 즉시 내리꽂도록 강제
		if (dist < 4.0f)
		{
			Vector3 finalDir = Vector3.RotateTowards(transform.forward, toTarget.normalized, turnRate * 2f * Mathf.Deg2Rad * Time.deltaTime, 0f);
			transform.forward = finalDir;
			transform.position += transform.forward * thrustSpeed * Time.deltaTime;
			return;
		}

		Vector3 desiredDir = toTarget.normalized;

		//[추가수정] 타겟의 미래 위치를 계산하는 예측 추적(Predictive Pursuit) 알고리즘
		if (targetVelocity.sqrMagnitude > 0.1f)
		{
			// 현재 속도로 타겟까지 도달하는 데 걸리는 예상 시간(ETA)
			float timeToHit = dist / Mathf.Max(thrustSpeed, 1f);

			// 거리가 너무 멀 때 예측 좌표가 우주로 튀는 것을 막기 위해 최대 1.5초 후의 위치까지만 예측
			timeToHit = Mathf.Min(timeToHit, 1.5f);

			// 타겟의 미래 예측 위치 도출
			Vector3 predictedPos = targetTr.position + (targetVelocity * timeToHit);

			desiredDir = (predictedPos - transform.position).normalized;
		}

		//[추가수정] 예측된 방향으로 부드럽게 회전 및 전진
		Vector3 newDir = Vector3.RotateTowards(transform.forward, desiredDir, turnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
		transform.forward = newDir;
		transform.position += transform.forward * thrustSpeed * Time.deltaTime;
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
		//Debug.Log("미사일 OnHit발동");
		// 폭발 실행 후 투사체 소멸
		Explode(explosionInfo);
		ReturnToPool();
	}


	public void Explode(ExplosionInfo explosionInfo)
	{

		//이펙트 출력 로직 추가(사운드,파티클)
		//폭발 반경(explosionRadius) 비율에 맞춰 VFX 크기 조절
		float vfxRatio = explosionInfo.explosionRadius / vfxBaseRadius;
		VFXManager.Instance.PlayEffectAtPosition(EFFECT_TYPE.VFX_EXPLOSION_MISSILE, transform.position, Quaternion.identity, 0f, Vector3.one * vfxRatio);
		SoundManager.Instance.PlaySFX3DAtPosition(SOUND_TYPE.SFX_EXPLOSION, transform.position);
		//맞은것들의 충돌박스 갯수 카운트
		int hitCount = Physics.OverlapSphereNonAlloc(transform.position, explosionInfo.explosionRadius, explosionHits);
		//Debug.Log($"hitCount: {hitCount}, radius: {explosionInfo.explosionRadius}");
		// 중복 타격 방지를 위한 HashSet 초기화
		damagedTargets.Clear();




		//맞은것들 전부처리
		for (int i = 0; i < hitCount; i++)
		{
			//맞은것들중 부모에 데미지받는애들 갖고오기
			IDamageable target = explosionHits[i].GetComponentInParent<IDamageable>();

			// 타격 대상 기록 . 중복이없으면
			if (target != null && !damagedTargets.Contains(target))
			{

				// 거리 비례 데미지 감쇠 (중심 100%, 외곽 50%)
				float distRatio = 1f - (Vector3.Distance(transform.position, explosionHits[i].transform.position) / explosionInfo.explosionRadius);
				int finalDamage = Mathf.RoundToInt(explosionInfo.explosionDamage * Mathf.Lerp(0.5f, 1f, distRatio));
				//데미지 실제적용 (AOE 오버로드: 폭발 중심 + 반경 전달)
				ApplyDamage(target, explosionHits[i], finalDamage, this.dmgType, transform.position, explosionInfo.explosionRadius);
				//중복체크용 해쉬셋Add
				damagedTargets.Add(target);
			}
		}
	}


	protected override void OnDisable()
	{
		base.OnDisable();
		targetTr = null;
		aliveTime = 0f;
	}
}
