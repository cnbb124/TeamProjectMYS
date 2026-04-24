using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Unit : MonoBehaviour, IDamageable
{
    public enum UNIT_STATE
    {
        IDLE,
        DIE,
        MOVING,

    }
    //==================데이터구역==================//
    
    public int maxHp; //최대,현재HP수치
	public int curHp;
	public int maxShield;//최대,현재실드수치
	public int curSheild;
	public int maxArmor;//최대,현재아머수치
	public int curArmor;
	
    public int defense;//아머경감수치

	public float baseMoveSpeed;//기본이동속ㄷ
	public float boostSpeed;//부스트시 이동속도
	public float maxSpeed;//최대속도velocity가 넘어갈시 고정시킬속도

	public float maxBoostRemaining;//최대,현재 부스트수치
	public float curBoostRemaining;//부스트잔량
    public float boostRegainRate;//초당 부스트 잔량회복수치
    public float speedMultiPlier;//HP 혹은 피격부위에따른 속도조절용.

    public float viewDistance;
    public float lockOnDistance;

    public float ciriticalChance;
    public float criticalDamage;





	// Start is called before the first frame update
	void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void TakeDamage(DamageInfo info)
    {

    }
}
