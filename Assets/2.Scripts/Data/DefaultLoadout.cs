using System.Collections.Generic;
using UnityEngine;

// 씬 단독 재생용 기본 파츠. 정상 흐름은 GameStartData → PlayerProfile이 담당.
[CreateAssetMenu(fileName = "New DefaultLoadout", menuName = "Create Data/Default Loadout")]
public class DefaultLoadout : ScriptableObject
{
    // 순서 무관. 코드에서 FRAME을 항상 먼저 처리함.
    public List<PartData> defaultParts;
}
