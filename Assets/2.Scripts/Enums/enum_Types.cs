
//재생할 애니메이션 타입. 차후 유닛의 STATE에 따른 재생가능.
//필요한만큼 추가가능
//NONE을 0에넣을까?
public enum ANIM_TYPE
{
	IDLE = 0,
	MOVING,
	BOOST,
	//DODGE_N,  // 무적시간 및 롤, 좌우키입력없을때는 좌우랜덤으로 재생되게 코드에서 
	DODGE_L,  // 좌입력
	DODGE_R,  // 우입력
	HIT,
	DIE,
	// 발사 관련 애니는 파츠 프리팹의 LauncherAnim에서 처리

	SKILL_MYS_IDLE = 100,
	SKILL_MYS_OPEN,
	SKILL_MYS_CLOSE,
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
	LockOnBox = 12,
	HitBox = 13,
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

// 파츠 프리팹 내 발사/이펙트 위치 오브젝트에 붙이는 WeaponFirePos 컴포넌트의 타입 구분
// Unit.firePositions[] / FIREPOS_TYPE / BOOSTPOS_TYPE 제거 — 모두 파츠 프리팹으로 동적 관리
public enum WEAPON_POS_TYPE
{
	BULLET,   // 총알 총구 — LAUNCHER_BULLET 파츠
	MISSILE,  // 미사일 총구 — LAUNCHER_MISSILE 파츠
	LASER,    // 레이저 총구 — LAUNCHER_LASER 파츠

}

public enum SOUND_TYPE
{
	//필요한 사운드는 규칙에 맞춰 추가후 매칭시켜주세요. 이름 변경원하면 변경하시되 해당 코드찾아서 수정해야함.

	//BGM관련 100번대
	BGM_LOBBY = 100,          // 정거장(상점) 배경음
	BGM_BATTLE,         // 우주 전투 배경음
	BGM_1F,             // 1층 배경음
	BGM_B2,             // 지하2층배경음
	BGM_MAIN,           // 메인화면
	BGM_STAGE1,// 스테이지1 현재로선 배틀맵이 하나이므로 배틀맵에 사용
	BGM_STAGE2,
	BGM_STATION,        // 스테이션
	BGM_GAMEOVER,       // 게임오버
	BGM_BOSS,           // 보스전(보스 등장 시 전환)
	BGM_MAP_SELECT,

	//UI관련 200번대

	SFX_DICE_ROLL = 200,      // 주사위 굴리는 소리
	SFX_UI_CLICK,       // 버튼 클릭음
	SFX_UI_LOCKON_COMPLETE, // 락온 완료 사운드
	SFX_UI_LOCKON_ALERT,    // 락온 경고음


	//전투 효과음관련
	//발사음 300번대
	SFX_BULLET_VULCAN_SHOOT = 300,
	SFX_BULLET_IMPULSE_SHOOT,//탄
	SFX_MISSILESHOOT,   //미사일
	SFX_LASERSHOOT,     //레이저

	//피격음 400번대
	SFX_BULLET_VULCAN_HIT = 400,
	SFX_BULLET_IMPULSE_HIT,//탄
	SFX_EXPLOSION,      // 미사일등 폭발음
	SFX_CONTACTSHIP,    // 유닛끼리 부딪혔을때.
	SFX_CONTACTGROUND,  //유닛이 행성등에 부딪혔을떄. 차후 필드명 수정할수있음
						//SFX_LASERHIT,       //레거시 사용안함
	SFX_BULLETHIT_SHIELD = 406,//실드 도탄
	SFX_EXPLOSION_SHIELD,//실드 폭발

	SFX_CONTACTSHIP_SHIELD,    //유닛끼리부딪혔을때. 실드
	SFX_CONTACTGROUND_SHIELD,  //유닛이 행성등에 부딪혔을 때, 실드
							   //SFX_LASERHIT_SHIELD,    //레거시 사용안함
	SFX_SHIELD_DESTROY = 411,   // 실드 파괴시

	//이동음 500번대
	SFX_IDLE = 500,
	SFX_MOVING,
	SFX_BOOST,
	SFX_DODGE,


	//스킬음 600번대
	SFX_SKILL_WARP = 600,
	SFX_SKILL_LASER_CHARGE,
	SFX_SKILL_LASER_SHOOT,
	SFX_SKILL_LASER_HIT,


