using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ConsumableEffect
{
    public CONSUMABLE_TYPE effectType;
    public float value;
}

[CreateAssetMenu(fileName = "New Consumable", menuName = "Gallag/Consumable Data")]
public class ConsumableData : ScriptableObject
{
    [Header("Info")]
    public string itemName;
    public Sprite icon;
    [TextArea]
    public string description;

    [Header("Effects")]
    public List<ConsumableEffect> effects = new List<ConsumableEffect>();

    [Header("Cooldown")]
    public float cooldown = 5f;
}
