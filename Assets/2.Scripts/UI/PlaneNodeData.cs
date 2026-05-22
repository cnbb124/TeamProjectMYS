using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlaneNodeData", menuName = "HangarUI/Unit Data")]
public class PlaneNodeData : ScriptableObject
{
    [Header("Identity")]
    public string unitName;
    public Sprite icon;
    public int mrpCost;
    public bool isUnlocked;
    [TextArea(2, 4)] public string description;

    [Header("Tree Structure")]
    public List<PlaneNodeData> children = new();
    [Header("Specifications")]
    [Range(0, 100)] public int hp;
    [Range(0, 100)] public int speed;
    [Range(0, 100)] public int defense;
    [Range(0, 100)] public int mobility;
    [Range(0, 100)] public int stability;
    [Range(0, 100)] public int airToAir;
    [Range(0, 100)] public int airToGround;
    
    [Header("Parts Slots")]
    public int bodySlots = 6;
    public int armSlots = 6;
    public int miscSlots = 6;
    public int usedBodySlots;
    public int usedArmSlots;
    public int usedMiscSlots;

    [Header("Weapons Ammo")]
    public int gunAmmo;
    public int mslAmmo;
    public int flrAmmo;
    public int emplAmmo;
}