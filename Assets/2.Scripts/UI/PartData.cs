using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PartStatBonus
{
    public STAT_TYPE statType;
    public float value;
}

[CreateAssetMenu(fileName = "New Part Data", menuName = "Gallag/Part Data")]
public class PartData : ScriptableObject
{
    [Header("Info")]
    public string partID;
    public string partName;
    public Sprite partIcon;
    [TextArea]
    public string description;

    [Header("Part Type")]
    public PART_TYPE partType;

    [Header("Stat Bonuses")]
    public List<PartStatBonus> statBonuses = new List<PartStatBonus>();

    [Header("Price")]
    public int price;
}
