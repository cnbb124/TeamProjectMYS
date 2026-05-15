using UnityEngine;

// 유니티 우클릭 메뉴에서 쉽게 생성할 수 있게 달아주는 어트리뷰트!



[CreateAssetMenu(fileName = "New Part Data", menuName = "Gallag/Part Data")]
public class PartData : ScriptableObject
{
	[Header("기본 정보")]
    public string partID; // 파츠 고유 ID (ex: "Fighter_01")
    public string partName; // 텍스트에 띄울 이름
    public Sprite partIcon; // 노드 버튼에 들어갈 아이콘 이미지
    [TextArea]
    public string description; // 우측 스탯창에 띄울 세부 설명

    [Header("스탯 정보")]
    public int attackPower;
    public int speed;
    public int price; // 해금 비용 (MRP 등)

    
    // TODO: 갤러그 미연시에 맞게 호감도 스탯이나 특수 기믹 변수들을 여기에 추가.
}