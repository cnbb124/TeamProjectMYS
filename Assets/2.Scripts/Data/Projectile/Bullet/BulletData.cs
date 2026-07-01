using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 총알 전용
[CreateAssetMenu(fileName = "New Bullet Data", menuName = "Create Data/Item/Projectile Data/Bullet")]
public class BulletData : ProjectileData
{
	[Tooltip("피격(실드 없을 때) 시 재생할 사운드. SFX_NONE(미등록)이면 무음")]
	public SOUND_TYPE hitSoundType = SOUND_TYPE.SFX_NONE;

	[Header("<size=18>피격 이펙트</size>")]
	[Tooltip("일반 피격(실드 없을 때) 시 재생할 VFX")]
	public EFFECT_TYPE hitEffectType = EFFECT_TYPE.VFX_BULLET_HIT;
	[Tooltip("실드에 막혔을 때 재생할 VFX")]
	public EFFECT_TYPE shieldHitEffectType = EFFECT_TYPE.VFX_BULLET_HIT_SHIELD;

	[Header("<size=18>탄 설정</size>")]
	[Tooltip("총알 날아가는 속도")]
	public float speed;

	

	
}
