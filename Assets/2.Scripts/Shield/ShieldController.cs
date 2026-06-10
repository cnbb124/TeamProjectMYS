using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// [주의 - 세션14/16 논의] 현재 shieldHp가 Unit.cs의 curShieldRemaining과
// 별개로 독립 관리되고 있음 (중복 구조). 실제 데미지/회복 처리는
// Unit.cs (curShieldRemaining, maxShieldCapacity, calculTakeDamage)가
// 이미 담당하고 있으므로, 이 스크립트의 TakeDamage/shieldHp는 사용 안 함.
//
// 권장 방향: 이 스크립트는 순수 비주얼 컨트롤러로 단순화.
// - Unit.curShieldRemaining > 0 ↔ 표시, <= 0 ↔ 숨김 형태로 Unit이 직접
//   shield GameObject를 SetActive() 하거나, 여기서 Unit 참조 후
//   curShieldRemaining을 읽어 표시 여부만 결정.
// - 피격 시 비주얼 트리거(Trigger)는 ProceduralForceFieldOverlay.Trigger()
//   쪽으로, Unit.OnHitReaction()에서 호출하도록 정리 예정.
// =====================================================================
public class ShieldController : MonoBehaviour
{
    public float shieldHp = 100f;

    public void TakeDamage(float damage)
    {
        shieldHp -= damage;

        if (shieldHp <= 0)
        {
            // 쉴드 비활성화
            gameObject.SetActive(false);
        }
    }
}