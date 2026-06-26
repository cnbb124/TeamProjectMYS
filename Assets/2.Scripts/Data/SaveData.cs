using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// SaveData
// 저장 시점에 게임 전체 상태를 담는 직렬화 클래스.
// GameManager.SaveGame() 에서 Player/Loadout 등에서 수집해 JSON으로 저장.
// GameManager.LoadGame() 에서 역직렬화 후 각 스크립트에 분배.
//
// SO 참조(PartData, MissileData 등)는 ITEM_ID 값(int)으로 변환해서 저장.
// 복원 시 ItemDatabase.Get()으로 역참조.
// ※ 착용 장비 / 인벤토리 항목은 PlayerLoadout 구현 후 추가
// =====================================================================

// 파츠 슬롯 하나를 JSON-safe 하게 저장하는 구조체.
// partId == 0(NONE) 이면 빈 슬롯.
[System.Serializable]
public class SavedPartSlot
{
    public PART_TYPE slotType;
    public int       partId;  // (int)ITEM_ID
}

// 미사일 슬롯 하나를 JSON-safe 하게 저장하는 구조체.
// MissileSlot.missileData(SO 참조) → missileDataId(int)로 변환.
[System.Serializable]
public class SavedMissileSlot
{
    public MISSILE_TYPE type;
    public int          missileDataId; // (int)ITEM_ID
    public int          curAmmo;
    public int          maxAmmo;
}

// NPC 한 명의 호감도를 JSON-safe 하게 저장하는 구조체.
[System.Serializable]
public class SavedAffection
{
    public NPC_ID npc;
    public int    value;
}

[System.Serializable]
public class SaveData
{
    [Header("플레이어 스탯")]
    public int   level;
    public int   exp;
    public int   expToNextLevel;

    [Header("현재 HP / 실드 / 아머 / 부스트")]
    public int   curHp;
    public int   curShield;
    public int   curArmor;
    public float curBoost;

    [Header("재화")]
    public int gold;

    [Header("호감도 (NPC별)")]
    public SavedAffection[] affections;

    [Header("파츠 슬롯")]
    public SavedPartSlot[] partSlots;

    [Header("미사일 슬롯")]
    public SavedMissileSlot[] missileSlots;

    // =====================================================================
    // 아래 항목은 PlayerLoadout.cs 구현 후 추가 예정
    // =====================================================================
    // public List<int> ownedItemIds;       // 보유 아이템 (ITEM_ID int)
    // public List<int> equippedItemIds;    // 착용 장비
    // public List<ConsumableSaveData> consumables; // 소모품 슬롯
}
