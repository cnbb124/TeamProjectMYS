using UnityEngine;

//차후 투사체에 넣을 데미지 정보
public struct DamageInfo
{
	public DAMAGE_TYPE type;//데미지타입
	public int damageAmount;//데미지수치
	public bool isCritical; //크리인지 데미지증가및 카메라 이동배율증가?
	public Vector3 hitPosition; //맞은위치(이펙트 생성용) / AOE 시 폭발 중심
	public Vector3 hitDiriection; //맞은 방향(밀려나거나 하는용)
	public GameObject attacker; //누가 공격했는지
	public float aoeRadius; // 0 = 단발(가장 가까운 파츠 1개), >0 = AOE(반경 내 모든 파츠)
	public SOUND_TYPE hitSoundType; // 탄종(SO)에서 지정한 피격(실드 없을때) 사운드. SFX_NONE(미등록)이면 무음
	// 실드 피격음은 여기 없음 — Unit.GetPlaySoundTypeShield(DamageInfo)에서 DAMAGE_TYPE 기준으로 따로 결정함
	public bool ignoreArmor; // 관통탄 여부. true면 defense(방어력) 경감만 무시 — 아머 HP 자체는 그대로 깎임
	public float shieldDamageMultiplier; // 실드를 깎는 양에만 적용되는 배율(통과 데미지는 원본 기준). 0(미지정)이면 Unit.calculTakeDamage에서 1로 보정
}


public interface IDamageable
{
	//피격시 작동
    void TakeDamage(DamageInfo info);
	//체력 참고용
	int CurHp { get; }
	
	//사용 x
	//int CurShiled { get; }
}



