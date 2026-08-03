using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// PlayerProfile — 플레이어 진행 데이터의 실제 주인.
//
// 함선(Player)은 이 데이터를 읽어 자기를 세팅하는 '표현'일 뿐임.
// 함선이 Photon 오브젝트라 방을 나가면 파괴되는데, 데이터까지 같이 사라지면
// 저장·스탯표시·멀티전환이 전부 함선 생존에 묶임. 그래서 데이터를 여기로 뺌.
//
// 골드/아이템은 InventoryManager(DDOL)가 계속 담당함 — 원래 함선과 무관했음.
// =====================================================================
public static class PlayerProfile
{
	/// <summary>새 게임/불러오기로 한 번이라도 채워졌는지.</summary>
	public static bool HasData { get; private set; }

	public static int level = 1;
	public static int exp;
	public static int expToNextLevel;

	public static int curHp;
	public static int curShield;
	public static int curArmor;
	public static float curBoost;
	public static float curFuel;

	public static readonly List<PartData> parts = new List<PartData>();
	// parts와 같은 인덱스. 음수면 정보 없음(만피).
	public static readonly List<int> partHps = new List<int>();
	public static readonly List<SavedMissileSlot> missiles = new List<SavedMissileSlot>();
	public static readonly List<SavedSkill> skills = new List<SavedSkill>();
	public static ITEM_ID[] quickSlots = new ITEM_ID[0];

	// 도메인 리로드 없이 플레이 모드를 다시 들어가면 이전 판 데이터가 남음
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetOnPlay()
	{
		Clear();
	}

	public static void Clear()
	{
		HasData = false;
		level = 1;
		exp = 0;
		expToNextLevel = 0;
		curHp = 0;
		curShield = 0;
		curArmor = 0;
		curBoost = 0f;
		curFuel = 0f;
		parts.Clear();
		partHps.Clear();
		missiles.Clear();
		skills.Clear();
		quickSlots = new ITEM_ID[0];
	}

	/// <summary>새 게임 초기 상태.</summary>
	public static void InitFromStartData(GameStartData data)
	{
		Clear();
		HasData = true;
		if (data == null)
		{
			return;
		}

		level = Mathf.Max(1, data.startLevel);
		CacheBaseStats(data.basePlayerPrefab);
		expToNextLevel = _baseExpToNext;

		foreach (PartData part in data.startParts)
		{
			if (part != null)
			{
				parts.Add(part);
				partHps.Add(-1);   // 새 게임이니 만피
			}
		}

		foreach (SkillData skill in data.startSkills)
		{
			if (skill != null)
			{
				skills.Add(new SavedSkill { skillId = skill.id, slotIndex = -1 });
			}
		}

		// 체력·미사일은 프리팹 기본값이 기준이라 여기서 안 정함.
		// 함선이 처음 스폰될 때 그 값을 그대로 받아 CaptureFrom으로 채워짐.
	}

	/// <summary>세이브에서 복원.</summary>
	public static void InitFromSave(SaveData data, ItemDatabase itemDatabase)
	{
		Clear();
		if (data == null)
		{
			return;
		}
		HasData = true;

		level = data.level;
		exp = data.exp;
		expToNextLevel = data.expToNextLevel;
		curHp = data.curHp;
		curShield = data.curShield;
		curArmor = data.curArmor;
		curBoost = data.curBoost;
		curFuel = data.curFuel;

		if (data.partSlots != null && itemDatabase != null)
		{
			foreach (SavedPartSlot saved in data.partSlots)
			{
				if (saved == null || saved.partId == 0)
				{
					continue;
				}
				PartData part = itemDatabase.Get<PartData>((ITEM_ID)saved.partId);
				if (part != null)
				{
					parts.Add(part);
					partHps.Add(data.version >= 2 ? saved.curPartHp : -1);
				}
			}
		}

		if (data.missileSlots != null)
		{
			missiles.AddRange(data.missileSlots);
		}
		if (data.skills != null)
		{
			skills.AddRange(data.skills);
		}
		if (data.quickSlotItemIds != null)
		{
			quickSlots = (ITEM_ID[])data.quickSlotItemIds.Clone();
		}
	}

