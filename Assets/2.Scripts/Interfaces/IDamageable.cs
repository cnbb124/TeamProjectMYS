
using UnityEngine;


public struct DamageInfo
{
	public DamageType type;//데미지타입
	public int damage;//데미지수치
	public bool isCiritical; //크리인지 데미지증가및 카메라 이동배율증가?
	public Vector3 hitPosition; //맞은위치(이펙트 생성용)
	public Vector3 hitDiriection; //맞은 방향(밀려나거나 하는용)
	public GameObject attacker; //누가 공격했는지
	
}

public enum DamageType
{
	Bullet,	//총알
	Laser, //레이저(스킬로 변경하거나 스킬을이걸로)
	Explosive, //폭발형(미사일)
	Collion //충돌뎀(빡치기)
}
public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}



