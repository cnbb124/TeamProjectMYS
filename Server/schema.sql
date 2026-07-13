-- =====================================================================
-- TeamProjectMYS DB Schema
-- 기준: Assets/2.Scripts/Data/SaveData.cs, Managers/InventoryManager.cs,
--       Data/Base/ItemData.cs
--
-- 세이브 슬롯: 유저 1명당 최대 10개 (slot 0~9). 로비 화면에서 플레이 누르면
-- 슬롯 목록(로드 화면)이 뜨고, 슬롯 선택 시 그 슬롯을 불러오거나 새로 시작.
-- =====================================================================

-- 계정 (로그인용)
CREATE TABLE users (
    id            INT AUTO_INCREMENT PRIMARY KEY,
    username      VARCHAR(50)  NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 플레이어 스탯 (유저 1명당 최대 10행 = 슬롯 0~9) — SaveData.cs 필드 그대로 매핑
CREATE TABLE player_save (
    user_id           INT NOT NULL,
    slot              TINYINT NOT NULL,        -- 0~9 (UI에는 "슬롯 1~10"으로 +1 해서 표시)
    level             INT     NOT NULL DEFAULT 1,
    exp               INT     NOT NULL DEFAULT 0,
    exp_to_next_level INT     NOT NULL DEFAULT 0,
    cur_hp            INT     NOT NULL DEFAULT 0,
    cur_shield        INT     NOT NULL DEFAULT 0,
    cur_armor         INT     NOT NULL DEFAULT 0,
    cur_boost         FLOAT   NOT NULL DEFAULT 0,
    gold              INT     NOT NULL DEFAULT 0,
    -- 호감도/스킬 등 "서버가 검증 안 하고 보관만 하는" 데이터를 JSON 쪽지째 저장 (하이브리드 방식)
    -- 새 항목이 생겨도 테이블 구조를 안 바꿔도 됨. 공부노트.md 4번 참고.
    extra_json        JSON    NULL,
    updated_at        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id, slot),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- 아이템 마스터 데이터 (ItemData.id = ITEM_ID enum 값). 게임 내 모든 파츠/소모품/미사일 종류 정의.
-- 한 번만 채워두고 이후엔 참조만. (지금은 비어있어 part_slot/missile_slot의 FK는 잠시 해제)
CREATE TABLE item_master (
    id       INT PRIMARY KEY,           -- (int)ITEM_ID
    name     VARCHAR(100) NOT NULL,     -- ItemData.itemName
    category VARCHAR(30)  NOT NULL,     -- ItemData.category (ITEM_CATEGORY)
    price    INT          NOT NULL DEFAULT 0
);

-- 파츠 슬롯 (유저 1명당, 세이브 슬롯 1개당 여러 행) — SavedPartSlot[] 매핑
CREATE TABLE part_slot (
    id        INT AUTO_INCREMENT PRIMARY KEY,
    user_id   INT NOT NULL,
    slot      TINYINT NOT NULL,           -- 이 파츠가 속한 세이브 슬롯 (0~9)
    slot_type INT NOT NULL,               -- (int)PART_TYPE  ← 유니티 JsonUtility가 enum을 숫자로 저장
    part_id   INT NOT NULL,               -- (int)ITEM_ID (0 = NONE/빈 슬롯)
    FOREIGN KEY (user_id, slot) REFERENCES player_save(user_id, slot) ON DELETE CASCADE
);

-- 미사일 슬롯 (유저 1명당, 세이브 슬롯 1개당 여러 행) — SavedMissileSlot[] 매핑
CREATE TABLE missile_slot (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    user_id         INT NOT NULL,
    slot            TINYINT NOT NULL,      -- 이 미사일이 속한 세이브 슬롯 (0~9)
    type            INT NOT NULL,          -- (int)MISSILE_TYPE
    missile_data_id INT NOT NULL,          -- (int)ITEM_ID
    cur_ammo        INT NOT NULL DEFAULT 0,
    max_ammo        INT NOT NULL DEFAULT 0,
    FOREIGN KEY (user_id, slot) REFERENCES player_save(user_id, slot) ON DELETE CASCADE
);

-- 인벤토리 보유 아이템 (유저 1명당, 세이브 슬롯 1개당 여러 행) — InventoryManager.ItemStack 매핑
CREATE TABLE inventory_item (
    id      INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    slot    TINYINT NOT NULL,              -- 이 아이템이 속한 세이브 슬롯 (0~9)
    item_id INT NOT NULL,                  -- (int)ITEM_ID
    count   INT NOT NULL DEFAULT 1,
    FOREIGN KEY (user_id, slot) REFERENCES player_save(user_id, slot) ON DELETE CASCADE
);
