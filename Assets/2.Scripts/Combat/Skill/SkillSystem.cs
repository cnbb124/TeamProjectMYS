using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// WeaponSystem과 같은 자리에 위치.
// 이 캐릭터가 배운 스킬 전체(_ownedSkills) + 액티브 스킬 핫바(slots) + 사용을 전부 담당.
// 스킬 자체(Skill/ActiveSkill)는 MonoBehaviour가 아닌 순수 객체라 GameObject에 붙일 필요 없음.
// 매 프레임 Update()가 slots[]를 돌며 ActiveSkill.UpdateSkill()을 호출 — 코루틴 없이 Time.time 비교로
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
	[SerializeField]
	private string[] _equippedSkillNames = new string[SLOT_COUNT];

	[Header("시작부터 보유한 스킬 (테스트/기본 지급용)")]
	[Tooltip("Start() 시 전부 LearnSkill() 호출됨. 레벨업/상점 등 정식 트리거 생기면 그쪽에서 추가 호출.")]
	[SerializeField]
	private List<SkillData> _startingSkills = new List<SkillData>();

	[Header("스킬용 Positions")]
	[Tooltip("사일로 발사위치 Transform(좌표 전용, 컴포넌트 불필요). 장비 파츠(WeaponSystem 발사위치)와 무관한 고정 위치 — 유닛 프리팹에서 직접 연결.")]
	[SerializeField]
	private List<Transform> _mysSiloPositions = new List<Transform>();
	[SerializeField]
	private Transform _laserFirePosition;
	[SerializeField]
	private Transform _laserChargePosition;
	// 인스펙터 수동연결 아님 — Start()에서 GetComponentInChildren로 자동 탐색.
	[SerializeField]
	[Tooltip("스킬전용 애니메이터")]
	private AnimCtrl _skillAnimCtrl;

	public IReadOnlyList<Transform> MysSiloPositions
	{
		get
		{
			return _mysSiloPositions;
		}
	}

	public Transform LaserFirePosition
	{
		get
		{
			return _laserFirePosition;
		}
	}

	public Transform LaserChargePosition
	{
		get
		{
			return _laserChargePosition;
		}
	}

	public AnimCtrl SkillAnimCtrl
	{
		get
		{
			return _skillAnimCtrl;
		}
	}

	private List<Skill> _ownedSkills = new List<Skill>();
	private int _currentSlotIndex = 0;

	private Unit _unit;
	// 멀티 복제 전파용. PhotonView 없으면(싱글) null → 로컬 발동만. Unit 루트의 PhotonView를 공유.
	private PhotonView _photonView;

	private void Awake()
	{
		_unit = GetComponent<Unit>();
		_photonView = GetComponent<PhotonView>();
	}

	private void Start()
	{
		
		for (int i = 0; i < _startingSkills.Count; i++)
		{
			LearnSkill(_startingSkills[i]);
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

	// 유닛이 죽거나 풀로 반납되면 Update가 멈춰서 채널링 스킬이 진행 중인 채로 얼어붙음 —
	// 루프 사운드/빔 같은 연출이 정지 호출을 못 받고 그대로 남으므로 여기서 전부 중단시킴.
	private void OnDisable()
	{
		for (int i = 0; i < SLOT_COUNT; i++)
		{
			if (slots[i] != null)
			{
				slots[i].StopSkill();
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
				_equippedSkillNames[freeIndex] = activeSkill.GetType().Name;
			}
		}

		OnSkillSlotChanged?.Invoke();
	}

	public void SwitchSlot()
	{
		_currentSlotIndex = (_currentSlotIndex + 1) % SLOT_COUNT;
		Debug.Log("[SkillSystem] Skill slot -> " + _currentSlotIndex + " : " + (slots[_currentSlotIndex] != null ? _equippedSkillNames[_currentSlotIndex] : "없음"));
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

		bool used = slots[slotIndex].TryUseSkill();

		// 로컬(소유자)이 실제로 발동했으면 남 클라에 복제 전파(연출용). WeaponSystem.RpcShoot와 동일 패턴.
		// 슬롯 인덱스가 아니라 '무슨 스킬인지'(SKILL_ID)를 보냄 — 받는 쪽 슬롯 배치가 쏜 사람과 같다는
		// 보장이 없어서(스킬을 배우는 순서/구성이 달라지면 어긋남) 인덱스로 보내면 엉뚱한 스킬이 재생됨.
		// 싱글(PhotonView 없음)이거나 룸 밖이면 전파 안 함.
		if (used && _photonView != null && _photonView.IsMine && PhotonNetwork.InRoom)
		{
			_photonView.RPC(nameof(RpcUseSkill), RpcTarget.Others, (int)slots[slotIndex].SkillId);
		}

		return used;
	}

	// 남 클라에서 수신 — 쿨다운 체크 없이 그 스킬을 연출용으로 복제 발동(데미지 권위 없음).
	// 내 슬롯 배치와 무관하게 ID로 찾으므로 쏜 사람이 쓴 스킬 그대로 재생됨.
	[PunRPC]
	private void RpcUseSkill(int skillId)
	{
		// 쓴 사람이 나와 다른 씬에 있으면 무시(WeaponSystem.RpcShoot과 동일).
		// Photon은 씬을 모르고 방 전체에 뿌리므로, 이게 없으면 스테이지에서 쓴 스킬 연출이
		// 로비/대기실 화면에도 재생됨.
		if (PlayerSceneVisibility.IsDifferentFromLocalScene(
				PlayerSceneVisibility.GetPlayerScene(_photonView != null ? _photonView.Owner : null)))
		{
			return;
		}

		ActiveSkill skill = FindSlotSkillById((SKILL_ID)skillId);
		if (skill == null)
		{
			return;
		}
		skill.PlayRemote();
	}

	// 슬롯에 꽂힌 스킬 중 해당 ID를 가진 것을 찾음. 없으면 null(그 스킬을 안 배운 복제본).
	private ActiveSkill FindSlotSkillById(SKILL_ID skillId)
	{
		if (skillId == SKILL_ID.NONE)
		{
			return null;
		}

		for (int i = 0; i < SLOT_COUNT; i++)
		{
			if (slots[i] != null && slots[i].SkillId == skillId)
			{
				return slots[i];
			}
		}
		return null;
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
				skillId   = skill.SkillId,
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
			_equippedSkillNames[i] = null;
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
				_equippedSkillNames[saved[i].slotIndex] = activeSkill.GetType().Name;
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
