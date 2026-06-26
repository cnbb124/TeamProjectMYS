using System.Collections;
using System.Collections.Generic;
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
// - 발사 직후 straightFlightDistance 동안은 직진(유도 안 함).
// - 타겟 소실 시 직진 유지. maxRange 도달하면 자동 소멸.
// - 사거리는 Projectile 누적 이동거리 기준.
//
// [발사 측 호출 예시]
//   Missile m = PoolManager.Instance.GetMissile();
//   m.Init(firePos.position, firePos.forward, this, lockOnSystem.LockedTarget);
// =====================================================================

// 앞으로 확장방향
//"변형탄"은 어떻게 만드나
//같은 프리팹 + 다른 SO 에셋 = 변형탄. 예: Missile_Homing.prefab에 data 필드만 MissileData_Homing_Default.asset
//대신 MissileData_Homing_ShieldBreaker.asset(shieldDamageMultiplier 높음)로 바꾼 **프리팹 변형(Prefab Variant)**을 만들면 끝.
//핵미사일도 동일: DumbMissile.prefab 변형 + MissileData_Nuke.asset(explosionRadius 매우 큼) 연결, 클래스 추가 불필요 
//PoolManager / WeaponSystem 연동
//풀에서 꺼낸 인스턴스의 data 필드는 프리팹에 미리 박혀있는 값 (풀링해도 유지됨, Init에서 매번 복사하므로 풀 오염 걱정 없음)
//발사 시점에 WeaponSystem이 데이터를 넘길 필요 없음 — 프리팹 자체가 자기 데이터를 알고 있음. WeaponSystem은 그냥 Init(pos, dir, attacker)만 호출
//작업 순서 (세션25 합의 기준 그대로)
//DamageInfo에 ignoreArmor, shieldDamageMultiplier 추가 + calculTakeDamage 반영 (A안: 배율은 실드 차감량에만 적용)
//ProjectileData/BulletData/MissileData SO 클래스 작성 + Create Data/Item/Projectile Data/... 메뉴 등록
//Missile.Init()에 데이터 복사 로직 연동 (유도미사일부터)
//ClusterMissile 분리유도 작업 시 MissileData 그대로 재사용 (자탄용 별도 에셋만 추가)
//핵미사일 = DumbMissile 변형 프리팹 + MissileData_Nuke.asset
//Bullet.Init()에 BulletData 연동, 기존 인스펙터 speed/baseDamage 값은 SO로 이전

public class Missile : Projectile, IExplodable
{
	//[SerializeField]
	//private int hitsArraySize = 30;
	[Space(5)]
	[Header("<size=22>[미사일 설정]</size>")]
	[Header("투사체 데이터(SO)")]
	public MissileData missileData;
	//[Header("폭발 범위 세팅")]
	[HideInInspector]
	[Tooltip("Missile의 실제 피해 범위. 변경시 이펙트 크기도 같이 변경됨.")]
	public float explosionRadius = 8f;
	[HideInInspector]
	[Tooltip("VFXManager에 연결된 폭발이펙트용 파티클 원본의 범위 입력. 원본값 입력 후 수정X.")]
	public float vfxBaseRadius = 8f;

	private Collider[] _explosionHits = new Collider[30]; //맞은것들 콜라이더 체크할배열 필요하면 스타트나 이닛쪽으로
	private HashSet<IDamageable> _damagedTargets = new HashSet<IDamageable>(); //중복데미지를 방지하기위한 해쉬셋

	[Space(5)]
	[HideInInspector]
	//[Header("<size=14>=====유도 설정=====</size>")]
	[Tooltip("초당 최대 선회 각도 (도/초). 클수록 날카롭게 꺾음.")]
	public float turnRate = 120f;

	[HideInInspector]
	[Tooltip("발사 직후 직진 유지 거리. 이 거리 전엔 유도(Steer) 안 하고 직진만 함.")]
	public float straightFlightDistance = 5.0f;

