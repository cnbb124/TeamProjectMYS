using System.Collections;
using System.Collections.Generic;

using UnityEngine;


[System.Serializable]
public class FirePosEntry///총구 좌표 연결용
{
    public FIREPOS_TYPE type;
    public Transform pos;
}

[System.Serializable]
public class BoostPosEntry///부스터(추진기 쓰러스터)좌표 연결용
{
    public BOOSTPOS_TYPE type;
    public Transform pos;
}

[System.Serializable]
public class MissileAmmoInfo//미사일 잔탄확인용
{
	public MISSILE_TYPE missileType;
	public int curAmmo;
	public int maxAmmo;
}

[RequireComponent(typeof(Rigidbody))]
public abstract class Unit : MonoBehaviour, IDamageable
{

    
	//==================레퍼런스==================//


	//=============기타 레퍼런스===============
	//리지드바디 할당용 레퍼런스
	protected Rigidbody _rb;
	//매니저 할당용 레퍼런스
	protected SoundManager _sound;
	protected PoolManager _pool;

	//==================유닛데이터==================//

	[Header("<size=18>기본 스탯 설정창</size>")]

    [Header("HP")]
    public int maxHpRemaining; //최대,현재HP수치


    [Header("Shield - 피격후 일정딜레이 후 자동회복")]
    public int maxShieldRemaining;//최대,현재실드수치

    public float shieldRegainDelay;//피격후 회복까지딜레이시간
    public float shieldRegainRate; //실드회복수치
    //private float shieldRegainTimer = 0f;//딜레이 시간까지잴 타이머 >0516 코루틴으로변경
    public bool isShieldRegaining = false; //회복중인지 여부
    private Coroutine _shieldRegenCoroutine;//중간 정지등을 위한 코루틴변수 따로
    //실드연결용
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

	[Header("잔탄 시스템")]
	[Tooltip("인스펙터에서 각 미사일 종류별 잔탄/최대치를 설정.")]
	public List<MissileAmmoInfo> missileAmmoList = new List<MissileAmmoInfo>();

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



    [Header("이펙트 위치(총구,부스터등)")]
    public FirePosEntry[] firePositions; // 인스펙터에서 타입+Transform 쌍으로 등록
    public BoostPosEntry[] boosterEffectPositions;//옆무빙시 부스터이펙트 추가필요.enum에 타입등추가필요.left,right,역분사,정분사,부스트상태등
    private Dictionary<FIREPOS_TYPE, Transform> _firePosDict = new Dictionary<FIREPOS_TYPE, Transform>();
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

    [Header("현재 상태(입력x 참고용)")]
    public UNIT_STATE curState = UNIT_STATE.IDLE;
    public int curHpRemaining;
   	public int CurHp => curHpRemaining;//인터페이스 프로퍼티용
	public int curShieldRemaining;
    public int curArmorRemaining;
    public float curSpeed;
    public float curBoostRemaining;//부스트잔량
                                   //잔탄도추가예정

    private float updateTimer = 0f;

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
        //UpdateShieldRegen(); >>0516 코루틴으로변경
        UpdateBoostRegen();

