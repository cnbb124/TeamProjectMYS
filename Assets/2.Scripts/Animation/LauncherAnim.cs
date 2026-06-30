using UnityEngine;

// =====================================================================
// LauncherAnim
// 총구 / 미사일 런처 파츠 프리팹에 부착.
// Animator 컴포넌트 필수. 비주얼 없는 파츠는 컴포넌트 자체를 붙이지 않으면 됨.
// WeaponSystem이 RegisterFirePos 시 자동 감지 및 캐싱.
// 발사 시 Animator 상태 "Fire"로 CrossFade 재생.
// =====================================================================
[RequireComponent(typeof(Animator))]
public class LauncherAnim : MonoBehaviour
{
    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    /// <summary>
    /// 발사 시 호출. WeaponSystem.ShootBullet / ShootMissileFrom에서 호출.
    /// </summary>
    public void PlayFire()
    {
        if (_animator == null)
        {
            return;
        }
        _animator.CrossFade("Fire", 0.1f);
    }
}