	SFX_NONE = 9999,//빈거설정용

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

// MISSILE_FIRE_MODE 삭제 — 발사 수는 missileFirePositions.Count와 curAmmo 중 작은 값으로 자동 결정


//Build Settings의 씬 순서와 일치해야 함. LoadScene(SCENE_TYPE)오버로드가(int)sceneType으로 로드함.
// 현재 Build Settings 순서 확인 후 맞출것
public enum SCENE_TYPE
{
	UNKNOWN = -1,   // 이 표에 없는 씬(작업씬 등). GameManager.curSceneType이 파싱 실패 시 쓰는 값
	MAIN = 0,
	STATION = 1,
	STATION_1F,
	STATION_B2,
	BASE_LANDING,
	LOADING_SEQUENCE = 20,   // ← 추가 (로딩 시퀀스 씬)
	MULTIPLAYER = 25,   // ← 추가 (멀티 대기실 씬)
	MAP_SELECT = 30,   // ← 추가 (맵 선택 화면)
	STAGE1 = 50,   // ← 기존 인덱스 밀릴 수 있음
	STAGE2,

	GAME_OVER = 999,   // ← 기존 인덱스 밀릴 수 있음
}
public enum INPUT_CONTROL_TYPE
{
	KEYBOARD_MOUSE,
	GAMEPAD,
	MOBILE,
}

// 게임패드 버튼(플레이스테이션 명칭). 인스펙터에서 직관적으로 고르게 하고,
// 실제 KeyCode.JoystickButton/축 매핑은 InputManager가 담당함(패드마다 번호가 달라 코드에 몰아둠).
// None = 이 패드엔 배정 안 함(안 눌림).
// ※ 스틱 '방향'(밀기)은 여기 없음 — 이동=왼쪽 스틱, 시야=오른쪽 스틱으로 축(axisLeftStick*/axisRightStick*)에서 따로 읽음.
//    여기의 L3/R3은 '스틱 누르기(클릭)'만 뜻함(방향 아님).
// ※ Dpad*는 게임패드 방향패드임 — 키보드 화살표(그건 KeyboardMouseConfig에서 KeyCode.UpArrow 등)와 전혀 별개.
// ※ L2/R2·Dpad는 버튼이 아니라 축이라 InputManager가 축으로 읽음.
// 패드 버튼 — 이름은 PS/Xbox 표기를 같이 적어 인스펙터에서 어느 패드든 바로 알아보게 함.
// 실제 판정은 이름이 아니라 '자리'(buttonSouth 등)로 하므로 어떤 패드를 꽂아도 같은 자리 버튼이 잡힘.
//
// ⚠ 번호를 명시하는 이유: 이 값이 프리팹과 설정 저장(InputSettings)에 숫자로 들어감.
//    번호를 안 박으면 항목을 중간에 끼워넣는 순간 그 아래가 전부 밀려서 배정이 뒤섞임.
//    한 번 정한 번호는 바꾸지 말 것. 항목을 지워도 그 번호는 비워두고 재사용하지 않는다.
public enum GAMEPAD_BUTTON
{
	None = 0,
	PsCross_XboxA    = 1,   // ✕ / A
	PsCircle_XboxB   = 2,   // ○ / B
	PsSquare_XboxX   = 3,   // □ / X
	PsTriangle_XboxY = 4,   // △ / Y
	PsL1_XboxLB      = 5,   // 왼쪽 범퍼
	PsR1_XboxRB      = 6,   // 오른쪽 범퍼
	PsL2_XboxLT      = 7,   // 왼쪽 트리거
	PsR2_XboxRT      = 8,   // 오른쪽 트리거
	PsL3_XboxLS      = 9,   // 왼쪽 스틱 누르기(클릭) — 스틱 방향 아님
	PsR3_XboxRS      = 10,  // 오른쪽 스틱 누르기(클릭) — 스틱 방향 아님
	PsOptions_XboxMenu = 11, // 시작 (PS Options / Xbox Menu)
	PsShare_XboxView   = 12, // 선택 (PS Share·Create / Xbox View)
	DpadUp    = 13,  // 게임패드 D패드 ↑ (키보드 화살표 아님)
	DpadDown  = 14,
	DpadLeft  = 15,
	DpadRight = 16,

