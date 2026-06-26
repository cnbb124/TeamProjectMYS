using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// TryUseSkill()				 canUseSkill() 체크 + UseSkill() 실행을 한 번에. 슬롯/입력처리 쪽에서는 이거 하나만 호출.
// canUseSkill()			     쿨다운/사용횟수 다 됐는지 확인용 조회 함수. 부작용 없음.
//								 → 스킬 발동 전 자식 클래스/입력처리 쪽에서 먼저 체크
// UseSkill()					 실제 스킬 발동 로직 + 쿨다운/사용횟수 갱신. 자식 클래스에서 override 시 base.UseSkill() 호출 권장.
//								 → TryUseSkill() 내부에서 canUseSkill() 통과 확인 후 호출됨
// StopSkill()					 스킬 중단/취소 로직. 주로 채널링 자식 클래스에서 override.
// activeSkillData              skillData를 ActiveSkillData로 캐스팅한 접근자. 쿨다운/사용횟수 데이터.
// GetRemainingSkillCooldown()   남은 쿨다운(초). 숫자 텍스트 표시용.
// GetCooldownRatio()            쿨다운 진행 비율(0~1). Image.fillAmount 등 게이지 표시용.
// GetRemainingUseCount()        남은 사용 횟수. 무제한이면 -1.
// ================================================================

public abstract class ActiveSkill : Skill
{
	// 베이스의 skillData(SkillData 타입)를 ActiveSkillData로 캐스팅해서 쓰는 접근자.
	// 인스펙터에는 Skill.skillData 한 칸만 보이고, 거기에 실제로는 ActiveSkillData 서브클래스 에셋을 연결하면 됨.
	protected ActiveSkillData activeSkillData
	{
		get
		{
			return skillData as ActiveSkillData;
		}
	}

	// skillData 칸엔 SkillData를 상속한 거면 뭐든 끼울 수 있어서(PassiveSkillData도 들어감),
	// 잘못 끼우면 activeSkillData가 null이 되고 나중에 canUseSkill() 등에서 NullReferenceException으로 터짐.
	// 여기서 미리 걸러서 원인 알기 쉬운 에러로 남김. 자식에서 Awake() override 시 base.Awake() 호출 필요.
	protected virtual void Awake()
	{
		if (activeSkillData == null)
		{
			Debug.LogError($"[{name}] skillData가 ActiveSkillData(또는 그 자식)가 아님! 인스펙터에서 다시 연결 필요.");
		}
	}

	private float lastSkillUseTime;

	// activeSkillData.maxUseCount로 매번 초기화. 0 이하(횟수 무제한)면 사용 안 함.
	private int remainingUseCount;
	private bool useCountInitialized;

	private void EnsureUseCountInitialized()
	{
		if (useCountInitialized)
		{
			return;
		}
		useCountInitialized = true;
		remainingUseCount = activeSkillData.maxUseCount;
	}

	protected virtual bool canUseSkill()
	{
		// 현재시간이 마지막사용시간+쿨다운을 아직 안 넘었으면(쿨다운이 안 끝났으면) 취소
		if (Time.time < lastSkillUseTime + activeSkillData.skillCoolDown)
		{
			return false;
		}

		// 사용횟수 제한이 있는 스킬인데 다 떨어졌으면 취소
		EnsureUseCountInitialized();
		if (activeSkillData.maxUseCount > 0 && remainingUseCount <= 0)
		{
			return false;
		}

		return true;
	}

	/// <summary>
	/// canUseSkill() 체크 후 통과하면 UseSkill() 실행. 슬롯/입력처리 쪽에서는 이 함수 하나만 호출하면 됨.
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
	/// 스킬사용
	/// </summary>
	protected virtual void UseSkill()
	{
		lastSkillUseTime = Time.time;

		EnsureUseCountInitialized();
		if (activeSkillData.maxUseCount > 0)
		{
			remainingUseCount--;
		}
		// 추가적인 스킬 로직은 자식 클래스에서 override
	}

	/// <summary>
	/// 스킬 정지(멈춰야하는 채널링 스킬일때)
	/// </summary>
	protected virtual void StopSkill()
	{

	}

	/// <summary>남은 쿨다운(초). 다 됐으면 0. 숫자 텍스트 표시용.</summary>
	public virtual float GetRemainingSkillCooldown()
	{
		// 음수면 0, 아니면 쿨다운 남은 시간 반환. 마지막 사용시간 + 쿨다운-현재시간=남은쿨다운. ex 10초에 사용+쿨다운5초==15초 - 현재시간 12초==남은쿨3초.
		// 사용안해서 음수면 0반환
		return Mathf.Max(0f, lastSkillUseTime + activeSkillData.skillCoolDown - Time.time);
	}

	/// <summary>쿨다운 진행 비율(0~1). 0=막 사용함, 1=다 됐음. Image.fillAmount에 그대로 사용.</summary>
	public virtual float GetCooldownRatio()
	{
		if (activeSkillData.skillCoolDown <= 0f)
		{
			return 1f;
		}
		return 1f - (GetRemainingSkillCooldown() / activeSkillData.skillCoolDown);
	}

	/// <summary>남은 사용 횟수. maxUseCount가 0 이하(무제한)면 -1 반환.</summary>
	public virtual int GetRemainingUseCount()
	{
		if (activeSkillData.maxUseCount <= 0)
		{
			return -1;
		}
		EnsureUseCountInitialized();
		return remainingUseCount;
	}
}
