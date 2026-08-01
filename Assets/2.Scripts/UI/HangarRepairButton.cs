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

	// 직전에 그린 상태. 같은 값이면 UI를 다시 안 그림.
	private int _lastCost = -1;
	private bool? _lastDamaged;

	private void Start()
	{
		if (repairButton == null) repairButton = GetComponent<Button>();
		if (repairButton != null)
			repairButton.onClick.AddListener(OnRepairClicked);
	}

	private void OnEnable()
	{
		// 캐시를 무효화해 켜지는 즉시 한 번은 실제로 그리게 함.
		_lastCost = -1;
		_lastDamaged = null;
		RefreshState();
	}

	private void Update()
	{
		// 격납고 열려있는 동안 상태 갱신 (HP/골드 변동 반영).
		// 값이 안 바뀌면 UI를 건드리지 않음 — 매 프레임 문자열을 새로 만들지 않기 위함.
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
		Player p = GetPlayer();

		if (p != null)
		{
			if (!IsRepairAllowed()) return;

			int missing = p.maxHpRemaining - p.curHpRemaining;
			// HP가 멀쩡해도 실드·아머·부스트·연료·파츠가 깎였으면 수리 대상임.
			if (!IsDamaged(p)) return; // 전부 만땅 — 할 것 없음

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

	// 버튼 활성/비활성 + 비용 텍스트 갱신.
	// 직전 값과 같으면 아무것도 안 함 — Update에서 매 프레임 불려도 문자열 생성이 안 일어나게 함.
	private void RefreshState()
	{
		Player p = GetPlayer();
		bool damaged = p != null && IsRepairAllowed() && IsDamaged(p);
		int cost = GetRepairCost();

		if (_lastDamaged == damaged && _lastCost == cost)
		{
			return;
		}
		_lastDamaged = damaged;
		_lastCost = cost;

		if (repairButton != null)
		{
			repairButton.interactable = damaged;
		}

		if (costText != null)
		{
			if (!damaged)
			{
				costText.text = "REPAIR";
			}
			else if (cost <= 0)
			{
				costText.text = "REPAIR  FREE";
			}
			else
			{
				costText.text = $"REPAIR  {cost:N0}G";
			}
		}
	}

	// 수리는 스테이지를 깨고 돌아왔을 때만. 사망 후 재시작은 마지막 저장으로 복구되므로 대상이 아님.
	private bool IsRepairAllowed()
	{
		return GameManager.Instance != null && GameManager.Instance.StageClearedBeforeHangar;
	}

	// 수리할 게 하나라도 있는지. RefillToMax가 되돌리는 값들을 그대로 따라감 —
	// HP만 보면 연료·실드·파츠만 깎인 상태에서 버튼이 꺼져 아무것도 못 고치게 됨.
	private bool IsDamaged(Player p)
	{
		if (p.curHpRemaining < p.maxHpRemaining) return true;
		if (p.curShieldRemaining < p.maxShieldCapacity) return true;
		if (p.curArmorRemaining < p.maxArmor) return true;
		if (p.curBoostRemaining < p.maxBoostCapacity) return true;
		if (p.curFuelRemaining < p.maxFuelCapacity) return true;

		UnitParts parts = p.GetComponent<UnitParts>();
		return parts != null && parts.HasDamagedPart();
	}

	private Player GetPlayer()
	{
		return GameManager.Instance != null ? GameManager.Instance.playerRef : null;
	}
}
