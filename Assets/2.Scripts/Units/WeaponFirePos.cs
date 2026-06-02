using UnityEngine;

// =====================================================================
// WeaponFirePos — 파츠 프리팹의 발사/이펙트 위치 마커 컴포넌트.
// 파츠 프리팹 내 각 포지션 오브젝트에 붙여두면
// UnitParts가 장착/해제 시 자동으로 WeaponSystem에 등록/해제.
//
// [사용법]
// 파츠 프리팹 자식 오브젝트에 이 컴포넌트 추가 → posType 설정
// forward(파란축)이 발사 방향을 향하도록 회전 조정
// =====================================================================
public class WeaponFirePos : MonoBehaviour
{
    public WEAPON_POS_TYPE posType;
}
