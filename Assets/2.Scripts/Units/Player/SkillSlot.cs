using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// QuickSlot.cs(소모품)와 동일한 패턴. 액티브 스킬 슬롯 보관 + 발동만 담당.
// 스킬을 "배우는"(언락) 흐름은 Skill.LearnSkill()/CanLearnSkill() 쪽 — 이 슬롯에 실제로
// 채워 넣는 연동(SkillManager 등)은 별도 미구현 항목. 현재는 Player가 자기 자식의
// ActiveSkill 컴포넌트를 찾은 순서대로 슬롯에 자동 등록.
//
//   AssignSlot(int, ActiveSkill)   슬롯에 스킬 등록
//   UseSlot(int)                   해당 슬롯 스킬 발동 시도. 쿨다운/사용횟수 부족이면 false.
//   SwitchSlot()                   현재 선택 슬롯 다음으로 순환
//   UseCurrentSlot()               현재 선택 슬롯 발동 시도
//   GetCooldownRatio(int)          쿨다운 진행 비율(0~1). UI 게이지용.
// ================================================================

public class SkillSlot : MonoBehaviour
{
	private const int SLOT_COUNT = 3;

	[Header("Slots (0~2)")]
	public ActiveSkill[] slots = new ActiveSkill[SLOT_COUNT];

	private int _currentSlotIndex = 0;

	public int CurrentSlotIndex
	{
		get
		{
			return _currentSlotIndex;
		}
	}

	public void AssignSlot(int slotIndex, ActiveSkill skill)
	{
		if (!IsValidIndex(slotIndex))
		{
			return;
		}
		slots[slotIndex] = skill;
	}

	public void SwitchSlot()
	{
		_currentSlotIndex = (_currentSlotIndex + 1) % SLOT_COUNT;
	}

	public bool UseCurrentSlot()
	{
		return UseSlot(_currentSlotIndex);
	}

	public bool UseSlot(int slotIndex)
	{
		if (!IsValidIndex(slotIndex))
		{
			return false;
		}
		if (slots[slotIndex] == null)
		{
			return false;
		}
		return slots[slotIndex].TryUseSkill();
	}

	public float GetCooldownRatio(int slotIndex)
	{
		if (!IsValidIndex(slotIndex) || slots[slotIndex] == null)
		{
			return 0f;
		}
		return slots[slotIndex].GetCooldownRatio();
	}

	private bool IsValidIndex(int index)
	{
		return index >= 0 && index < SLOT_COUNT;
	}
}
