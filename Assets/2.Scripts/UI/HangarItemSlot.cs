/*
 * [HangarItemSlot]
 * 격납고 파츠 리스트의 한 칸(아이템 슬롯).
 * PartData(ScriptableObject)를 참조해 실제 파츠 정보를 표시.
 *
 * [프리팹 구조]
 * ItemSlot (이 스크립트 부착)
 *   ├ Icon (Image)        — 파츠 아이콘
 *   ├ NameText (TMP)      — 파츠 이름
 *   └ CountText (TMP)     — 보유 수량(1개면 숨김)
 *
 * [사용법]
 * HangarSlotList가 프리팹을 Instantiate한 뒤 Setup(partData, count) 호출.
 * 슬롯이 참조하는 파츠는 Data 프로퍼티로 외부에서 읽을 수 있음 (클릭 시 장착 등에 활용).
 */

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HangarItemSlot : MonoBehaviour
{
    [SerializeField] private Image    icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text countText;

    // 이 슬롯이 표시 중인 파츠 데이터 (클릭/장착 처리에 활용)
    public PartData Data { get; private set; }

    /// <summary>
    /// PartData를 받아 슬롯 한 칸을 채움.
    /// </summary>
    /// <param name="data">표시할 파츠 데이터</param>
    /// <param name="count">보유 수량 (1이면 수량 텍스트 숨김)</param>
    public void Setup(PartData data, int count = 1)
    {
        Data = data;

        if (data == null)
        {
            // 빈 슬롯 처리
            if (icon != null)      { icon.sprite = null; icon.enabled = false; }
            if (nameText != null)  nameText.text  = "";
            if (countText != null) countText.text = "";
            return;
        }

        if (icon != null)
        {
            icon.sprite  = data.icon;
            icon.enabled = data.icon != null;
        }
        if (nameText != null)  nameText.text  = data.itemName;
        if (countText != null) countText.text = count > 1 ? $"x{count}" : "";
    }
}
