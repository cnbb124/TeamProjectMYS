-- =====================================================================
-- TeamProjectMYS DB Schema
-- 기준: Assets/2.Scripts/Data/SaveData.cs, Managers/InventoryManager.cs,
--       Data/Base/ItemData.cs
-- =====================================================================

-- 계정 (로그인용)
CREATE TABLE users (
    id            INT AUTO_INCREMENT PRIMARY KEY,
    username      VARCHAR(50)  NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 플레이어 스탯 (유저 1명당 1행) — SaveData.cs 필드 그대로 매핑
CREATE TABLE player_save (
    user_id           INT PRIMARY KEY,
    level             INT     NOT NULL DEFAULT 1,
    exp               INT     NOT NULL DEFAULT 0,
    exp_to_next_level INT     NOT NULL DEFAULT 0,
    cur_hp            INT     NOT NULL DEFAULT 0,
    cur_shield        INT     NOT NULL DEFAULT 0,
    cur_armor         INT     NOT NULL DEFAULT 0,
    cur_boost         FLOAT   NOT NULL DEFAULT 0,
    gold              INT     NOT NULL DEFAULT 0,
    updated_at        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- 아이템 마스터 데이터 (ItemData.id = ITEM_ID enum 값). 게임 내 모든 파츠/소모품/미사일 종류 정의.
-- 한 번만 채워두고 이후엔 참조만.
CREATE TABLE item_master (
    id       INT PRIMARY KEY,           -- (int)ITEM_ID
    name     VARCHAR(100) NOT NULL,     -- ItemData.itemName
    category VARCHAR(30)  NOT NULL,     -- ItemData.category (ITEM_CATEGORY)
    price    INT          NOT NULL DEFAULT 0
);

-- 파츠 슬롯 (유저 1명당 여러 행) — SavedPartSlot[] 매핑
CREATE TABLE part_slot (
    id        INT AUTO_INCREMENT PRIMARY KEY,
    user_id   INT NOT NULL,
    slot_type VARCHAR(30) NOT NULL,     -- PART_TYPE enum
    part_id   INT NOT NULL,             -- item_master.id (0 = NONE/빈 슬롯)
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (part_id) REFERENCES item_master(id)
);

-- 미사일 슬롯 (유저 1명당 여러 행) — SavedMissileSlot[] 매핑
CREATE TABLE missile_slot (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    user_id         INT NOT NULL,
    type            VARCHAR(30) NOT NULL,  -- MISSILE_TYPE enum
    missile_data_id INT NOT NULL,          -- item_master.id
    cur_ammo        INT NOT NULL DEFAULT 0,
    max_ammo        INT NOT NULL DEFAULT 0,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (missile_data_id) REFERENCES item_master(id)
);

-- 인벤토리 보유 아이템 (유저 1명당 여러 행) — InventoryManager.ItemStack 매핑
CREATE TABLE inventory_item (
    id      INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    item_id INT NOT NULL,              -- item_master.id
    count   INT NOT NULL DEFAULT 1,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (item_id) REFERENCES item_master(id)
);
