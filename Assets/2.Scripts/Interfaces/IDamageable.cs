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
}


public interface IDamageable
{
	//피격시 작동
    void TakeDamage(DamageInfo info);
	//체력 참고용
	int CurHp { get; }
}



