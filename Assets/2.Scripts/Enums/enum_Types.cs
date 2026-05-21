public enum LAYER_TYPE
{
    Default = 0,
    TransparentFX = 1,
    IgnoreRaycast = 2,
    None = 3,
    Water = 4,
    UI = 5,
    Unit_Player = 6,
    Unit_Enemy = 7,
    Projectile_Player = 8,
    Projectile_Enemy = 9,
    Environment = 10,
    //임시
    Trigger_DetectionRange = 11,
    Trigger_LockonRange = 12,
    HitBox_Player = 13,
    HitBox_Enemy = 14,
}


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
    BGM_1F,             // 1층 배경음
    BGM_B2,             // 지하2층배경음


    //UI관련
    SFX_DICE_ROLL,      // 주사위 굴리는 소리
    SFX_UI_CLICK,       // 버튼 클릭음
	SFX_UI_LOCKON_COMPLETE,

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

public enum LOCK_ON_MODE
{
	SINGLE, // 기존 1개 표적 선택 모드(호밍용)
	MULTI,   // 다중 표적 동시 락온 모드(클러스터용)
    NONE,//락온안됨(Dumb미사일용)
}

public enum MISSILE_TYPE
{
	HOMING,     // 기본 락온 추적 미사일
	CLUSTER,    // 분열 미사일 (멀티락온 후 분열)
	DUMB,       // 직선 무유도 미사일 (일정시간/충돌 후 광역폭발)
}



public enum SCENE_TYPE
{
    MAIN,
    STAGE1,
    STATION,
    GAME_OVER,
}
