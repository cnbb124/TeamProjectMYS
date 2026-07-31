using System.Collections.Generic;
using UnityEngine;

// 레거시 — GameStartData.startParts로 이관됨. UnitParts는 GameStartData가 비었을 때만 이걸 씀.
[CreateAssetMenu(fileName = "New DefaultLoadout", menuName = "Create Data/Default Loadout")]
public class DefaultLoadout : ScriptableObject
{
    // 순서 무관. 코드에서 FRAME을 항상 먼저 처리함.
    public List<PartData> defaultParts;
}
