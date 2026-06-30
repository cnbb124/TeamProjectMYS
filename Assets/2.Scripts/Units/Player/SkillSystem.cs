using System.Collections.Generic;
using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// WeaponSystem과 같은 자리에 위치.
// 이 캐릭터가 배운 스킬 전체(_ownedSkills) + 액티브 스킬 핫바(slots) + 사용을 전부 담당.
// 스킬 자체(Skill/ActiveSkill)는 MonoBehaviour가 아닌 순수 객체라 GameObject에 붙일 필요 없음.
// 매 프레임 Update()가 slots[]를 돌며 ActiveSkill.Tick()을 호출 — 코루틴 없이 Time.time 비교로
// 지속시간 있는 스킬(워프 채널링 등)을 처리하기 위함.
//
//   LearnSkill(SkillData)            스킬 배움. CanLearnSkill 통과 시 SkillData.CreateSkill()로
//                                     인스턴스 생성 → 보유목록 추가 → 액티브면 빈 슬롯에 자동 배치.
//   slots[i] / CurrentSlotIndex      슬롯 상태 (UI 참조용)
//   SwitchSlot() / UseCurrentSlot()  슬롯 전환 / 현재 슬롯 사용
//   GetCooldownRatio(int)            쿨다운 진행 비율(0~1). 게이지 UI용.
//   CollectSaveData() / LoadSaveData(SavedSkill[])  저장/로드용. GameManager가 호출.
//
// ▶ UI 갱신용 이벤트 — LearnSkill() 끝에서 자동 발행됨
//   public static event System.Action OnSkillSlotChanged;
//   예시)
//     void OnEnable()  { SkillSystem.OnSkillSlotChanged += RefreshSkillSlots; }
//     void OnDisable() { SkillSystem.OnSkillSlotChanged -= RefreshSkillSlots; }
// ================================================================

public class SkillSystem : MonoBehaviour
{
	private const int SLOT_COUNT = 3;

	public static event System.Action OnSkillSlotChanged;

	// ActiveSkill은 plain C# 클래스라 인스펙터에 표시 불가 — 직접 설정용 아님, 코드에서만 참조.
	public ActiveSkill[] slots = new ActiveSkill[SLOT_COUNT];

	[Header("현재 장착된 스킬 (디버그 표시용, 읽기전용)")]
	[SerializeField] private string[] equippedSkillNames = new string[SLOT_COUNT];

	[Header("시작부터 보유한 스킬 (테스트/기본 지급용)")]
	[Tooltip("Start() 시 전부 LearnSkill() 호출됨. 레벨업/상점 등 정식 트리거 생기면 그쪽에서 추가 호출.")]
	[SerializeField] private List<SkillData> startingSkills = new List<SkillData>();

	[Header("MYSSkill(미사일 연발) 전용 고정 발사위치")]
	[Tooltip("사일로 하드포인트 Transform. 장비 파츠(WeaponSystem 발사위치)와 무관한 고정 위치 — 유닛 프리팹에서 직접 연결.")]
	[SerializeField] private List<Transform> mysSiloPositions = new List<Transform>();

	public IReadOnlyList<Transform> MysSiloPositions
	{
		get
		{
			return mysSiloPositions;
		}
	}

	private List<Skill> _ownedSkills = new List<Skill>();
	private int _currentSlotIndex = 0;

	private Unit _unit;

	private void Awake()
	{
		_unit = GetComponent<Unit>();
	}

	private void Start()
	{
		for (int i = 0; i < startingSkills.Count; i++)
		{
			LearnSkill(startingSkills[i]);
		}
	}

	private void Update()
	{
		for (int i = 0; i < SLOT_COUNT; i++)
		{
			if (slots[i] != null)
			{
				slots[i].UpdateSkill();
			}
		}
	}

	public int CurrentSlotIndex
	{
		get
		{
			return _currentSlotIndex;
		}
	}

	/// <summary>스킬 배움. 레벨업/상점/호감도 등 트리거 쪽에서 호출.</summary>
	public void LearnSkill(SkillData data)
	{
		if (data == null || !data.CanLearnSkill(_unit))
		{
			return;
		}

		Skill newSkill = data.CreateSkill(_unit);
		_ownedSkills.Add(newSkill);

		ActiveSkill activeSkill = newSkill as ActiveSkill;
		if (activeSkill != null)
		{
			int freeIndex = FindFreeSlot();
			if (freeIndex >= 0)
			{
				slots[freeIndex] = activeSkill;
				equippedSkillNames[freeIndex] = activeSkill.GetType().Name;
			}
		}

		OnSkillSlotChanged?.Invoke();
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

	/// <summary>저장(SaveData)용 — 보유 스킬 전체 + 스킬슬롯 위치저장.</summary>
	public SavedSkill[] CollectSaveData()
	{
		SavedSkill[] result = new SavedSkill[_ownedSkills.Count];
		for (int i = 0; i < _ownedSkills.Count; i++)
		{
			Skill skill = _ownedSkills[i];
			result[i] = new SavedSkill
			{
				skillId   = (int)skill.SkillId,
				slotIndex = FindSlotIndexOf(skill as ActiveSkill)
			};
		}
		return result;
	}

	/// <summary>불러오기(SaveData)용 — 보유 스킬 전체 복원. 기존 보유/슬롯은 전부 덮어씀.</summary>
	public void LoadSaveData(SavedSkill[] saved)
	{
		_ownedSkills.Clear();
		for (int i = 0; i < SLOT_COUNT; i++)
		{
			slots[i] = null;
			equippedSkillNames[i] = null;
		}

		if (saved == null)
		{
			return;
		}

		for (int i = 0; i < saved.Length; i++)
		{
			SkillData data = SkillDatabase.Instance != null ? SkillDatabase.Instance.Get((SKILL_ID)saved[i].skillId) : null;
			if (data == null)
			{
				continue;
			}

			Skill newSkill = data.CreateSkill(_unit);
			_ownedSkills.Add(newSkill);

			ActiveSkill activeSkill = newSkill as ActiveSkill;
			if (activeSkill != null && IsValidIndex(saved[i].slotIndex))
			{
				slots[saved[i].slotIndex] = activeSkill;
				equippedSkillNames[saved[i].slotIndex] = activeSkill.GetType().Name;
			}
		}

		OnSkillSlotChanged?.Invoke();
	}

	private int FindSlotIndexOf(ActiveSkill skill)
	{
		if (skill == null)
		{
			return -1;
		}
		for (int i = 0; i < SLOT_COUNT; i++)
		{
			if (slots[i] == skill)
			{
				return i;
			}
		}
		return -1;
	}

	private int FindFreeSlot()
	{
		for (int i = 0; i < SLOT_COUNT; i++)
		{
			if (slots[i] == null)
			{
				return i;
			}
		}
		return -1;
	}

	private bool IsValidIndex(int index)
	{
		return index >= 0 && index < SLOT_COUNT;
	}
}
