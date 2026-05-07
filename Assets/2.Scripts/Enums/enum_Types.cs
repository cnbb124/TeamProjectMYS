//적종류

public enum ENEMY_TYPE
{
	DROPSHIP,
	GUNSHIP,
	MISSLIESHIP,
	BOSS,
}
//피해종류
public enum DAMAGE_TYPE
{
	BULLET, //총알
	LASER, //레이저(스킬로 변경하거나 스킬을이걸로)
	EXPLOSION, //폭발형(미사일)
	CONTACT, //충돌뎀(빡치기)
}

//쏘는종류(투사체)
public enum PROJECTILE_TYPE
{
	BULLET, //총알
	LASER, //레이저(스킬로 변경하거나 스킬을이걸로)
	
	//(미사일)
	MISSILE,
	//MISSILE_BOTH,//현재미사용
	
	//전체동시
	ALL,//필요한가?
	
}

//총구종류
public enum FIREPOS_TYPE
{
	BULLET_LEFT,
	BULLET_RIGHT,
	MISSILE_LEFT,
	MISSILE_RIGHT,
	LASER,
}
public enum BOOSTPOS_TYPE
{
	LEFT,
	RIGHT,
	FRONT,
	BACK,
}

public enum SOUND_TYPE
{
	BGM_LOBBY,          // 정거장(상점) 배경음
	BGM_BATTLE,         // 우주 전투 배경음
						//UI관련
	SFX_DICE_ROLL,      // 주사위 굴리는 소리
	SFX_UI_CLICK,       // 버튼 클릭음
						// 발사음
	SFX_BULLETSHOOT,    //탄
	SFX_MISSILESHOOT,   //미사일
	SFX_LASERSHOOT,     //레이저
						// 피격음
	SFX_BULLETHIT,      //탄
	SFX_EXPLOSION,      // 미사일등 폭발음
	SFX_CONTACTSHIP,    //부딪혔을때.
	SFX_CONTACTGROUND,  //행성등 부딪혔을떄. 차후 필드명 수정할수있음
	SFX_LASERHIT,       //레이저


	SFX_NONE,//빈거설정용
}