using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.PlayerLoop;

[System.Serializable]
public class FirePosEntry
{
	public FIREPOS_TYPE type;
	public Transform pos;
}

[System.Serializable]
public class BoostPosEntry
{
	public BOOSTPOS_TYPE type;
	public Transform pos;
}

[RequireComponent(typeof(Rigidbody))]
public abstract class Unit : MonoBehaviour, IDamageable
{


	//==================레퍼런스==================//




	//==================유닛데이터==================//

	[Header("<size=18>기본 스탯 설정창</size>")]
	
	[Header("HP")]
	public int maxHpRemaining; //최대,현재HP수치
	

	[Header("Shield - 피격후 일정딜레이 후 자동회복")]
	public int maxShieldRemaining;//최대,현재실드수치
	
	public float shieldRegainDelay;//피격후 회복까지딜레이시간
	public float shieldRegainRate; //실드회복수치
	private float shieldRegainTimer = 0f;//딜레이 시간까지잴 타이머
	private bool isShieldRegaining = false; //회복중인지 여부




	public GameObject shield;

	[Header("Armor - 자동회복x")]
	public int maxArmor;//최대,현재아머수치
	

	[Tooltip("Armor보유시 데미지 경감되는 수치.")]
	public int defense;//아머 있을시 데미지 경감수치(damageAmount=damage-defense)



	[Header("Critical")]
	public float criChance;
	public float criDamageMultiplier;


	[Header("사격 관련 설정")]
	public float fireDelay = 0.1f; // 총알 발사 간격 (초)
	protected float lastFireTime = 0f;

	// 크리여부 판정은 투사체가 담당 크확은 유닛이. → DamageInfo.isCritical로 전달받음
	// criChance는 투사체 생성 시 attacker에서 복사해서 사용
	//데미지 계산식
	//shield>armor>hp순 실드없고 armor있을때는 경감수치만큼 데미지 경감
	//damageAmount=
	//(실드o,아머x)(Damageinfo.damage) * (크리시)criDamageMultiplier;
	//(실드x,아머o)Damageinfo.damage-defense *(크리시)criDamageMultiplier;
	//(실드x,아머x)Damageinfo.damage) * (크리시)criDamageMultiplier;
	//curHp-=damageAmount;

	[Header("이동 관련")]
	public float baseMoveSpeed;//기본이동속ㄷ
	public float boostSpeed;//부스트시 이동속도
	public float maxSpeed;//최대속도velocity가 넘어갈시 고정시킬속도  
	public float maxBoostRemaining;//최대,현재 부스트수치
	
	public float boostRegainDelay;//부스트 회복딜레이
	public float boostRegainRate;//초당 부스트 잔량회복수치
	private float boostRegainTimer = 0f;//부스트 회복딜레이까지 잴 타이머
	private bool isBoostRegaining = false;//회복유무


	[Tooltip("피격부위 혹은 HP잔량에 따른이동속도 변경용")]
	public float speedMultiPlier;//HP 혹은 피격부위에따른 속도조절용.

	public float viewDistance;
	public float lockOnDistance;

	
	[Header("이펙트 위치(총구,부스터등)")]
	public FirePosEntry[] firePositions; // 인스펙터에서 타입+Transform 쌍으로 등록
	public BoostPosEntry[] boosterEffectPositions;//옆무빙시 부스터이펙트 추가필요.enum에 타입등추가필요.left,right,역분사,정분사,부스트상태등
	private Dictionary<FIREPOS_TYPE, Transform> _firePosDict= new Dictionary<FIREPOS_TYPE, Transform>();
	private Dictionary<BOOSTPOS_TYPE, Transform> _boostPosDict = new Dictionary<BOOSTPOS_TYPE, Transform>();