	[HideInInspector]
	//[Tooltip("비례항법 계수 (1~5). 클수록 예측 추적 강화. 3 권장.")]
	[Range(1f, 5f)]
	public float navGain = 3f;

	
	[Header("락온되는 목표(확인용)")]
	public Transform targetTr;


	[Space(5)]
	[Header("<size=14>=====속도 설정=====</size>")]

	[HideInInspector]
	//자연스러운 미사일 연출을 위한 속도 미세조정. 시작속도, 최고속도, 가속시간
	[Tooltip("발사 시작 속도. accelerateTime 동안 maxSpeed로 가속.")]
	public float launchSpeed = 10f;
	[HideInInspector]
	//스피드설정
	[Tooltip("최대 도달 속도")]
	public float maxSpeed;
	[HideInInspector]
	//[Header("현재 미사일 속도(참고용 입력x)")]
	//현재속도
	public float curSpeed;
	[HideInInspector]
	[Tooltip("최고 속도 도달까지 걸리는 시간 (초).")]
	public float accelerateTime = 0.8f;
	[HideInInspector]
	public ExplosionInfo explosionInfo;

	//폭발음. Explode()에서 1번만 재생. SO에서 복사됨.
	[HideInInspector]
	public SOUND_TYPE explosionSoundType;

	//발사후 경과시간
	private float _aliveTime = 0f;
	//락온타겟 이전좌표(추적용)
	private Vector3 _prevTargetPos;

	//계산된 미사일의 추진 속도
	private float _thrustSpeed;




