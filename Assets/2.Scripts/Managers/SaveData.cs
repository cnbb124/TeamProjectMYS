using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// SaveData
// 저장 시점에 게임 전체 상태를 담는 직렬화 클래스.
// GameManager.SaveGame() 에서 Player/Loadout 등에서 수집해 JSON으로 저장.
// GameManager.LoadGame() 에서 역직렬화 후 각 스크립트에 분배.
//
// ※ MissileAmmoSaveData 별도 클래스 불필요.
//   MissileAmmoInfo가 이미 [System.Serializable]이고 필드 동일해서 직접 사용.
// ※ 착용 장비 / 인벤토리 항목은 PlayerLoadout 구현 후 추가
// =====================================================================
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

    [Header("미사일 탄약")]
    public List<MissileAmmoInfo> missileAmmoList = new List<MissileAmmoInfo>();

    // =====================================================================
    // 아래 항목은 PlayerLoadout.cs 구현 후 추가 예정
    // =====================================================================
    // public List<string> ownedItemIds;       // 보유 아이템
    // public List<string> equippedItemIds;    // 착용 장비
    // public List<ConsumableSaveData> consumables; // 소모품 슬롯
}
