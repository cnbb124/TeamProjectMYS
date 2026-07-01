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
		_affectionByNpc[npc] = Mathf.Max(0, current + amount);
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
		}
	}
}
