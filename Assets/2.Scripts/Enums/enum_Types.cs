
//재생할 애니메이션 타입. 차후 유닛의 STATE에 따른 재생가능.
//필요한만큼 추가가능
//NONE을 0에넣을까?
public enum ANIM_TYPE
{
	IDLE,
    BOOST,
    DODGE,//무적시간및 롤
    SHOOT_BULLET,//좌우 총구가 앞뒤로 쏠대마다 밀리게
    SHOOT_MISSILE_L,//미사일베이열리게
    SHOOT_MISSILE_R,
    SHOOT_MISSILE_BOTH,
    SHOOT_LASER,//기모으는 파츠?
    MOVING,
    HIT,
    DIE
}


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
    THRUSTER_MAIN,
	THRUSTER_L_INNER,
	THRUSTER_L_OUTER,
	THRUSTER_R_INNER,
	THRUSTER_R_OUTER,
    REVERSAL,
    WING_L_TOP,
    WING_L_BOTTTOM,
    WING_R_TOP,
    WING_R_BOTTOM,
}

public enum SOUND_TYPE
{
    BGM_LOBBY,          // 정거장(상점) 배경음
    BGM_BATTLE,         // 우주 전투 배경음
    BGM_1F,             // 1층 배경음
    BGM_B2,             // 지하2층배경음
	BGM_MAIN,
	BGM_STAGE1,
	BGM_STATION,
	BGM_GAMEOVER,


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
    SFX_BULLETHIT_SHIELD,//실드 도탄
    SFX_EXPLOSION_SHIELD,//실드 폭발
	SFX_CONTACTSHIP_SHIELD,    //부딪혔을때. 실드
	SFX_CONTACTGROUND_SHIELD,  //행성등 부딪혔을 때, 실드
	SFX_LASERHIT_SHIELD,    //레이저맞았을때, 실드





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

// 미사일 발사 모드 
public enum MISSILE_FIRE_MODE
{
	DOUBLE, // 동시 발사
	SINGLE, // 교대 발사
}


//Build Settings의 씬 순서와 일치해야 함. LoadScene(SCENE_TYPE)오버로드가(int)sceneType으로 로드함.
// 현재 Build Settings 순서 확인 후 맞출것
public enum SCENE_TYPE
{
	MAIN = 0,
	STATION = 1,
	LOADING_SEQUENCE = 2,   // ← 추가 (로딩 시퀀스 씬)
	MAP_SELECT = 3,   // ← 추가 (맵 선택 화면)
	STAGE1 = 4,   // ← 기존 인덱스 밀릴 수 있음
	GAME_OVER = 5,   // ← 기존 인덱스 밀릴 수 있음
}
public enum INPUT_CONTROL_TYPE
{
    KEYBOARD_MOUSE,
    GAMEPAD,
    MOBILE,
}
public enum POOL_TYPE
{
    // 투사체 (DisableAllProjectiles 대상)
    BULLET,
    MISSILE,
    LASER,
    CLUSTER_MISSILE,
    DUMB_MISSILE,

    // 적
    ENEMY_GUNSHIP,
    ENEMY_DROPSHIP,
    ENEMY_MISSILESHIP,

    // 아이템 / 이펙트 (추후 세분화)
    ITEM,
    VFX,
}

public enum PART_TYPE
{
    ENGINE,//부스트게이지, 부스트게이지회복량,실드회복량
    FRAME,//기초 프레임. 무게, 착용장비가능종류? HP
    ARMOR,
    LAUNCHER_MISSILE,//동시 사출 증가관련
    LAUNCHER_BULLET,//탄속, 연사속도증가
    THRUSTER,//추진기, 부스트속도
}

public enum STAT_TYPE
{
    MAX_HP,
    MAX_SHIELD,
    MAX_ARMOR,
    DEFENSE,
    BASE_MOVE_SPEED,
    BOOST_SPEED,
    MAX_SPEED,
    MAX_BOOST,
    CRI_CHANCE,
    CRI_DAMAGE_MULT,
}

public enum CONSUMABLE_TYPE
{
    HP_RESTORE,
    SHIELD_RESTORE,
    BOOST_RESTORE,
}
