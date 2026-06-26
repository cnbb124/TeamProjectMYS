using UnityEngine;

// 패시브/액티브 공통 — 배움(언락) 조건만. 쿨다운/사용횟수 등 발동 관련 데이터는 ActiveSkillData에서.
public abstract class SkillData : ScriptableObject
{
	[Tooltip("스킬 배움에 필요한 레벨. 0 이하면 레벨 조건 없음.")]
	public int requiredLevel;

	[Tooltip("스킬 배움에 필요한 호감도. 0 이하면 호감도 조건 없음.\n" +
		"※ 호감도 시스템 자체가 아직 SaveData/Player에 없어서, 구현 전까지는 이 값 설정해도 항상 통과 처리됨.")]
	public int requiredAffinity;
}
