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


    //레거시
    //[Header("발사 딜레이")]
    //[Tooltip("고정 딜레이(초). 직전 항목 발사 후 이 시간만큼 대기.\n" +
    //         "0이면 직전 항목과 동시 발사.\n" +
    //         "Use Random Delay가 ON이면 이 값은 무시됨.")]
    //public float delay = 0f;

    //[Tooltip("랜덤 딜레이 사용 여부.\n" +
    //         "ON이면 매 발사마다 Min~Max 범위에서 랜덤 결정.\n" +
    //         "OFF이면 위의 Delay 고정값 사용.")]
    //public bool useRandomDelay = false;

    //[Tooltip("랜덤 딜레이 최솟값(초). Use Random Delay가 ON일 때만 적용.")]
    //public float minDelay = 0f;

    //[Tooltip("랜덤 딜레이 최댓값(초). Use Random Delay가 ON일 때만 적용.")]
    //public float maxDelay = 0.3f;

    //// 발사 시 호출. useRandomDelay에 따라 고정값 또는 랜덤값 반환.
    //public float GetDelay()
    //{
    //    return useRandomDelay ? Random.Range(minDelay, maxDelay) : delay;
    //}
}
