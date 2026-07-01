using System.Collections.Generic;
using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// NPC별 개별 호감도 보관. 대화/구매/재료전달/퀘스트완료 등 트리거에서
// AddAffection() 호출 — 실제 트리거 연동은 각 시스템 쪽에서 호출.
//
//   GetAffection(NPC_ID)              현재 호감도 조회. 등록 안 된 NPC면 0.
//   AddAffection(NPC_ID, int amount)   호감도 증가(음수면 감소). 0 미만으로는 안 내려감.
//   GetAllAffections()                저장(SaveData)용 전체 스냅샷.
//   LoadAffections(SavedAffection[])   불러오기(SaveData)용 일괄 복원.
//
//   예시) AffectionManager.Instance.AddAffection(NPC_ID.XXX, 1);
//
// ── 변경 통지 이벤트 (UI팀 구독용) ─────────────────────────────
//   OnAffectionChanged(NPC_ID, int)   호감도가 바뀔 때마다 (변경된 NPC, 변경 후 값)을 발행.
//     대화/퀘스트/상점 등 어떤 경로로 바뀌든 여기 한 곳으로 통지되므로,
//     UI는 이 이벤트만 구독하고 자기 관심 NPC일 때만 갱신하면 됨.
//     AddAffection / LoadAffections(항목별) 에서 발행.
//   구독 예시)
//     void OnEnable()  { AffectionManager.Instance.OnAffectionChanged += OnChanged; Refresh(); }
//     void OnDisable() { if (AffectionManager.Instance != null) AffectionManager.Instance.OnAffectionChanged -= OnChanged; }
//     void OnChanged(NPC_ID changed, int value) { if (changed == npc) Refresh(); }
// ================================================================

public class AffectionManager : MonoBehaviour
{
	private static AffectionManager instance = null;

	public static AffectionManager Instance
	{
		get
		{
			if (instance == null)
			{
				instance = FindObjectOfType<AffectionManager>();
				if (instance == null)
				{
					Debug.LogError("[AffectionManager] 씬에 AffectionManager 없음! 하이어라키에 추가 필요");
				}
				else
				{
					DontDestroyOnLoad(instance.gameObject);
				}
			}
			return instance;
		}

	}


	private void Awake()
	{
		if (instance == null)
		{
			instance = this;
			DontDestroyOnLoad(gameObject);
		}
		else if (instance != this)
		{
			Debug.LogWarning("[AffectionManager] 중복 감지. 파괴 후 기존 유지");
			Destroy(gameObject);
		}
	}



	private Dictionary<NPC_ID, int> _affectionByNpc = new Dictionary<NPC_ID, int>();

	// 호감도 변경 통지 이벤트. (변경된 NPC, 변경 후 값). 어떤 경로로 바뀌든 변경의 단일 소스 역할.
	// UI 등 구독자는 자기 관심 NPC일 때만 갱신하면 됨.
	public event System.Action<NPC_ID, int> OnAffectionChanged;

	/// <summary>현재 호감도 조회. 등록 안 된 NPC면 0.</summary>
	public int GetAffection(NPC_ID npc)
	{
		int value;
		if (_affectionByNpc.TryGetValue(npc, out value))
		{
			return value;
		}
		return 0;
	}

	/// <summary>호감도 증가(음수면 감소). 0 미만으로는 안 내려감.</summary>
	public void AddAffection(NPC_ID npc, int amount)
	{
		int current = GetAffection(npc);
		int next = Mathf.Max(0, current + amount);
		_affectionByNpc[npc] = next;
		OnAffectionChanged?.Invoke(npc, next);
	}

	/// <summary>저장(SaveData)용 전체 스냅샷.</summary>
	public Dictionary<NPC_ID, int> GetAllAffections()
	{
		return _affectionByNpc;
	}

	/// <summary>불러오기(SaveData)용 일괄 복원. 기존 데이터는 전부 덮어씀.</summary>
	public void LoadAffections(SavedAffection[] saved)
	{
		_affectionByNpc.Clear();
		if (saved == null)
		{
			return;
		}

		for (int i = 0; i < saved.Length; i++)
		{
			_affectionByNpc[saved[i].npc] = saved[i].value;
			// 로드로 복원된 값도 UI에 통지 (세이브 불러오기 직후 상태 UI 자동 갱신)
			OnAffectionChanged?.Invoke(saved[i].npc, saved[i].value);
		}
	}
}
