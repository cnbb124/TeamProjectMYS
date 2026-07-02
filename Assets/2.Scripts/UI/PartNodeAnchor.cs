/*
 * [PartNodeAnchor]
 * 격납고 기체의 "파츠 부위" 3D 마커. 기체 모델의 각 부위(콕핏/엔진/윙 등)에
 * 빈 자식 오브젝트로 배치하고 이 스크립트를 부착. PartNodeUI가 이 위치를 화면 노드로 변환.
 *
 * [세팅]
 * 1. 격납고 기체 모델 하위에 빈 오브젝트 생성 (예: Anchor_Engine)
 * 2. 그 부위 표면 근처로 위치 이동
 * 3. 이 스크립트 부착 + partType 지정 (이 부위에 장착 가능한 파츠 종류)
 * 4. label은 노드에 표시할 부위 이름 (비우면 partType 이름 사용)
 */

using UnityEngine;

public class PartNodeAnchor : MonoBehaviour
{
    [Tooltip("이 부위에 장착 가능한 파츠 종류")]
    public PART_TYPE partType;

    [Tooltip("노드에 표시할 부위 이름 (비우면 partType 이름)")]
    public string label;

    /// <summary>노드 표시용 이름.</summary>
    public string DisplayName => string.IsNullOrEmpty(label) ? partType.ToString() : label;
}