	// 아래는 한쪽 패드에만 있는 버튼 — 없는 패드에서는 눌리지 않음(안 잡히면 그냥 무시됨).
	TouchpadClick = 17,  // 듀얼쇽/듀얼센스 터치패드 누르기. Xbox엔 없음
	PsButton      = 18,  // PS 버튼(홈). Xbox의 가이드 버튼에 해당하나 OS가 가로채는 경우가 많음
}

// 게임패드 아날로그 축. 이름을 문자열로 들고 다니면 오타가 나도 실행 전까지 모르고,
// Project Settings에 실제로 등록된 이름과 어긋나면 런타임 예외가 남 —
// 그래서 enum으로 고정하고 실제 축 이름 변환은 InputManager가 표로 들고 있음.
// ⚠ 여기 항목을 늘리면 InputManager.AxisNames 배열도 같은 순서로 늘려야 함.
// 조합키에서 '스틱을 어느 쪽으로 민 상태'를 조건으로 걸 때 씀.
// 버튼은 눌림/안눌림이 명확하지만 스틱은 아날로그라, 어느 스틱을 어느 방향으로
// 얼마나 밀었을 때를 '누른 것'으로 볼지 따로 정해줘야 함.
public enum GAMEPAD_STICK
{
	Left,   // 왼쪽 스틱 (이동)
	Right,  // 오른쪽 스틱 (시야)
}

public enum STICK_DIR
{
	Up,
	Down,
	Left,
	Right,
}

// 조합키에 붙일 수 있는 행동 목록.
// 인스펙터에서 "이 키들을 같이 누르면 이 행동" 형태로 고르게 하려고 만든 것 —
// InputManager.ApplyAction이 여기 값에 맞는 출력 필드를 세워줌.
// ⚠ 항목을 늘리면 InputManager.ApplyAction의 switch에도 같이 추가할 것.
// ⚠ 번호를 명시하는 이유: 이 값이 키 배정/조합키 설정으로 저장됨(InputSettings).
//    번호를 안 박으면 중간에 항목을 하나 끼워넣는 순간 그 아래 값이 전부 밀려서,
//    저장해둔 설정이 엉뚱한 행동으로 읽힌다(예: 부스트가 회피로).
//    한 번 정한 번호는 바꾸지 말 것. 항목을 지워도 그 번호는 비워두고 재사용하지 않는다.
public enum INPUT_ACTION
{
	None = 0,

	// 이동/회전 (누르는 동안 유지)
	MoveForward = 1,
	MoveBack = 2,
	MoveLeft = 3,
	MoveRight = 4,
	MoveUp = 5,
	MoveDown = 6,
	RollLeft = 7,
	RollRight = 8,
	Boost = 9,

	// 즉발 (누른 순간 한 프레임)
	Dodge = 10,
	FireBullet = 11,      // 이것만 누르는 동안 유지됨(연사)
	FireMissile = 12,
	LockOnPrev = 13,
	LockOnNext = 14,
	MissilePrev = 15,
	MissileNext = 16,
	MissileShootMode = 17,
	ToggleClusterLockMode = 18,
	SwitchConsumable = 19,
	UseConsumable = 20,
	SwitchSkillSlot = 21,
	UseSkill = 22,

	// UI
	FuelGaugeToggle = 23,
	InventoryToggle = 24,
	PauseMenu = 25,
	MapToggle = 26,
	Interact = 27,
}
public enum POOL_TYPE
{
	// 투사체 (DisableAllProjectiles 대상)
	PROJECTILE_BULLET,
	PROJECTILE_MISSILE,
	//PROJECTILE_LASER,
	PROJECTILE_MISSILE_CLUSTER = 3,
	PROJECTILE_MISSILE_CLUSTER_CHILD,
	PROJECTILE_MISSILE_DUMB,
	PROJECTILE_BULLET_BOSS,

	// 적
	ENEMY_GUNSHIP = 100,
	ENEMY_DROPSHIP,
	ENEMY_MISSILESHIP,
	ENEMY_WORKER,
	ENEMY_BOSS,
	ENEMY_STATION,