	protected Transform GetFirePos(FIREPOS_TYPE type)
	{
		if (_firePosDict.TryGetValue(type, out Transform pos))
		{
			return pos;
		}
		Debug.LogWarning($"[Unit] FirePos 미설정: {type}");
		return null;
	}
	protected Transform GetBoostPos(BOOSTPOS_TYPE type)
	{
		if (_boostPosDict.TryGetValue(type, out Transform pos))
		{
			return pos;
		}
		Debug.LogWarning($"[Unit] BoostPos 미설정: {type}");
		return null;
	}


	//[HideInInspector]
	//public Transform curFirePos;//밑에서 총구스위칭용 
	//필요없음.

	[Header("현재 상태")]
	public UNIT_STATE curState = UNIT_STATE.IDLE;
	public int curHpRemaining;
	public int curShieldRemaining;
	public int curArmorRemaining;
	public float curBoostRemaining;//부스트잔량
	

	//// ==================레이어==================
	//[HideInInspector]
	//public int playerLayer;
	//[HideInInspector]
	//public int enemyLayer;
	//[HideInInspector]
	//public int groundLayer;//행성등 지형지물, 차후 수정필요
	//[HideInInspector]
	//public int ItemLayer;//아이템레이어 추가필요
	//[HideInInspector]
	//public int playerProjectileLayer;
	//[HideInInspector]
	//public int enemyProjectileLayer;


	[HideInInspector]
	public SOUND_TYPE _playSoundType;


	//=============기타 레퍼런스===============
	//리지드바디 할당용 레퍼런스
	protected Rigidbody _rb;
	//매니저 할당용 레퍼런스
	protected SoundManager _sound;
	protected PoolManager _pool;

	protected virtual void Awake()
	{
		_rb = GetComponent<Rigidbody>();
	}




	// Start is called before the first frame update
	protected virtual void Start()
	{
		_sound = SoundManager.Instance;
		_pool = PoolManager.Instance;
		//인스펙터에서 입력된 값 현재 스탯으로 설정
		//저장 기능 생길시 변경필요.
		curHpRemaining = maxHpRemaining;
		curShieldRemaining = maxShieldRemaining;
		curArmorRemaining = maxArmor;
		curBoostRemaining = maxBoostRemaining;

		//playerLayer = LayerMask.NameToLayer("UNIT_Player");
		//enemyLayer = LayerMask.NameToLayer("UNIT_Enemy");
		//groundLayer = LayerMask.NameToLayer("Environment");
		////아이템 레이어 추가필요ItemLayer = LayerMask.NameToLayer("");
		//playerProjectileLayer = LayerMask.NameToLayer("PlayerProjectile");
		//enemyProjectileLayer = LayerMask.NameToLayer("EnemyProjectile");

		CurState = UNIT_STATE.IDLE;
		foreach (FirePosEntry entry in firePositions)
		{
			_firePosDict[entry.type] = entry.pos;
		}
	}

	// Update is called once per frame
	protected virtual void Update()
	{
		UpdateFSM();
		UpdateShieldRegen();
		UpdateBoostRegen();
	}

	protected virtual void FixedUpdate()
	{
		
	}


	//===================FSM======================d
	

	public UNIT_STATE CurState
	{
		get
		{
			return curState;
		}
		set
		{
			if (curState == value)//셋할때 똑같으면 필요없음로
			{
				return;
			}
			//현재상태에서 나가는 메섣
			OnStateExit(curState);
			//넣은값 적용해주고
			curState = value;
			//들어가는 메서드
			OnStateEnter(curState);
		}
	}

	private void UpdateFSM()
	{
		switch (curState)
		{
			case UNIT_STATE.IDLE:
				OnIdle();
				break;
			case UNIT_STATE.MOVING:
				OnMoving();
				break;
			case UNIT_STATE.DODGE:
				OnDodge();
				break;
			case UNIT_STATE.DIE:
				OnDying();
				break;
		}
	}