	/// <summary>함선의 현재 상태를 프로필로 끌어옴. 저장 직전·씬 이탈 직전에 호출.</summary>
	public static void CaptureFrom(Player player)
	{
		if (player == null)
		{
			return;
		}
		HasData = true;

		level = player.level;
		exp = player.exp;
		expToNextLevel = player.expToNextLevel;
		curHp = player.curHpRemaining;
		curShield = player.curShieldRemaining;
		curArmor = player.curArmorRemaining;
		curBoost = player.curBoostRemaining;
		curFuel = player.curFuelRemaining;

		parts.Clear();
		partHps.Clear();
		UnitParts unitParts = player.GetComponent<UnitParts>();
		if (unitParts != null)
		{
			foreach (PartSlotEntry slot in unitParts.partSlots)
			{
				if (slot.equippedPart != null)
				{
					parts.Add(slot.equippedPart);
					partHps.Add(slot.curPartHp);
				}
			}
		}

		// [진단] 파츠 HP가 0으로 저장되는 문제 추적용. 확인 끝나면 지울 것
		{
			string dump = "";
			for (int i = 0; i < parts.Count; i++)
			{
				dump += $"\n    {parts[i].name}({parts[i].partType}) cur={partHps[i]} max={parts[i].maxPartHp}";
			}
			Debug.Log($"[진단-수집] 함선 '{player.name}' 에서 프로필로 담음. " +
					  $"파츠 {parts.Count}개 / 연료 {player.curFuelRemaining}/{player.maxFuelCapacity}{dump}\n" +
					  $"호출 경로:\n{System.Environment.StackTrace}");
		}

		missiles.Clear();
		if (player.weaponSystem != null && player.weaponSystem.missileSlots != null)
		{
			foreach (MissileSlot src in player.weaponSystem.missileSlots)
			{
				missiles.Add(new SavedMissileSlot
				{
					type = src.type,
					missileDataId = src.missileData != null ? (int)src.missileData.id : 0,
					curAmmo = src.curAmmo,
					maxAmmo = src.maxAmmo
				});
			}
		}

		skills.Clear();
		if (player.skillSystem != null)
		{
			SavedSkill[] collected = player.skillSystem.CollectSaveData();
			if (collected != null)
			{
				skills.AddRange(collected);
			}
		}

		QuickSlot quickSlot = player.GetComponent<QuickSlot>();
		if (quickSlot != null && quickSlot.slots != null)
		{
			quickSlots = new ITEM_ID[quickSlot.slots.Length];
			for (int i = 0; i < quickSlot.slots.Length; i++)
			{
				quickSlots[i] = quickSlot.slots[i] != null ? quickSlot.slots[i].id : 0;
			}
		}
	}

