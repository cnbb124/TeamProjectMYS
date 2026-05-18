using UnityEngine;

//차후 투사체에 넣을 데미지 정보
public struct DamageInfo
{
	public DAMAGE_TYPE type;//데미지타입
	public int damageAmount;//데미지수치
	public bool isCritical; //크리인지 데미지증가및 카메라 이동배율증가?
	public Vector3 hitPosition; //맞은위치(이펙트 생성용)
	public Vector3 hitDiriection; //맞은 방향(밀려나거나 하는용)
	public GameObject attacker; //누가 공격했는지
	
}


public interface IDamageable
{
    void TakeDamage(DamageInfo info);
	int CurHp { get; }
}



