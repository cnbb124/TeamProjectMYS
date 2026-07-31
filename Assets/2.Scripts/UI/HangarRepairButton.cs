/*
 * [HangarRepairButton]
 * 격납고 Repair 버튼 — 즉시 수리 + 골드 비용 (깎인 HP 비례).
 *
 * [비용 공식]
 * 수리비 = (maxHp - curHp) × costPerHp   (costPerHp = 0이면 무료 수리)
 *
 * [동작]
 * - 풀피면 버튼 비활성 (누를 필요 없음)
 * - 골드 부족하면 실패 로그 (InventoryManager.SpendGold가 알아서 거절)
 * - 수리 성공 시 HP 완전 회복 + 비용 텍스트 갱신
 *
 * [부착 / 연결]
 * 1. Repair 버튼(또는 그 부모)에 부착
 * 2. repairButton  : Repair 버튼
 * 3. costText      : (선택) 수리비 표시 TMP — "REPAIR  120G" 식으로 갱신됨
 * 4. 버튼 OnClick은 비워둠 — Start에서 자동 연결
 * 플레이어는 GameManager.Instance.playerRef 자동 사용.
 */

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HangarRepairButton : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private Button repairButton;
	[SerializeField] private TMP_Text costText;      // (선택) 수리비 표시

	[Header("비용 설정")]
	[Tooltip("깎인 HP 1당 수리비 골드. 0이면 무료")]
	[SerializeField] private int costPerHp = 2;

	private void Start()
	{
		if (repairButton == null) repairButton = GetComponent<Button>();
		if (repairButton != null)
			repairButton.onClick.AddListener(OnRepairClicked);
	}

	private void OnEnable()
	{
		RefreshState();
	}

	private void Update()
	{
		// 격납고 열려있는 동안 상태 갱신 (HP/골드 변동 반영). UI 하나라 비용 미미
		RefreshState();
	}

	/// <summary>현재 수리비. 풀피면 0.</summary>
	public int GetRepairCost()
	{
		Player p = GetPlayer();
		if (p == null) return 0;
		int missing = Mathf.Max(0, p.maxHpRemaining - p.curHpRemaining);
		return missing * Mathf.Max(0, costPerHp);
	}

	private void OnRepairClicked()
	{
		Player p = GameManager.Instance.playerRef;

		if (p != null)
		{// Player p = GetPlayer();
		 //if (p == null) return;

			int missing = p.maxHpRemaining - p.curHpRemaining;
			if (missing <= 0) return; // 풀피 — 할 것 없음

			int cost = GetRepairCost();

			// 비용 지불 (부족하면 SpendGold가 false 반환하고 경고 로그 출력)
			if (cost > 0)
			{
				if (InventoryManager.Instance == null) return;
				if (!InventoryManager.Instance.SpendGold(cost)) return;
			}

			// 즉시 수리 — HP 완전 회복
			p.RefillToMax();
			// p.curHpRemaining = p.maxHpRemaining;
			Debug.Log($"[Repair] 수리 완료 (+{missing} HP, -{cost} G)");

			RefreshState();
		}
	}

	// 버튼 활성/비활성 + 비용 텍스트 갱신
	private void RefreshState()
	{
		Player p = GetPlayer();
		bool damaged = p != null && p.curHpRemaining < p.maxHpRemaining;

		if (repairButton != null)
			repairButton.interactable = damaged;

		if (costText != null)
		{
			if (!damaged) costText.text = "REPAIR";
			else if (costPerHp <= 0) costText.text = "REPAIR  FREE";
			else costText.text = $"REPAIR  {GetRepairCost():N0}G";
		}
	}

	private Player GetPlayer()
	{
		return GameManager.Instance != null ? GameManager.Instance.playerRef : null;
	}
}
