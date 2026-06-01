using UnityEngine;

[CreateAssetMenu(fileName = "New Consumable", menuName = "Gallag/Consumable Data")]
public class ConsumableData : ScriptableObject
{
    [Header("Info")]
    public string itemName;
    public Sprite icon;
    [TextArea]
    public string description;

    [Header("Effect")]
    public CONSUMABLE_TYPE consumableType;
    public float value;

    [Header("Cooldown")]
    public float cooldown = 5f;
}
