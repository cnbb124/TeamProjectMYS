public enum ENEMY_TYPE
{
	DROPSHIP,
	GUNSHIP,
	MISSLIESHIP,
	BOSS,
}

public enum DAMAGE_TYPE
{
	BULLET, //총알
	LASER, //레이저(스킬로 변경하거나 스킬을이걸로)
	EXPLOSION, //폭발형(미사일)
	CONTACT, //충돌뎀(빡치기)
}
public enum SHOOT_TYPE
{
	BULLET, //총알
	LASER, //레이저(스킬로 변경하거나 스킬을이걸로)
	MISSILE, //(미사일)
	
}

public enum UNIT_STATE
{
	IDLE,
	DIE,
	MOVING,
	DODGE,
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