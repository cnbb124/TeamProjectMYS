// 패시브 전용 데이터. 필드 미정 — 실제 패시브 스킬(ChainDodgeSkill 등) 구현 시 추가.
// SkillData.CreateInstance()를 아직 구현할 구체 패시브 스킬 클래스가 없어서 abstract로 유지
// (구현하기 전까진 이 데이터로 에셋을 만들어도 인스턴스화가 안 됨 — 의도된 제약).
public abstract class PassiveSkillData : SkillData
{

}
