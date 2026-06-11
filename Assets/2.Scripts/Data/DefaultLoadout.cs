using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New DefaultLoadout", menuName = "Create Data/Default Loadout")]
public class DefaultLoadout : ScriptableObject
{
    // 순서 무관. 코드에서 FRAME을 항상 먼저 처리함.
    public List<PartData> defaultParts;
}
