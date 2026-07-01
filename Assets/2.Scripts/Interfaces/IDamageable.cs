using UnityEngine;





// 데미지를 받는 대상. 피격 반응(IHittable) + 데미지 처리.
public interface IDamageable : IHittable
{
	//피격시 작동
    void TakeDamage(HitInfo info);
	//체력 참고용
	int CurHp { get; }

	//사용 x
	//int CurShiled { get; }
}