	protected override void Awake()
	{
		base.Awake();
		dmgType = DAMAGE_TYPE.EXPLOSION;
		projectileType = PROJECTILE_TYPE.MISSILE;

		// 미사일은 hitSoundType 미사용(폭발음은 explosionSoundType이 담당, 같이 쓰면 중복재생).
		// Projectile.hitSoundType 기본값(enum 0번=BGM_LOBBY)이 그대로 남는 걸 막기 위해 명시적으로 고정.
		hitSoundType = SOUND_TYPE.SFX_NONE;
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


		if (missileData != null)
		{
			launchSpeed = missileData.launchSpeed;
			maxSpeed = missileData.maxSpeed;
			accelerateTime = missileData.accelerateTime;
			turnRate = missileData.turnRate;
			straightFlightDistance = missileData.straightFlightDistance;
			navGain = missileData.navGain;
			explosionRadius = missileData.explosionRadius;
			vfxBaseRadius = missileData.vfxBaseRadius;
			baseDamage = missileData.damage;
			maxRange = missileData.maxRange;
			explosionSoundType = missileData.explosionSoundType;
		}


		// 풀에서 꺼낼 때마다 인스펙터의 최신 damage 값으로 갱신
		explosionInfo.explosionDamage = this.curDamage;
		explosionInfo.explosionRadius = this.explosionRadius;
		//발사후경과시간
		_aliveTime = 0f;
		//현재 추진 스피드를 발사스피드로 입력
		_thrustSpeed = launchSpeed;
		curSpeed = 0f;

		//타겟이 있을경우. 타겟의 전 좌표 초기화
		if (targetTr != null)
		{
			_prevTargetPos = targetTr.position;
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
		_aliveTime += Time.deltaTime;

		// 속도 가속, 발사시간>최대속도
		_thrustSpeed = Mathf.Lerp(launchSpeed, maxSpeed, Mathf.Clamp01(_aliveTime / accelerateTime));


		// [추가수정]Steer() 진입 여부와 무관하게 매 프레임 타겟의 속도를 계산하고 이전 좌표를 갱신
		Vector3 targetVelocity = Vector3.zero;
		if (targetTr != null)
		{
			float dt = Mathf.Max(Time.deltaTime, 0.001f);
			targetVelocity = (targetTr.position - _prevTargetPos) / dt;
			_prevTargetPos = targetTr.position; // 직진(straightFlightDistance) 기간에도 정상 갱신됨
		}

		if (_traveledDistance < straightFlightDistance || targetTr == null)
		{
			transform.position += transform.forward * _thrustSpeed * Time.deltaTime;
		}
		else
		{
			// 계산된 정상 속도를 유도 로직에 전달
			Steer(targetVelocity);
		}

		curSpeed = Vector3.Distance(transform.position, _prevPos) / Time.deltaTime;
		base.Update();
	}

	protected override void OnMaxRange()
	{
		Explode(explosionInfo);
		base.OnMaxRange();
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
			transform.position += transform.forward * _thrustSpeed * Time.deltaTime;
			return;
		}

		Vector3 desiredDir = toTarget.normalized;

		//[추가수정] 타겟의 미래 위치를 계산하는 예측 추적(Predictive Pursuit) 알고리즘
		if (targetVelocity.sqrMagnitude > 0.1f)
		{
			// 현재 속도로 타겟까지 도달하는 데 걸리는 예상 시간(ETA)
			float timeToHit = dist / Mathf.Max(_thrustSpeed, 1f);

			// 거리가 너무 멀 때 예측 좌표가 우주로 튀는 것을 막기 위해 최대 1.5초 후의 위치까지만 예측
			timeToHit = Mathf.Min(timeToHit, 1.5f);

			// 타겟의 미래 예측 위치 도출
			Vector3 predictedPos = targetTr.position + (targetVelocity * timeToHit);

			desiredDir = (predictedPos - transform.position).normalized;
		}

		//[추가수정] 예측된 방향으로 부드럽게 회전 및 전진
		Vector3 newDir = Vector3.RotateTowards(transform.forward, desiredDir, turnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
		transform.forward = newDir;
		transform.position += transform.forward * _thrustSpeed * Time.deltaTime;
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
		SoundManager.Instance.PlaySFX3DAtPosition(explosionSoundType, transform.position);
		//맞은것들의 충돌박스 갯수 카운트
		int hitMask = LayerMask.GetMask("HitBox");
		int hitCount = Physics.OverlapSphereNonAlloc(transform.position, explosionInfo.explosionRadius, _explosionHits, hitMask);


		//int hitCount = Physics.OverlapSphereNonAlloc(transform.position, explosionInfo.explosionRadius, explosionHits);
		//Debug.Log($"hitCount: {hitCount}, radius: {explosionInfo.explosionRadius}");
		// 중복 타격 방지를 위한 HashSet 초기화
		_damagedTargets.Clear();




		//맞은것들 전부처리
		for (int i = 0; i < hitCount; i++)
		{
			//맞은것들중 부모에 데미지받는애들 갖고오기
			IDamageable target = _explosionHits[i].GetComponentInParent<IDamageable>();

			// 타격 대상 기록 . 중복이없으면
			if (target != null && !_damagedTargets.Contains(target))
			{
				if ((object)target == attacker)
				{
					continue;
				}
				// 거리 비례 데미지 감쇠 (중심 100%, 외곽 50%)
				float distRatio = 1f - (Vector3.Distance(transform.position, _explosionHits[i].transform.position) / explosionInfo.explosionRadius);
				int finalDamage = Mathf.RoundToInt(explosionInfo.explosionDamage * Mathf.Lerp(0.5f, 1f, distRatio));
				//데미지 실제적용 (AOE 오버로드: 폭발 중심 + 반경 전달)
				ApplyDamage(target, _explosionHits[i], finalDamage, this.dmgType, _explosionHits[i].ClosestPoint(transform.position), explosionInfo.explosionRadius);
				//중복체크용 해쉬셋Add
				_damagedTargets.Add(target);
			}
		}
	}


	protected override void OnDisable()
	{
		base.OnDisable();
		targetTr = null;
		_aliveTime = 0f;
	}
}
