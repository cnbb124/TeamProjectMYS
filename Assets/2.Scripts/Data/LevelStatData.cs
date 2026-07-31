using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// LevelStatData — "레벨업 1회당 무엇이 얼마씩 오르는가"를 담는 ScriptableObject.
// 레벨마다 표를 일일이 채우는 게 아니라, 매 레벨업에 공통으로 적용되는 증가값을 정함.
// (기존 하드코딩: HP +50, 크리 +5 ... 를 데이터로 뺀 것)
//
// Player.LevelUp() 에서 이 값을 읽어 스탯을 올림.
//
// 새 보너스 종류 추가 시:
//   1. enum_Types.cs 의 LEVEL_BONUS_TYPE 에 항목 추가
//   2. Player.ApplyLevelBonus() switch 에 case 추가
// =====================================================================

[System.Serializable]
public class LevelBonus
{
    public LEVEL_BONUS_TYPE bonusType;
    [Tooltip("레벨업 1회당 더할 값. 감소형(DODGE_COOLTIME_DECREASE 등)은 양수를 넣으면 줄어듦.")]
    public float value;
}

[System.Serializable]
public class PeriodicBonus
{
    public LEVEL_BONUS_TYPE bonusType;
    [Tooltip("적용될 때 더할 값.")]
    public float value;
    [Tooltip("몇 레벨마다 적용할지. 3이면 3/6/9레벨. 1 이하면 매 레벨 적용.")]
    public int everyNLevels = 3;
}

[CreateAssetMenu(fileName = "New Level Stat Data", menuName = "Create Data/Level Stat Data")]
public class LevelStatData : ScriptableObject
{
    [Tooltip("1레벨에서 다음 레벨까지 필요한 경험치. 새 게임 시작값이며 0이면 레벨업이 동작하지 않음.")]
    public int baseExpToNext = 100;

    [Tooltip("레벨업 1회당 '다음 레벨 필요 경험치'에 더할 값. 레벨이 오를수록 필요 경험치가 늘어남.")]
    public int expToNextIncrease = 50;

    [Tooltip("레벨업 때마다 매번 적용되는 보너스 목록.")]
    public List<LevelBonus> perLevelBonuses = new List<LevelBonus>();

    [Tooltip("일정 주기(N레벨)마다만 적용되는 보너스. 미사일 슬롯처럼 매 레벨은 과한 것들.")]
    public List<PeriodicBonus> periodicBonuses = new List<PeriodicBonus>();
}
