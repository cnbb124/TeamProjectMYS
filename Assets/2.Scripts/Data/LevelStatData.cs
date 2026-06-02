using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// LevelStatData — 레벨별 보너스 테이블 ScriptableObject.
// 에셋 하나로 전 레벨 관리. index 0 = Lv1.
// Create > Create Data > Level Stat Data
//
// Player.LevelUp() 에서 levels[level - 1].bonuses 를 순회해
// ApplyLevelBonus() 호출.
//
// 새 보너스 종류 추가 시:
//   1. enum_Types.cs 의 LEVEL_BONUS_TYPE 에 항목 추가
//   2. Player.ApplyLevelBonus() switch 에 case 추가
// =====================================================================

[System.Serializable]
public class LevelBonus
{
    public LEVEL_BONUS_TYPE bonusType;
    public float value;
}

[System.Serializable]
public class LevelStat
{
    public int level;
    public List<LevelBonus> bonuses = new List<LevelBonus>();
}

[CreateAssetMenu(fileName = "New Level Stat Data", menuName = "Create Data/Level Stat Data")]
public class LevelStatData : ScriptableObject
{
    [Tooltip("index 0 = Lv1. 레벨 수만큼 항목 추가.")]
    public List<LevelStat> levels = new List<LevelStat>();

    /// <summary>
    /// 해당 레벨의 데이터 반환. 없으면 null.
    /// </summary>
    public LevelStat GetLevelStat(int level)
    {
        int idx = level - 1;
        if (idx < 0 || idx >= levels.Count)
        {
            return null;
        }
        return levels[idx];
    }
}