	/// <summary>프로필을 함선에 씌움. 함선이 스폰된 뒤(모든 Start 종료 후) 호출.</summary>
	public static void ApplyTo(Player player, ItemDatabase itemDatabase)
	{
		if (player == null || !HasData)
		{
			return;
		}

		player.level = level;
		player.exp = exp;
		// 새 게임이면 요구치가 아직 0임 — 함선 프리팹 값을 그대로 둬야 레벨업이 성립함
		if (expToNextLevel > 0)
		{
			player.expToNextLevel = expToNextLevel;
		}

		UnitParts unitParts = player.GetComponent<UnitParts>();
		if (unitParts != null && parts.Count > 0)
		{
			unitParts.ReloadLoadout(parts, partHps);
		}

		// 최대치는 저장하지 않고 레벨에서 다시 계산 — 파츠를 끼운 뒤에 불러야 파츠 몫까지 같이 얹힘.
		player.RecalcStatsFromLevel();

		// [진단] curHp > maxHp 추적용. 확인 끝나면 지울 것
		Debug.Log($"[진단-복원2] 재계산 직후 lv={player.level} 파츠목록={parts.Count}개 " +
				  $"maxHp={player.maxHpRemaining} curHp(프로필)={curHp} " +
				  $"maxShield={player.maxShieldCapacity} maxArmor={player.maxArmor} maxBoost={player.maxBoostCapacity}");

		if (player.weaponSystem != null && missiles.Count > 0 && itemDatabase != null)
		{
			player.weaponSystem.missileSlots = new List<MissileSlot>();
			foreach (SavedMissileSlot saved in missiles)
			{
				player.weaponSystem.missileSlots.Add(new MissileSlot
				{
					type = saved.type,
					missileData = saved.missileDataId != 0
						? itemDatabase.Get<MissileData>((ITEM_ID)saved.missileDataId)
						: null,
					curAmmo = saved.curAmmo,
					maxAmmo = saved.maxAmmo
				});
			}
		}

		if (player.skillSystem != null && skills.Count > 0)
		{
			player.skillSystem.LoadSaveData(skills.ToArray());
		}

		QuickSlot quick = player.GetComponent<QuickSlot>();
		if (quick != null && quickSlots != null && itemDatabase != null)
		{
			for (int i = 0; i < quickSlots.Length; i++)
			{
				ConsumableData consumable = quickSlots[i] != 0
					? itemDatabase.Get<ConsumableData>(quickSlots[i])
					: null;
				quick.AssignSlot(i, consumable);
			}
		}

		// 체력류는 파츠 적용으로 최대치가 바뀐 뒤에 넣어야 잘림 없이 들어감.
		// 손상된 파츠를 복원하면 최대치가 내려가 있을 수 있으므로 최대치로 한 번 더 자름.
		if (curHp > 0)
		{
			player.curHpRemaining = Mathf.Min(curHp, player.maxHpRemaining);
			player.curShieldRemaining = Mathf.Min(curShield, player.maxShieldCapacity);
			player.curArmorRemaining = Mathf.Min(curArmor, player.maxArmor);
			player.curBoostRemaining = Mathf.Min(curBoost, player.maxBoostCapacity);
			player.curFuelRemaining = Mathf.Min(curFuel, player.maxFuelCapacity);
		}

		// [진단] curHp > maxHp 추적용. 확인 끝나면 지울 것
		Debug.Log($"[진단-복원3] ApplyTo 끝 lv={player.level} " +
				  $"HP {player.curHpRemaining}/{player.maxHpRemaining} " +
				  $"실드 {player.curShieldRemaining}/{player.maxShieldCapacity} " +
				  $"아머 {player.curArmorRemaining}/{player.maxArmor} " +
				  $"부스트 {player.curBoostRemaining}/{player.maxBoostCapacity}");
	}

	/// <summary>저장용 구조체에 옮겨 담음.</summary>
	public static void WriteTo(SaveData data)
	{
		if (data == null)
		{
			return;
		}

		data.level = level;
		data.exp = exp;
		data.expToNextLevel = expToNextLevel;
		data.curHp = curHp;
		data.curShield = curShield;
		data.curArmor = curArmor;
		data.curBoost = curBoost;
		data.curFuel = curFuel;

		data.partSlots = new SavedPartSlot[parts.Count];
		for (int i = 0; i < parts.Count; i++)
		{
			// 프로필에 HP 정보가 없으면(-1) 만피로 기록 — 불러올 때 0(파괴)으로 오인되지 않게.
			int hp = i < partHps.Count && partHps[i] >= 0 ? partHps[i] : parts[i].maxPartHp;
			data.partSlots[i] = new SavedPartSlot
			{
				slotType = parts[i].partType,
				partId = (int)parts[i].id,
				curPartHp = hp
			};
		}

		data.missileSlots = missiles.ToArray();
		data.skills = skills.ToArray();
		data.quickSlotItemIds = quickSlots;
	}

