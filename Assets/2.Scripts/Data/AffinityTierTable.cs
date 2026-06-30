/*
 * [AffinityTierTable]
 * 호감도 "원점수(AffectionManager)" → 레벨 / 진행률 / 등급이름 / 할인율 변환표.
 * AffectionManager는 점수만 저장하므로, 레벨·등급·할인 로직은 이 SO가 담당(방향 A).
 *
 * [만들기]
 * Project 창 우클릭 → Create → Affinity → Tier Table
 *
 * [사용 예]
 *   int pts   = AffectionManager.Instance.GetAffection(npc);
 *   int lv    = tierTable.GetLevel(pts);          // 1~maxLevel
 *   float pr  = tierTable.GetProgress01(pts);     // 현재 레벨 내 진행률 0~1
 *   string nm = tierTable.GetTierName(lv);        // 등급 이름
 *   float dc  = tierTable.GetDiscount01(lv);      // 할인율 0~1 (0.1 = 10%)
 *
 * [기본값] HTML 디자인과 동일: 100점=1레벨, 10단계, Lv2/5/10에서 5/10/15% 할인.
 */

using UnityEngine;

[CreateAssetMenu(fileName = "AffinityTierTable", menuName = "Affinity/Tier Table")]
public class AffinityTierTable : ScriptableObject
{
    [Header("레벨 스케일")]
    [Tooltip("1레벨 올리는 데 필요한 점수")]
    public int pointsPerLevel = 100;

    [Tooltip("최대 레벨 (= 등급 개수)")]
    public int maxLevel = 10;

    [Header("등급 이름 (1레벨부터 순서대로, maxLevel개)")]
    public string[] tierNames =
    {
        "Drifter", "Acquaintance", "Familiar Face", "Trusted Patron", "Friend",
        "Close Friend", "Confidante", "Kindred Spirit", "Cherished", "Beloved"
    };

    [Header("레벨별 할인율(%) — index 0 = 1레벨")]
    [Tooltip("길이가 maxLevel보다 짧으면 마지막 값을 이어서 사용")]
    public float[] discountPercentByLevel =
    {
        0f,   // Lv1
        5f,   // Lv2
        5f,   // Lv3
        5f,   // Lv4
        10f,  // Lv5
        10f,  // Lv6
        10f,  // Lv7
        10f,  // Lv8
        10f,  // Lv9
        15f   // Lv10
    };

    /// <summary>원점수 → 레벨(1~maxLevel).</summary>
    public int GetLevel(int points)
    {
        if (pointsPerLevel <= 0) return 1;
        int lv = 1 + Mathf.FloorToInt(points / (float)pointsPerLevel);
        return Mathf.Clamp(lv, 1, Mathf.Max(1, maxLevel));
    }

    /// <summary>현재 레벨 내 진행률 0~1 (만렙이면 1).</summary>
    public float GetProgress01(int points)
    {
        if (pointsPerLevel <= 0) return 1f;
        if (GetLevel(points) >= maxLevel) return 1f;
        int into = points % pointsPerLevel;
        return Mathf.Clamp01(into / (float)pointsPerLevel);
    }

    /// <summary>등급 이름. 범위 벗어나면 안전 처리.</summary>
    public string GetTierName(int level)
    {
        if (tierNames == null || tierNames.Length == 0) return $"Lv {level}";
        int idx = Mathf.Clamp(level - 1, 0, tierNames.Length - 1);
        return tierNames[idx];
    }

    /// <summary>다음 등급 이름 (만렙이면 빈 문자열).</summary>
    public string GetNextTierName(int level)
    {
        if (level >= maxLevel) return "";
        return GetTierName(level + 1);
    }

    /// <summary>레벨별 할인율 0~1 (0.1 = 10%).</summary>
    public float GetDiscount01(int level)
    {
        if (discountPercentByLevel == null || discountPercentByLevel.Length == 0) return 0f;
        int idx = Mathf.Clamp(level - 1, 0, discountPercentByLevel.Length - 1);
        return Mathf.Clamp01(discountPercentByLevel[idx] / 100f);
    }

    /// <summary>할인 적용 가격 계산 (반올림).</summary>
    public int ApplyDiscount(int basePrice, int level)
    {
        return Mathf.RoundToInt(basePrice * (1f - GetDiscount01(level)));
    }
}
