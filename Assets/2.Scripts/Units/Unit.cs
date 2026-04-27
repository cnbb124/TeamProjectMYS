using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;



[RequireComponent(typeof(Rigidbody))] 
public abstract class Unit : MonoBehaviour,IDamageable
{
	

	//==================레퍼런스==================//
	protected SoundManager _soundManager;
	//==================유닛데이터==================//

	[Header("기본 스탯")]
	[Space(10)]
	[Header("HP")]
	public int maxHp; //최대,현재HP수치
	public int curHp;

	[Header("Shield - 피격후 일정딜레이 후 자동회복")]
	public int maxShield;//최대,현재실드수치
	public int curShield;
	public float shieldRegainDelay;//피격후 회복까지딜레이
	public float shieldRegainRate; //실드회복수치

	[Header("Armor - 자동회복x")]
	public int maxArmor;//최대,현재아머수치
	public int curArmor;

	[Tooltip("Armor보유시 데미지 경감되는 수치.")]
	public int defense;//아머 있을시 데미지 경감수치(damageAmount=damage-defense)



	[Header("Critical")]
	public float criChance;
	public float criDamageMultiplier;
	protected bool isCritical;//크리유무
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
	public float curBoostRemaining;//부스트잔량
	public float boostRegainDelay;//부스트 회복딜레이
	public float boostRegainRate;//초당 부스트 잔량회복수치
	[Tooltip("피격부위 혹은 HP잔량에 따른이동속도 변경용")]
	public float speedMultiPlier;//HP 혹은 피격부위에따른 속도조절용.

	public float viewDistance;
	public float lockOnDistance;

	[Header("이펙트 위치(총구,부스터등)")]
	public Transform bulletFirePos;
	public Transform missileFirePos;
	public Transform laserFirePos;
	public Transform boosterEffectPos;
	[HideInInspector]
	public Transform curFirePos;//밑에서 총구스위칭용

	[HideInInspector]
	public int playerLayer;
	[HideInInspector]
	public int enemyLayer;
	[HideInInspector]
	public int groundLayer;//행성등 지형지물, 차후 수정필요
	[HideInInspector]
	public int ItemLayer;//아이템레이어 추가필요
	[HideInInspector]
	public int playerProjectileLayer;
	[HideInInspector]
	public int enemyProjectileLayer;
	[HideInInspector]
	public SOUND_TYPE _playSoundType;

	protected virtual void Awake()
	{
		_soundManager = SoundManager.Instance;
	}

	// Start is called before the first frame update
	protected virtual void Start()
	{
		//인스펙터에서 입력된 값 현재 스탯으로 설정
		//저장 기능 생길시 변경필요.
		curHp = maxHp;
		curShield = maxShield;
		curArmor = maxArmor;
		curBoostRemaining = maxBoostRemaining;
		
		playerLayer=LayerMask.NameToLayer("UNIT_Player");
		enemyLayer=LayerMask.NameToLayer("UNIT_Enemy"); 
		groundLayer = LayerMask.NameToLayer("Environment");
		//아이템 레이어 추가필요ItemLayer = LayerMask.NameToLayer("");
		playerProjectileLayer = LayerMask.NameToLayer("PlayerProjectile");
		enemyProjectileLayer = LayerMask.NameToLayer("EnemyProjectile");
	}

	// Update is called once per frame
	protected virtual void Update()
	{

	}
	//(실드o,아머x)(Damageinfo.damage) * (크리시)criDamageMultiplier;
	//(실드x,아머o)Damageinfo.damage-defense *(크리시)criDamageMultiplier;
	//(실드x,아머x)Damageinfo.damage) * (크리시)criDamageMultiplier;
	//반올림할것. 0.5->1 0.4->0
	public virtual void Shoot(SHOOT_TYPE type)
	{
		//총쏘는타입별로
		switch(type)
		{
			//총구정해주고
			case SHOOT_TYPE.BULLET:
				curFirePos = bulletFirePos;
				break;
			case SHOOT_TYPE.LASER:
				curFirePos = laserFirePos;
				break;
			case SHOOT_TYPE.MISSILE:
				curFirePos = missileFirePos;
				break;
		}
		//사운드바꿔주고
		_playSoundType = GetPlaySoundType(type);
		//총구에서재생
		_soundManager.PlaySFX3DAtPosition(_playSoundType,curFirePos.position);
		//발사로직필요//
	}

	public virtual void TakeDamage(DamageInfo info)
	{
		//피격 애니메이션재생 필요
		//피격 사운드재생 필요
		_playSoundType = GetPlaySoundType(info);
		_soundManager.PlaySFX3DAtPosition(_playSoundType, info.hitPosition);
		//피격 카메라무빙필요
		//피격 데미지수치필요(실드있을시, 없을시)
		//크리면 데미지 배율, 아니면 그냥 데미지

		isCritical = Random.Range(0f, 100f) < criChance;
		int damageAmount =isCritical ? Mathf.RoundToInt(info.damage * criDamageMultiplier):info.damage;

		calculDamage(damageAmount);
		

		//피격 방향에 따른 이동(반동)피요
	}


	//bool isCritical()
	//{
	//	float rand = Random.Range(0f, 100f);//0~100퍼
	//	return rand < criChance;
	//}

	void calculDamage(int damageAmount)
	{
		if (curShield > 0)
		{
			int shieldDamage = Mathf.Min(curShield, damageAmount);//현지실드량보다 초과해서 -가되면 안됨
			curShield -= shieldDamage;//실드에 가해진 피해량만큼 현재실드량 깎기
			damageAmount -= shieldDamage;//실드에 가해진피해량만큼 데미지잔량도 깎기

		}
		if (damageAmount > 0 && curArmor > 0)//데미지잔량0초과,실드0,아머0초과
		{
			int reducedDamage = Mathf.Max(1, damageAmount - defense);//아머가몇이건 최소 1이건 데미지들어감
			int armorDamage = Mathf.Min(curArmor, reducedDamage);//아머로 경감한데미지만큼 현재아머량깎기 초과해서 -가되면안되므로
			curArmor -= armorDamage;//아머에 가해진피해량만큼깎기
			damageAmount -= armorDamage;//아머에 가해진 피해량만큼 데미지잔량도깎기

		}
		if (damageAmount > 0)
		{

			int hpDamage = Mathf.Min(curHp, damageAmount);
			curHp -= hpDamage;
		}

	}

	SOUND_TYPE GetPlaySoundType (DamageInfo info)
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
					return  SOUND_TYPE.SFX_CONTACTSHIP;
				}
				if(info.attacker.CompareTag("Ground"))
				{
					return SOUND_TYPE.SFX_CONTACTGROUND;
				}
			break;
		}
		return SOUND_TYPE.SFX_NONE;
	}
	SOUND_TYPE GetPlaySoundType(SHOOT_TYPE type)
	{
		switch (type)
		{
			case SHOOT_TYPE.BULLET:
				return SOUND_TYPE.SFX_BULLETSHOOT;
			case SHOOT_TYPE.LASER:
				return SOUND_TYPE.SFX_LASERSHOOT;

			case SHOOT_TYPE.MISSILE:
				return SOUND_TYPE.SFX_MISSILESHOOT;
		}
		return SOUND_TYPE.SFX_NONE;
		
	}
}
