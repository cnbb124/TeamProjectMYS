using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PartStatBonus
{
    public STAT_TYPE statType;
    public float value;
}

[CreateAssetMenu(fileName = "New Part Data", menuName = "Create Item Data/Part Data")]
public class PartData : ItemData
{
    [Header("Part")]
    public string partID;
    public PART_TYPE partType;

    [Header("Stat Bonuses")]
    public List<PartStatBonus> statBonuses = new List<PartStatBonus>();
}
