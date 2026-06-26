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
    [Header("사용 효과")]
    public List<ConsumableEffect> effects = new List<ConsumableEffect>();

    [Header("재사용 쿨다운")]
    public float cooldown = 5f;
}
