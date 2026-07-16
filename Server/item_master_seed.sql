-- =====================================================================
-- item_master 채우기 — 파츠 12 + 소모품 3 + 재료 2 + 투사체 5 = 22개
--
-- [실행법] MySQL Workbench나 mysql CLI에서 teamprojectmys DB 선택 후 이 파일 실행.
--   또는:  SOURCE C:/TeamProjectMYS/Server/item_master_seed.sql;
--
-- [상태] 2026-07-16 갱신
--   ✅ 파츠(1000~1702), 소모품(3001/3101/3102), 재료(4001/4002), 투사체(2005/2105/2201/2301/2305) = 22개.
--      유니티 ItemDatabase '전체 스캔 & 갱신' 결과도 22개로 일치 확인됨(2026-07-16 새 네이밍 체계).
--   ❌ 제외: MYS 스킬 전용 미사일(Cluster_Base_MYS1/Children_MYS), 적 투사체(Enemy_*).
--      → 스킬이 SO를 직접 참조 / 적은 세이브에 안 들어감 = 저장 대상 아니라 item_master 불필요(의도).
--
-- [주의] item_master.id 는 PRIMARY KEY라 중복 불가. 아래는 중복 검사 끝난 값.
--   name 은 유니티 에셋 이름(영문) 기준 — 표시명(itemName, 한글 포함)은 나중에 UPDATE로 다듬어도 됨.
--   category 는 ITEM_CATEGORY enum 이름(0=PARTS,1=CONSUMABLE,2=MATERIAL,3=MISSILE,4=BULLET)을 문자열로.
-- =====================================================================

-- 안전하게: 이미 있으면 덮어쓰기(id 기준). 처음이면 그냥 INSERT됨.
INSERT INTO item_master (id, name, category, price) VALUES
  -- ── 파츠 (PARTS, 1000번대) ─────────────────────────────
  (1000, 'Titanium_Reinforced_Chasis_Frame', 'PARTS', 35),
  (1001, 'Frame_Default',                     'PARTS', 0),
  (1101, 'Engine_Default',                    'PARTS', 0),
  (1102, 'Engine_Lightweight_PowerEngine',    'PARTS', 80),
  (1201, 'Thruster_Default',                  'PARTS', 0),
  (1202, 'Thruster_Heavy_Particle_Accelerator','PARTS', 50),
  (1301, 'Rev-Thruster_Default',              'PARTS', 0),
  (1401, 'THRUSTER_SIDE',                     'PARTS', 0),
  (1601, 'Launcher_Bullet_Default_L',         'PARTS', 0),
  (1602, 'Launcher_Bullet_Default_R',         'PARTS', 0),
  (1701, 'Launcher_Missile_Default_L',        'PARTS', 0),
  (1702, 'Launcher_Missile_Default_R',        'PARTS', 0),
  -- ── 소모품 (CONSUMABLE, 3000번대) ──────────────────────
  (3001, 'First_Aid_Kit',  'CONSUMABLE', 10),   -- HP 회복(effectType 0)
  (3101, 'Energy_Shield',  'CONSUMABLE', 100),  -- 실드 회복(effectType 1)
  (3102, 'Repair_Kit',     'CONSUMABLE', 15),   -- 실드 회복(effectType 1)
  -- ── 재료 (MATERIAL, 4000번대) ─────────────────────────
  (4001, 'Nickel',      'MATERIAL', 20),   -- 아머 업그레이드 재료
  (4002, 'MagnetStone', 'MATERIAL', 25),   -- 실드 업그레이드 재료
  -- ── 플레이어 투사체 (2000번대) — 새 네이밍 체계 번호. MYS 스킬 전용·적(Enemy) 투사체는 제외 ──
  --    유니티 ItemDatabase 스캔 결과(22개)와 동일하게 맞춤(Children_Default 포함).
  (2005, 'Player_Bullet_Vulcan_Default',            'BULLET',  0),   -- 기본 벌컨 총알
  (2105, 'Player_Missile_Homing_Default',           'MISSILE', 0),   -- 유도 미사일
  (2201, 'Player_Missile_Dumb_Default',             'MISSILE', 0),   -- 무유도 미사일
  (2301, 'Player_Missile_Cluster_Base_Default',     'MISSILE', 0),   -- 클러스터 미사일(장착용 Base)
  (2305, 'Player_Missile_Cluster_Children_Default', 'MISSILE', 0)    -- 클러스터 자탄(Base가 터지며 소환)
ON DUPLICATE KEY UPDATE
  name = VALUES(name), category = VALUES(category), price = VALUES(price);

-- 확인용: 잘 들어갔는지
-- SELECT * FROM item_master ORDER BY id;

-- =====================================================================
-- [다음 단계 — 이 파일 실행 후]
-- 1) part_slot / missile_slot 의 item_master FK 복원 (지금은 비어서 잠시 해제해둔 상태).
--    ※ 이제 파츠·장착미사일이 다 들어가므로 FK 복원 가능. 단 MYS/적 미사일은 item_master에
--       없으므로, 그것들의 id가 part_slot/missile_slot에 저장될 일이 없는지만(=장착 대상 아님) 확인.
-- =====================================================================
