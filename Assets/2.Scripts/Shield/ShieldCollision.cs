using UnityEngine;
using ProceduralForceField;

// =====================================================================
// [주의 - 세션14/16 논의] OnCollisionEnter는 일반 충돌(Collision)에만 반응함.
// 투사체(Bullet/Missile)는 IsTrigger 콜라이더라서 OnCollisionEnter가
// 호출되지 않음 → 현재 구조로는 총알/미사일 피격 시 작동 안 함.
//
// 권장 방향:
// - OnTriggerEnter로 변경하거나,
// - 이 스크립트를 제거하고 Unit.OnHitReaction()에서
//   ProceduralForceFieldOverlay.Trigger(info.hitPosition)을 직접 호출
//   (Unit.cs에 가이드 주석 추가됨)
// =====================================================================
public class ShieldCollision : MonoBehaviour
{
    [SerializeField] private ProceduralForceFieldOverlay _forceField;

    void OnCollisionEnter(Collision coll)
    {
        _forceField.Trigger(coll.contacts[0].point);
    }
}