	// 기준 기체 프리팹의 소지 스탯. 함선이 없는 씬에서 최대치를 계산할 때 밑값으로 씀.
	private static int _baseMaxHp;
	private static int _baseMaxShield;
	private static int _baseMaxArmor;
	private static int _baseExpToNext;
	private static bool _baseCached;
	private static LevelStatData _levelStatData;

	/// <summary>기준 기체 프리팹에서 밑값 스탯을 읽어둠. GameStartData가 있으면 게임 시작 시 1회.</summary>
	public static void CacheBaseStats(GameObject playerPrefab)
	{
		if (_baseCached || playerPrefab == null)
		{
			return;
		}
		Player prefabPlayer = playerPrefab.GetComponent<Player>();
		if (prefabPlayer == null)
		{
			return;
		}
		_baseMaxHp = prefabPlayer.maxHpRemaining;
		_baseMaxShield = prefabPlayer.maxShieldCapacity;
		_baseMaxArmor = prefabPlayer.maxArmor;
		_baseExpToNext = prefabPlayer.levelStatData != null ? prefabPlayer.levelStatData.baseExpToNext : 0;
		_levelStatData = prefabPlayer.levelStatData;
		_baseCached = true;
	}

	/// <summary>기준 기체 + 레벨 + 파츠. Player.RecalcStatsFromLevel과 같은 식.</summary>
	public static int GetMaxHp()
	{
		return _baseMaxHp
			+ Mathf.RoundToInt(GetLevelStatBonus(LEVEL_BONUS_TYPE.HP_MAX) + GetPartStatBonus(STAT_TYPE.HP_MAX));
	}

	public static int GetMaxShield()
	{
		return _baseMaxShield
			+ Mathf.RoundToInt(GetLevelStatBonus(LEVEL_BONUS_TYPE.SHIELD_MAX) + GetPartStatBonus(STAT_TYPE.SHIELD_MAX));
	}

	public static int GetMaxArmor()
	{
		return _baseMaxArmor
			+ Mathf.RoundToInt(GetLevelStatBonus(LEVEL_BONUS_TYPE.ARMOR_MAX) + GetPartStatBonus(STAT_TYPE.ARMOR_MAX));
	}

	/// <summary>2레벨부터 현재 level까지 쌓인 그 항목의 레벨 보너스 합.</summary>
	public static float GetLevelStatBonus(LEVEL_BONUS_TYPE bonusType)
	{
		if (_levelStatData == null)
		{
			return 0f;
		}

		float total = 0f;
		for (int lv = 2; lv <= level; lv++)
		{
			for (int i = 0; i < _levelStatData.perLevelBonuses.Count; i++)
			{
				LevelBonus b = _levelStatData.perLevelBonuses[i];
				if (b.bonusType == bonusType)
				{
					total += b.value;
				}
			}

			for (int i = 0; i < _levelStatData.periodicBonuses.Count; i++)
			{
				PeriodicBonus p = _levelStatData.periodicBonuses[i];
				bool due = p.everyNLevels <= 1 || lv % p.everyNLevels == 0;
				if (due && p.bonusType == bonusType)
				{
					total += p.value;
				}
			}
		}
		return total;
	}

	/// <summary>장착 파츠의 스탯 보너스 합계. 함선 없이 스탯을 보여줘야 하는 UI용.</summary>
	public static float GetPartStatBonus(STAT_TYPE statType)
	{
		float sum = 0f;
		foreach (PartData part in parts)
		{
			if (part == null)
			{
				continue;
			}
			foreach (PartStatBonus bonus in part.statBonuses)
			{
				if (bonus.statType == statType)
				{
					sum += bonus.value;
				}
			}
		}
		return sum;
	}
}
