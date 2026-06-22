using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 총알 전용
[CreateAssetMenu(fileName = "New Bullet Data", menuName = "Create Data/Item/Projectile Data/Bullet")]
public class BulletData : ProjectileData
{
	[Tooltip("피격(실드 없을 때) 시 재생할 사운드. SFX_NONE(미등록)이면 무음")]
	public SOUND_TYPE hitSoundType = SOUND_TYPE.SFX_NONE;

	[Header("<size=18>탄 설정</size>")]
	[Tooltip("총알 날아가는 속도")]
	public float speed;

	

	
}