	// 아이템 / 이펙트 (추후 세분화)
	ITEM = 200,
	ITEM_ASTEROID,
	ITEM_CONSUMABLE_HPKIT_DEFAULT,
	ITEM_CONSUMABLE_BOOSTKIT_DEFAULT,
	ITEM_CONSUMABLE_SHIELDKIT_DEFAULT,
}

// PoolEntry.category — DisableByCategory(PoolCategory)에서 poolConfigs 필터링용.
public enum PoolCategory
{
	Projectile,
	Enemy,
	Item,
}

/// <summary>
/// 파츠종류
/// </summary>
public enum PART_TYPE
{
	ENGINE,//부스트게이지, 부스트게이지회복량,실드회복량
	FRAME,//기초 프레임. 무게, 착용장비가능종류? HP
	ARMOR,
	LAUNCHER_MISSILE,//동시 사출 증가관련
	LAUNCHER_BULLET,//탄속, 연사속도증가
	LAUNCHER_LASER,
	THRUSTER,        // 정방향 추진기
	THRUSTER_REVERSE, // 역추진기
	THRUSTER_SIDE,    // 측면 추진기 (닷지/롤)
}

/// <summary>
/// 파츠데이터에 들어갈 스탯들
/// </summary>
public enum STAT_TYPE
{
	HP_MAX,
	SHIELD_MAX,
	SHIELD_REGEN_RATE,  // 실드 회복률 (ENGINE 스탯)
	ARMOR_MAX,
	ARMOR_DEF,          // 데미지 경감 수치
	MOVE_SPEED_BASE,
	MOVE_SPEED_MAX,
	MOVE_SPEED_BOOST,
	BOOST_MAX,
	BOOST_REGEN_RATE,   // 부스트 회복률 (ENGINE 스탯)
	CRI_RATE,
	CRI_DMG_MULT,
	FUEL_MAX,
	FIRE_MISSILE_DELAY_DECREASE,
	FIRE_BULLET_DELAY_DECREASE,
}
/// <summary>
/// 소모품종류
/// </summary>
public enum CONSUMABLE_TYPE
{
	HP_RESTORE,
	SHIELD_RESTORE,
	BOOST_RESTORE,
}

/// <summary>
/// 인벤토리 UI 탭 분류용 카테고리.
/// ItemData.category에 설정. InventoryManager.GetAllOfCategory()로 필터링.
/// 탭 추가 시 이 enum에 값만 추가하면 됨 (새 ItemData 자식 클래스 불필요).
/// </summary>
public enum ITEM_CATEGORY
{
	PARTS,  // 파츠(장비)
	CONSUMABLE, // 소모품
	MATERIAL,   // 재료
	MISSILE,
	BULLET,
}

/// <summary>
/// 레벨업 보너스 종류.
/// STAT_TYPE(파츠/장비용)과 분리 — 인벤토리 슬롯 등 비전투 보너스 포함.
/// LevelStatData에서 사용.
/// </summary>
public enum LEVEL_BONUS_TYPE
{
	HP_MAX,
	SHIELD_MAX,
	ARMOR_MAX,
	ARMOR_DEF,
	MOVE_SPEED_BASE,
	MOVE_SPEED_MAX,
	MOVE_SPEED_BOOST,
	BOOST_MAX,
	CRI_RATE,
	CRI_DMG_MULT,
	INVENTORY_SLOTS,    // 인벤토리 슬롯 수 증가
}

public enum EFFECT_TYPE
{
	

	VFX_EXPLOSION_MISSILE = 0,//Missile.Explode()에서 호출
	VFX_EXPLOSION_MISSILE_CLUSTER,
	VFX_EXPLOSION_MISSILE_DUMB,
	VFX_EXPLOSION_DIE1,
	VFX_EXPLOSION_DIE2,
	VFX_BULLET_HIT = 100,
	VFX_BULLET_HIT_SHIELD,
	VFX_BULLET_MUZZLE,
	VFX_BULLET_MUZZLE_NOLIGHT,
	VFX_TEMP = 200,

	VFX_MISSILE_MUZZLE = 300,
	// 쉴드 파괴 이펙트
	VFX_SHIELD_DESTROY = 400,

	VFX_SKILL_LASER_BEAM,
	VFX_SKILL_LASER_CHARGE,
	VFX_SKILL_LASER_HIT,



	VFX_SKILL_WARP_IN = 1000,
	VFX_SKILL_WARP_OUT,

	VFX_BOSS_WARP_IN = 2000,
	VFX_BOSS_WARP_OUT,


   
    VFX_NONE = 9999,
}


public enum SKILL_MSY_TYPE
{
	CLUSTER,
	HOMING,
}



