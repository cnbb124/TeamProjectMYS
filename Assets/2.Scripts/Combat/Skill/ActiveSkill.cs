using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// TryUseSkill()				 canUseSkill() 체크 + UseSkill() 실행을 한 번에. SkillSystem은 이거 하나만 호출.
// canUseSkill()			     쿨다운/사용횟수 다 됐는지 확인용 조회 함수. 부작용 없음.
// UseSkill()					 실제 스킬 발동 로직 + 쿨다운/사용횟수 갱신. 자식 클래스에서 override 시 base.UseSkill() 호출 권장.
//								 → TryUseSkill() 내부에서 canUseSkill() 통과 확인 후 호출됨
// StopSkill()					 스킬 중단/취소 로직. 주로 채널링 자식 클래스에서 override.
//								 → SkillSystem.OnDisable()이 슬롯 전체에 호출(유닛 사망/풀 반납 정리용)
// _activeSkillData              쿨다운/사용횟수 데이터. 생성자가 ActiveSkillData 타입을 직접 받아서
//                              캐스팅/null체크가 필요 없음(기존엔 skillData를 캐스팅하다가 잘못된 타입이면
//                              null이 되는 문제가 있었는데, 생성자 시그니처로 원천 차단됨).
// GetRemainingSkillCooldown()   남은 쿨다운(초). 숫자 텍스트 표시용.
// GetCooldownRatio()            쿨다운 진행 비율(0~1). Image.fillAmount 등 게이지 표시용.
// GetRemainingUseCount()        남은 사용 횟수. 무제한이면 -1.
// Tick()                        매 프레임 SkillSystem.Update()가 호출. 코루틴 대신 Time.time 비교로
//                              지속시간 있는 처리(워프 채널링 등)가 필요한 자식만 override.
// ================================================================

public abstract class ActiveSkill : Skill
{
	protected ActiveSkillData _activeSkillData;

	protected ActiveSkill(Unit owner, ActiveSkillData skillData) : base(owner, skillData)
	{
		_activeSkillData = skillData;
	}

	// 멀티 복제 발동(PlayRemote)로 켜진 '남의 연출용' 인스턴스는 false → 실제 데미지/이동은 안 하고 연출만.
	// 로컬(내 소유) 발동은 true 유지. WeaponSystem/Projectile의 _hasDamageAuthority와 같은 개념.
	protected bool _hasDamageAuthority = true;

	private float _lastSkillUseTime;

	// activeSkillData.maxUseCount로 매번 초기화. 0 이하(횟수 무제한)면 사용 안 함.
	private int _remainingUseCount;
	private bool _useCountInitialized;

	private void EnsureUseCountInitialized()
	{
		if (_useCountInitialized)
		{
			return;
		}
		_useCountInitialized = true;
		_remainingUseCount = _activeSkillData.maxUseCount;
	}

	protected virtual bool canUseSkill()
	{
		// 현재시간이 마지막사용시간+쿨다운을 아직 안 넘었으면(쿨다운이 안 끝났으면) 취소
		if (Time.time < _lastSkillUseTime + _activeSkillData.skillCoolDown)
		{
			return false;
		}

		// 사용횟수 제한이 있는 스킬인데 다 떨어졌으면 취소
		EnsureUseCountInitialized();
		if (_activeSkillData.maxUseCount > 0 && _remainingUseCount <= 0)
		{
			return false;
		}

		return true;
	}

	/// <summary>
	/// canUseSkill() 체크 후 통과하면 UseSkill() 실행. SkillSystem은 이 함수 하나만 호출하면 됨.
	/// </summary>
	public bool TryUseSkill()
	{
		if (!canUseSkill())
		{
			return false;
		}

		UseSkill();
		return true;
	}

	/// <summary>
	/// 멀티 원격 복제 발동. 쿨다운/사용횟수 체크 없이(남 클라의 연출 재현) UseSkill을 그대로 돌리되,
	/// 데미지 권위를 끈다 → 스폰되는 투사체/판정은 연출만 하고 실제 데미지는 소유자 클라에서만.
	/// SkillSystem.RpcUseSkill이 호출.
	/// </summary>
	public void PlayRemote()
	{
		_hasDamageAuthority = false;
		UseSkill();
	}

	/// <summary>
	/// 스킬사용 추가적인 스킬 로직은 자식 클래스에서 override
	/// </summary>
	protected virtual void UseSkill()
	{
		_lastSkillUseTime = Time.time;

		EnsureUseCountInitialized();
		if (_activeSkillData.maxUseCount > 0)
		{
			_remainingUseCount--;
		}
		// 추가적인 스킬 로직은 자식 클래스에서 override
	}

	/// <summary>
	/// 스킬 정지(멈춰야하는 채널링 스킬일때).
	/// SkillSystem.OnDisable()이 슬롯 전체에 대해 호출함 — 유닛이 죽거나 풀로 반납될 때
	/// 루프 사운드/빔 같은 진행 중인 연출이 남지 않게 하기 위함.
	/// 언제 불려도 안전하도록(진행 중이 아닐 때 포함) 구현할 것.
	/// </summary>
	public virtual void StopSkill()
	{

	}

	/// <summary>
	/// 매 프레임 SkillSystem.Update()가 호출. 코루틴 없이 Time.time 비교로 지속시간 처리가
	/// 필요한 자식만 override(워프 채널링 등). 기본은 아무것도 안 함.
	/// </summary>
	public virtual void UpdateSkill()
	{

	}

	/// <summary>남은 쿨다운(초). 다 됐으면 0. 숫자 텍스트 표시용.</summary>
	public virtual float GetRemainingSkillCooldown()
	{
		// 음수면 0, 아니면 쿨다운 남은 시간 반환. 마지막 사용시간 + 쿨다운-현재시간=남은쿨다운. ex 10초에 사용+쿨다운5초==15초 - 현재시간 12초==남은쿨3초.
		// 사용안해서 음수면 0반환
		return Mathf.Max(0f, _lastSkillUseTime + _activeSkillData.skillCoolDown - Time.time);
	}

	/// <summary>쿨다운 진행 비율(0~1). 0=막 사용함, 1=다 됐음. Image.fillAmount에 그대로 사용.</summary>
	public virtual float GetCooldownRatio()
	{
		if (_activeSkillData.skillCoolDown <= 0f)
		{
			return 1f;
		}
		return 1f - (GetRemainingSkillCooldown() / _activeSkillData.skillCoolDown);
	}

	/// <summary>남은 사용 횟수. maxUseCount가 0 이하(무제한)면 -1 반환.</summary>
	public virtual int GetRemainingUseCount()
	{
		if (_activeSkillData.maxUseCount <= 0)
		{
			return -1;
		}
		EnsureUseCountInitialized();
		return _remainingUseCount;
	}
}
