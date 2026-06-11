using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ConsumableEffect
{
    public CONSUMABLE_TYPE effectType;
    public float value;
}

[CreateAssetMenu(fileName = "New Consumable Data", menuName = "Create Data/Item/Consumable Data")]
public class ConsumableData : ItemData
{
    [Header("Effects")]
    public List<ConsumableEffect> effects = new List<ConsumableEffect>();

    [Header("Cooldown")]
    public float cooldown = 5f;
}