	//===============자식에서 직접 override==================
	protected virtual void OnStateEnter(UNIT_STATE state)
	{

	}
	protected virtual void OnStateExit(UNIT_STATE state)
	{

	}
	protected virtual void OnIdle()
	{
		//애니메이션명령, 사운드재생?
	}
	protected virtual void OnMoving()
	{
		//애니메이션명령, 사운드재생?
	}
	protected virtual void OnDodge()
	{
		//애니메이션명령, 사운드재생?
	}
	protected virtual void OnDying()
	{
		//애니메이션명령, 사운드재생?
		//죽는처리
	}

	//실드회복
	private void UpdateShieldRegen()
	{
		if (curState == UNIT_STATE.DIE || curShieldRemaining >= maxShieldRemaining)
		{
			return;
		}
		//if(curShieldRemaning>=maxShieldRemaning)//디버그 로깅같은거 필요하면 주석풀고 위에서 지울것
		//{
		//	return;
		//}
		//타이머에 일정시간더해주고
		shieldRegainTimer += Time.deltaTime;
		//타이머가 딜레이보다 커졌고 충전중이아닐때, 즉 딜레이만큼시간지났을떄
		if (!isShieldRegaining && shieldRegainTimer >= shieldRegainDelay)
		{
			isShieldRegaining = true;
		}

		if(isShieldRegaining)
		{
			//반올림공식
			curShieldRemaining += Mathf.RoundToInt(shieldRegainRate * Time.deltaTime);
			//혹여나 초과시 제한걸도록 둘중 작은값 반환하는 함수(동일시 그값반환)
			curShieldRemaining = Mathf.Min(curShieldRemaining, maxShieldRemaining);
		}

	}
	//부스트회복
	private void UpdateBoostRegen()
	{
		if (curState == UNIT_STATE.DIE || curBoostRemaining >= maxBoostRemaining)
		{
			return;
		}
		//if(curBoostRemaining>=maxBoostRemaining)//디버그 로깅같은거 필요하면 주석풀고 위에서 지울것
		//{
		//	return;
		//}
		//타이머에 일정시간더해주고
		boostRegainTimer += Time.deltaTime;
		//타이머가 딜레이보다 커졌고 충전중이아닐때, 즉 딜레이만큼시간지났을떄
		if(!isBoostRegaining && boostRegainTimer >= boostRegainDelay )
		{
			isBoostRegaining = true;
		}

		if(isBoostRegaining)
		{
			curBoostRemaining += boostRegainRate*Time.deltaTime;
			curBoostRemaining = Mathf.Min(curBoostRemaining, maxBoostRemaining);//실드와동일
		}
	}

	//부스트사용
	public void UseBoost(float amount)
	{
		curBoostRemaining = Mathf.Max(0f, curBoostRemaining - amount);
		boostRegainTimer = 0f;
		isBoostRegaining = false;
	}



	//(실드o,아머x)(Damageinfo.damage) * (크리시)criDamageMultiplier;
	//(실드x,아머o)Damageinfo.damage-defense *(크리시)criDamageMultiplier;
	//(실드x,아머x)Damageinfo.damage) * (크리시)criDamageMultiplier;
	//반올림할것. 0.5->1 0.4->0

	//자식에서 오버라이드
	public virtual void Shoot(PROJECTILE_TYPE type)
	{
		
	}

	public virtual void TakeDamage(DamageInfo info)
	{
		//피격 애니메이션재생 필요
		//피격 사운드재생 필요
		_playSoundType = GetPlaySoundType(info);
		_sound.PlaySFX3DAtPosition(_playSoundType, info.hitPosition);
		//피격 카메라무빙필요

		//크리면 데미지 배율, 아니면 그냥 데미지


		//info.isCritical = Random.Range(0f, 100f) < criChance; //크리판정은 투사체에서 직접담당.
		int damageAmount = info.isCritical ? Mathf.RoundToInt(info.damageAmount * criDamageMultiplier) : info.damageAmount;
		//실드회복중지, 타이머 초기화
		shieldRegainTimer = 0f;
		isShieldRegaining = false;

		//피격 데미지수치필요(실드있을시, 없을시)
		calculTakeDamage(damageAmount);


		//피격 방향에 따른 이동(반동)피요
		OnHitReaction(info);

		if (curHpRemaining <= 0)
		{
			CurState = UNIT_STATE.DIE;
			Die();
		}

	}
	//피격 반동(카메라 쉐이크, 넉백등 자식에서 override)
	protected virtual void OnHitReaction(DamageInfo info)
	{

	}
	//사망처리(오브젝트 풀반납, 비활성화등. 플레이어와는 다르게 처리할거기때문에 자식에서 override)
	protected virtual void Die()
	{

	}

