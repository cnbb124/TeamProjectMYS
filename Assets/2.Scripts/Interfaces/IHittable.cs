using UnityEngine;
// 맞으면 피격 반응(VFX/사운드 등)을 하는 모든 대상.
// 데미지를 받지 않는 환경 오브젝트(소행성/벽 등)도 이것만 구현하면 자기 피격 반응을 가질 수 있음.
// 투사체는 대상이 유닛인지 환경인지 몰라도 이 인터페이스로 반응을 위임할 수 있음.
public interface IHittable
{
	void OnHitReaction(HitInfo info);
	// 이 대상이 직선 판정(시야)을 막는지. 레이저 관통과 락온 차폐가 이 값을 같이 씀.
	// 레이저: true면 데미지를 준 뒤 관통을 중단(뒤 대상 보호).
	// 락온  : true면 이 대상 뒤에 있는 적은 락온 후보에서 제외됨.
	// 벽/거대몹(보스·전함)/파괴가능 장애물 등. 일반 적은 false(관통됨).
	bool BlocksLineOfSight { get; }
}



//차후 투사체에 넣을 데미지 정보
public struct HitInfo
{
	public DAMAGE_TYPE type;//데미지타입
	public int damageAmount;//데미지수치
	public bool isCritical; //크리인지 데미지증가및 카메라 이동배율증가?
	public float critMultiplier; // 크리 시 곱할 배율 — '공격자'의 criDamageMultiplier를 실어 보냄(맞는 쪽 값 아님). 0(미지정)이면 ApplyHitDamage에서 1로 보정
	public Vector3 hitPosition; //맞은위치(이펙트 생성용) / AOE 시 폭발 중심
	public Vector3 hitDiriection; //맞은 방향(밀려나거나 하는용)
	public GameObject attacker; //누가 공격했는지
	public float aoeRadius; // 0 = 단발(가장 가까운 파츠 1개), >0 = AOE(반경 내 모든 파츠)
	public SOUND_TYPE hitSoundType; // 탄종(SO)에서 지정한 피격(실드 없을때) 사운드. SFX_NONE(미등록)이면 무음
									// 실드 피격음은 여기 없음 — Unit.GetPlaySoundTypeShield(HitInfo)에서 DAMAGE_TYPE 기준으로 따로 결정함
	public bool ignoreArmor; // 관통탄 여부. true면 defense(방어력) 경감만 무시 — 아머 HP 자체는 그대로 깎임
	public float shieldDamageMultiplier; // 실드를 깎는 양에만 적용되는 배율(통과 데미지는 원본 기준). 0(미지정)이면 Unit.calculTakeDamage에서 1로 보정
										 // 피격 VFX(총알류가 자기 데이터에서 채움). Unit.OnHitReaction이 실드 유무로 골라 재생. 미사일(EXPLOSION)은 Explode()가 대신 처리하므로 안 씀.
	public EFFECT_TYPE hitVfxType;       // 일반 피격(실드 없을 때) VFX
	public EFFECT_TYPE shieldHitVfxType; // 실드에 막혔을 때 VFX
}
