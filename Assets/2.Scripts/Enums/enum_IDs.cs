/// <summary>
/// 아이템 고유 ID. 장르별로 앞자리가 다르고 종류별로 100개씩 확보.
/// 저장/로드, 네트워크 전송, DB PK 용도로 사용.
/// 새 아이템 추가 시 해당 범위 안에 값 추가 후 .asset에서 id 설정.
/// </summary>
public enum ITEM_ID
{
	NONE = 0,

	// ── 파츠 (1000번대) ──────────────────────────────────────────
	__ID_FRAME_Do_not_select_            = 1000, FRAME_DEFAULT,
	//FRAME_END              = 1099,
	__ID_ENGINE_Do_not_select_ = 1100, ENGINE_DEFAULT,  ENGINE_LIGHTWEIGHT,
	//ENGINE_END             = 1199,
	__ID_THRUSTER_Do_not_select_ = 1200, THRUSTER_DEFAULT,  THRUSTER_HEAVY,                         
	//THRUSTER_END           = 1299,
	THRUSTER_REVERSE_DEFAULT=1301,                   
	//THRUSTER_REVERSE_END   = 1399,
	THRUSTER_SIDE_DEFAULT=1401,
	//THRUSTER_SIDE_END      = 1499,
	__ID_ARMOR_Do_not_select_ = 1500, ARMOR_DEFAULT,
	//ARMOR_END              = 1599,
	__ID_LAUNCHER_Do_not_select_ = 1600, LAUNCHER_BULLET_DEFAULT_L, LAUNCHER_BULLET_DEFAULT_R,
	//LAUNCHER_BULLET_END    = 1699,
	//LAUNCHER_MISSILE   = 1700,
	LAUNCHER_MISSILE_DEFAULT_L = 1701, LAUNCHER_MISSILE_DEFAULT_R,
	//LAUNCHER_MISSILE_END   = 1799,
	// LAUNCHER_LASER 제외 — 스킬로 전환 확정

	// ── 투사체 (2000번대) ─────────────────────────────────────────
	__ID_BULLET_Do_not_select_ = 2000, BULLET_DEFAULT,                                 //BULLET_END             = 2099,
	__ID_MISSILE_Do_not_select_ = 2100, MISSILE_DEFAULT,                                //MISSILE_END            = 2199,
	//DUMB_MISSILE       = 2200,
	DUMB_MISSILE_DEFAULT = 2201,                           //DUMB_MISSILE_END       = 2299,
	//CLUSTER_MISSILE    = 2300,
	CLUSTER_MISSILE_DEFAULT = 2301,                        //CLUSTER_MISSILE_END    = 2399,
														   //NUKE_MISSILE       = 2400, NUKE_MISSILE_DEFAULT,                          // NUKE_MISSILE_END       = 2499,

	// ── 소모품 (3000번대) ─────────────────────────────────────────
	__ID_CONSUMABLE_Do_not_select_= 3000,
	//CONSUMABLE_HPKIT		= 3000,
	CONSUMABLE_HPKIT_DEFAULT = 3001,			 //CONSUMABLE_HP_END		 = 3099,
	//CONSUMABLE_SHIELDKIT	= 3100,
	CONSUMABLE_SHIELDKIT_DEFAULT = 3101,		 //CONSUMABLE_SHIELD_END	 = 3199,
	//CONSUMABLE_BOOSTKIT     = 3200,
	CONSUMABLE_BOOSTKIT_DEFAULT = 3201,		 //CONSUMABLE_BOOST_END	 = 3299,
	//CONSUMABLE_ARMORKIT		= 3300,
	CONSUMABLE_ARMORKIT_DEFAULT = 3301,      //CONSUMABLE_ARMORKIT_END = 3399,

	// ── 재료 (4000번대) ───────────────────────────────────────────
	__ID_MATERIAL_Do_not_select_ = 4000,
	MATERIAL_NICKEL = 4001,
	MATERIAL_MAGNETSTONE,//MATERIAL_END           = 4099,
}

/// <summary>
/// NPC 고유 ID. 호감도 등 NPC별 데이터 저장/조회용.
/// 실제 NPC 캐릭터가 확정되면 여기에 항목 추가.
/// </summary>
public enum NPC_ID
{
	NONE = 0,
	// ── NPC (5000번대) ───────────────────────────────────────────
	NPC_STORENPC_LYRA_VAEL = 5000,
}


/// <summary>
/// 스킬 고유 ID. 종류별로 100개씩 확보(ITEM_ID와 동일 패턴).
/// 저장/로드용. 새 스킬 추가 시 해당 범위 안에 값 추가 후 .asset에서 id 설정.
/// </summary>
public enum SKILL_ID
{
	NONE = 0,

	// ── 액티브 스킬 (6000번대) ─────────────────────────────────────
	WARP/**/ = 6000,
	MYS = 6100,
	LASER = 6200,
	DRONE = 6300,
}



