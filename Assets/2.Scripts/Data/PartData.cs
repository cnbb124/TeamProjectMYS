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

    [Tooltip("장착 시 기체에 붙일 프리팹.")]
    public GameObject partPrefab;

    [Tooltip("기체 중심 기준 장착 오프셋. 플레이어 프리팹에 임시 배치해서 localPosition 값 옮겨오기.")]
    public Vector3 mountOffset;

    [Header("Frame Slots (FRAME 파츠 전용)")]
    [Tooltip("이 프레임이 제공하는 파츠 슬롯 목록. FRAME 타입 파츠에만 설정.")]
    public List<PART_TYPE> providedSlots = new List<PART_TYPE>();

    [Header("Part HP")]
    [Tooltip("이 파츠의 최대 HP. 0이면 HP 없음(FRAME 등).")]
    public int maxPartHp = 100;

    [Header("Stat Bonuses")]
    public List<PartStatBonus> statBonuses = new List<PartStatBonus>();
}