        updateTimer += Time.deltaTime;
        if (updateTimer > 0.5f)
        {
            curSpeed = _rb.velocity.magnitude;
            updateTimer = 0f;
        }
    }

    protected virtual void FixedUpdate()
    {

    }


	// ================= 잔탄 관리 메서드 =================

	/// <summary>
	/// 특정 미사일의 잔탄이 남아있는지 확인용
	/// 플레이어나 적 유닛이 발사 버튼을 누르거나 공격 패턴을 시작할 때,
    /// 가장 먼저 호출하여 총알이 나갈 수 있는 상태인지 판별 
	/// </summary>
	public bool HasMissileAmmo(MISSILE_TYPE type)
	{
		// 리스트에서 해당 타입의 미사일 정보 탐색
		MissileAmmoInfo info = missileAmmoList.Find(x => x.missileType == type);
		// 정보가 존재하고, 잔탄이 0보다 크면 true 반환
		return info != null && info.curAmmo > 0;
	}

	/// <summary>
	/// 미사일 발사 시 잔탄 1 감소
	/// 발사 로직이 최종적으로 통과되어 투사체가 생성되는 시점에 호출,
    /// 실제 잔탄을 소비하게 만들기.
	/// </summary>
	public void RemoveMissileAmmo(MISSILE_TYPE type)
	{
		// 리스트에서 해당 타입의 미사일 정보 탐색
		MissileAmmoInfo info = missileAmmoList.Find(x => x.missileType == type);
		// 잔탄이 1발이라도 남아있을 경우에만 차감 진행 (음수 방지)
		if (info != null && info.curAmmo > 0)
		{
			info.curAmmo--;
		}
	}

	/// <summary>
	/// 미사일 잔탄 획득 (아이템 습득 등)
	/// 게임 플레이 도중 보급품이나 탄약 팩을 획득했을 때 호출
    /// 획득량(amount)을 기존 수치에 더하되, 결괏값이 최대 적재량을 초과하면 무조건 최대치에 맞춰지도록 처리
	/// </summary>
	public void AddMissileAmmo(MISSILE_TYPE type, int amount)
	{
		// 리스트에서 해당 타입의 미사일 정보 탐색
		MissileAmmoInfo info = missileAmmoList.Find(x => x.missileType == type);
		if (info != null)
		{
			// Mathf.Min을 사용해 획득 후 잔탄이 최대치(maxAmmo)를 넘지 않도록 제한
			info.curAmmo = Mathf.Min(info.curAmmo + amount, info.maxAmmo);
		}
	}


	/// <summary>
	/// 미사일 최대 적재량 증가 (레벨업, 장비 장착 등)
    /// 소지한도 상한선증가.
	/// </summary>
	public void IncreaseMaxMissileAmmo(MISSILE_TYPE type, int amount)
	{
		// 리스트에서 해당 타입의 미사일 정보 탐색
		MissileAmmoInfo info = missileAmmoList.Find(x => x.missileType == type);
		if (info != null)
		{
			// 최대치 한도 증가
			info.maxAmmo += amount;
			// 차후 최대치 증가 시 현재 잔탄도 같이 채워줄 수 있을지도?(필요시 주석 해제)
			//info.curAmmo += amount; 
		}
	}


	/// <summary>
	/// 미사일 최대 적재량 감소 (파손, 장비 해제 등)
	/// 소지한도 상한선감소.
	/// </summary>
	public void DecreaseMaxMissileAmmo(MISSILE_TYPE type, int amount)
	{
		// 리스트에서 해당 타입의 미사일 정보 탐색
		MissileAmmoInfo info = missileAmmoList.Find(x => x.missileType == type);
		if (info != null)
		{
			// 최대치 한도 증가
			info.maxAmmo -= amount;
			// 차후 최대치 감소 시 현재 잔탄도 같이 감소할 수 있을지도?(필요시 주석 해제)
			//info.curAmmo -= amount; 
		}
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
    protected IEnumerator ShieldRegenerationRoutine()
    {
        //피격 후 설정된 딜레이(초)만큼 대기합니다. (Update의 타이머 연산을 완벽히 대체)
        yield return new WaitForSeconds(shieldRegainDelay);

        isShieldRegaining = true;

        // 최적화를 위해 0.1초마다 대기할 캐싱 객체 생성
        WaitForSeconds tick = new WaitForSeconds(0.1f);

        //  실드가 꽉 차지 않았고, 유닛이 살아있는 동안 반복해서 회복
        while (curShieldRemaining < maxShieldRemaining && curState != UNIT_STATE.DIE)
        {
            // 초당 회복량(shieldRegainRate)을 0.1초 기준 단위로 계산하여 더함
            curShieldRemaining += Mathf.RoundToInt(shieldRegainRate * 0.1f);
            curShieldRemaining = Mathf.Min(curShieldRemaining, maxShieldRemaining);

            // 다음 0.1초까지 대기
            yield return tick;
        }

        // 회복이 완료되었거나 죽었을 경우 상태 초기화
        isShieldRegaining = false;
        _shieldRegenCoroutine = null;
    }

    //0516 실드회복 코루틴으로변겨ㅑㅇ
    //private void UpdateShieldRegen()
    //{
    //    if (curState == UNIT_STATE.DIE || curShieldRemaining >= maxShieldRemaining)
    //    {
    //        return;
    //    }
    //    //if(curShieldRemaning>=maxShieldRemaning)//디버그 로깅같은거 필요하면 주석풀고 위에서 지울것
    //    //{
    //    //	return;
    //    //}
    //    //타이머에 일정시간더해주고
    //    shieldRegainTimer += Time.deltaTime;
    //    //타이머가 딜레이보다 커졌고 충전중이아닐때, 즉 딜레이만큼시간지났을떄
    //    if (!isShieldRegaining && shieldRegainTimer >= shieldRegainDelay)
    //    {
    //        isShieldRegaining = true;
    //    }

    //    if (isShieldRegaining)
    //    {
    //        //반올림공식
    //        curShieldRemaining += Mathf.RoundToInt(shieldRegainRate * Time.deltaTime);
    //        //혹여나 초과시 제한걸도록 둘중 작은값 반환하는 함수(동일시 그값반환)
    //        curShieldRemaining = Mathf.Min(curShieldRemaining, maxShieldRemaining);
    //    }

    //}
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
        if (!isBoostRegaining && boostRegainTimer >= boostRegainDelay)
        {
            isBoostRegaining = true;
        }

        if (isBoostRegaining)
        {
            curBoostRemaining += boostRegainRate * Time.deltaTime;
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

    /// <summary>
    /// Unit TakeDamage(IDamageable 상속시 필수구현하는 메서드) 
    /// </summary>
    /// <param name="info"> 데미지정보구조체 받음</param>
    public virtual void TakeDamage(DamageInfo info)
    {

        //info.isCritical = Random.Range(0f, 100f) < criChance; //크리판정은 투사체에서 직접담당.
        int damageAmount = info.isCritical ? Mathf.RoundToInt(info.damageAmount * criDamageMultiplier) : info.damageAmount;
        //실드회복중지, 타이머 초기화
        //shieldRegainTimer = 0f; //0516 코루틴으로 변경
        isShieldRegaining = false;

        if (_shieldRegenCoroutine != null)
        {
            StopCoroutine(_shieldRegenCoroutine);
        }
        //피격 데미지수치필요(실드있을시, 없을시),실제로 데미지받음
        calculTakeDamage(damageAmount);


        //피격 방향에 따른 리액션(사운드,이펙트,카메라흔들림, 혹은 밀려남등)
        OnHitReaction(info);

        if (curHpRemaining <= 0)
        {
            CurState = UNIT_STATE.DIE;
            Die();
        }
        else
        {
            // 죽지 않았다면 딜레이 후 다시 실드가 차오르도록 코루틴을 새로 시작함
            _shieldRegenCoroutine = StartCoroutine(ShieldRegenerationRoutine());
        }

    }

    /// <summary>
    /// 피격 반동(카메라 쉐이크, 넉백등 자식에서 override)
    /// </summary>
    /// <param name="info"></param>
    protected virtual void OnHitReaction(DamageInfo info)
    {
        //피격 애니메이션재생 필요
        //피격 사운드재생 필요
        _playSoundType = GetPlaySoundType(info);
        _sound.PlaySFX3DAtPosition(_playSoundType, info.hitPosition);
        //피격 카메라무빙필요

        //크리면 데미지 배율, 아니면 그냥 데미지
        //데미지인포에서 총알인지 폭발인지 레이저인지에 따라서
    }




    /// <summary>
    /// 사망처리(오브젝트 풀반납, 비활성화등. 플레이어와는 다르게 처리할거기때문에 자식에서 override)
    /// </summary>
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