	//bool isCritical()
	//{
	//	float rand = Random.Range(0f, 100f);//0~100퍼
	//	return rand < criChance;
	//}

	protected void calculTakeDamage(int damageAmount)
	{
		if (curShieldRemaining > 0)
		{
			int shieldDamage = Mathf.Min(curShieldRemaining, damageAmount);//현지실드량보다 초과해서 -가되면 안됨
			curShieldRemaining -= shieldDamage;//실드에 가해진 피해량만큼 현재실드량 깎기
			damageAmount -= shieldDamage;//실드에 가해진피해량만큼 데미지잔량도 깎기

		}
		if (damageAmount > 0 && curArmorRemaining > 0)//데미지잔량0초과,실드0,아머0초과
		{
			int reducedDamage = Mathf.Max(1, damageAmount - defense);//아머가몇이건 최소 1이건 데미지들어감
			int armorDamage = Mathf.Min(curArmorRemaining, reducedDamage);//아머로 경감한데미지만큼 현재아머량깎기 초과해서 -가되면안되므로
			curArmorRemaining -= armorDamage;//아머에 가해진피해량만큼깎기
			damageAmount -= armorDamage;//아머에 가해진 피해량만큼 데미지잔량도깎기

		}
		if (damageAmount > 0)
		{

			int hpDamage = Mathf.Min(curHpRemaining, damageAmount);
			curHpRemaining -= hpDamage;
		}

	}



	public virtual void OnCollisionEnter(Collision collision)
	{
		
	}



	//재생할 사운드 찾는 함수 (오버로딩)
	//피격
	protected SOUND_TYPE GetPlaySoundType(DamageInfo info)
	{


		switch (info.type)
		{
			case DAMAGE_TYPE.BULLET:
				return SOUND_TYPE.SFX_BULLETHIT;

			case DAMAGE_TYPE.LASER:
				return SOUND_TYPE.SFX_LASERHIT;

			case DAMAGE_TYPE.EXPLOSION:
				return SOUND_TYPE.SFX_EXPLOSION;

			case DAMAGE_TYPE.CONTACT:
				if (info.attacker.CompareTag("Enemy"))
				{
					return SOUND_TYPE.SFX_CONTACTSHIP;
				}
				if (info.attacker.CompareTag("Ground"))
				{
					return SOUND_TYPE.SFX_CONTACTGROUND;
				}
				break;
		}
		return SOUND_TYPE.SFX_NONE;
	}
	//사격
	protected SOUND_TYPE GetPlaySoundType(PROJECTILE_TYPE type)
	{
		switch (type)
		{
			case PROJECTILE_TYPE.BULLET:
				return SOUND_TYPE.SFX_BULLETSHOOT;
			case PROJECTILE_TYPE.LASER:
				return SOUND_TYPE.SFX_LASERSHOOT;

			case PROJECTILE_TYPE.MISSILE:
			//	case SHOOT_TYPE.MISSILE_RIGHT:
			//case SHOOT_TYPE.MISSILE_BOTH:
				return SOUND_TYPE.SFX_MISSILESHOOT;
			//case SHOOT_TYPE.ALL://전체쏘는키를 구현할지...근데 그러면 소리를어케해야되나?그냥 다 누르면 다 재생되지않나
			//	break;
		}
		return SOUND_TYPE.SFX_NONE;

	}
}
