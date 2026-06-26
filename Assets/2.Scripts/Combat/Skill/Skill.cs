using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// 패시브/액티브 스킬 공통 베이스. 발동(쿨다운/사용횟수)이 있는 스킬은 ActiveSkill 상속.
//
// AddSkillToSkillSlot()		 스킬슬롯에 스킬 추가
// RemoveSkillFromSkillSlot()	 스킬슬롯에서 스킬 제거
// LearnSkill()					 캐릭터가 스킬 배움.(레벨업이나 상점언락등)
// CanLearnSkill()              스킬 배움 가능 여부 조회. LearnSkill() 호출 전 먼저 체크.
// skillData        배움(언락) 조건 데이터(SO). 같은 스킬 스크립트 + 다른 SkillData 에셋 = 변형 스킬.
// owner / SetOwner(Unit)        이 스킬을 쓰는 캐릭터. 스킬 인스턴스 생성/장착 시 호출자가 직접 주입.
//
// ▶ UI 갱신용 이벤트 — AddSkillToSkillSlot/RemoveSkillFromSKillSlot/LearnSkill 끝에서 자동 발행됨
//   public static event System.Action OnSkillSlotChanged;
//   static라 매니저 없이도 Skill.OnSkillSlotChanged로 바로 구독 가능.
//   UI팀 할 일: 스킬슬롯 UI Awake/OnEnable에서 구독, OnDisable/OnDestroy에서 구독 해제,
//              콜백 안에서 슬롯 아이콘 그리드 다시 그리기.
//   예시)
//     void OnEnable()  { Skill.OnSkillSlotChanged += RefreshSkillSlots; }
//     void OnDisable() { Skill.OnSkillSlotChanged -= RefreshSkillSlots; }
// ================================================================

public abstract class Skill : MonoBehaviour
{
	// 슬롯/배움 상태 변동 시 발행. UI팀이 구독해서 스킬슬롯 갱신용으로 사용.
	// delegate로 매개변수 받고 일정 슬롯만 바꿀수있게 할수도있으나 규모가 작아 선택하지않았음
	public static event System.Action OnSkillSlotChanged;

	[Header("<size=22>스킬 SO 연결</size>")]
	[SerializeField]
	protected SkillData skillData;

	// 이 스킬을 쓰는 캐릭터(Player/Enemy 공용). 스킬 효과가 캐스터 스탯/위치를 참조해야 할 때 사용.
	// Projectile.attacker와 동일한 이유로 GetComponent 자동탐색 대신 명시적으로 SetOwner()를 호출해서 주입.
	protected Unit owner;

	public void SetOwner(Unit unit)
	{
		owner = unit;
	}

	/// <summary>
	/// 스킬 슬롯에 더하기
	/// </summary>
	protected virtual void AddSkillToSkillSlot()
	{
		//스킬슬롯바뀌었을 때를 구독해둔애들을 전부실행
		OnSkillSlotChanged?.Invoke();
	}

	protected virtual void RemoveSkillFromSKillSlot()
	{
		OnSkillSlotChanged?.Invoke();
	}


	protected virtual void LearnSkill()
	{
		OnSkillSlotChanged?.Invoke();
	}

	/// <summary>
	/// 스킬 배움 가능 여부 조회 함수. 부작용 없음 — LearnSkill() 호출 전 먼저 체크.
	/// 레벨 조건만 실제 체크됨. 호감도는 시스템 자체가 아직 없어서 항상 통과.
	/// </summary>
	public virtual bool CanLearnSkill()
	{
		if (skillData.requiredLevel > 0)
		{
			Player player = owner as Player;
			if (player == null || player.level < skillData.requiredLevel)
			{
				return false;
			}
		}

		// 호감도 체크: SaveData/Player에 호감도 필드 자체가 아직 없음(메모리 기록 참고).
		// 호감도 시스템 구현되면 여기에 skillData.requiredAffinity 비교 추가.

		return true;
	}
}
