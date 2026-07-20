/*
 * [AlertSystemUI]
 * 적 미사일 피격 경고등 ("MISSILE WARNING").
 * 플레이어를 추적(targetTr) 중인 활성 적 미사일이 하나라도 있으면 경고를 깜빡임.
 * (실제 전투기의 미사일 경고 수신기(MWR)와 동일한 개념 — 위협 알림)
 *
 * [부착 위치]
 * AlertPanelU (부모)에 부착. 깜빡일 대상(자식 "Alert")을 alertRoot에 연결.
 * ※ 자기 자신을 끄면 Update가 멈추므로, 스크립트는 "끄지 않는 부모"에 둔다.
 *
 * [하이어라키 예시]
 * AlertPanelU      ← 이 스크립트 부착 (항상 켜둠)
 *   └ Alert        ← alertRoot (경고 프레임 + 텍스트, 깜빡임 대상)
 *       └ Text(TMP)
 *
 * [동작]
 * - scanInterval 마다 씬의 활성 Missile을 검사 → targetTr이 플레이어면 위협으로 판단
 * - 위협 있으면 blinkInterval 주기로 alertRoot On/Off (깜빡임)
 * - 위협 없으면 alertRoot Off
 * - warningAudio 연결 시 위협 동안 경고음 재생 (선택)
 *
 * [인스펙터 연결]
 * - alertRoot    : 깜빡일 경고 오브젝트(자식 Alert)
 * - warningAudio : 경고음 AudioSource (선택, 없으면 무음)
 */

using UnityEngine;

public class AlertSystemUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject  alertRoot;     // 깜빡일 경고 오브젝트(자식)
    private SoundManager _soundManager;

    [Header("Settings")]
    [SerializeField] private float scanInterval  = 0.2f;  // 미사일 검사 주기(초)
    [SerializeField] private float blinkInterval = 0.4f;  // 깜빡임 주기(초)

    private float _scanTimer;
    private float _blinkTimer;
    private bool  _threat;

    private void Start()
    {
        _soundManager = SoundManager.Instance;
        // 시작 시 경고 꺼둠
        if (alertRoot != null) alertRoot.SetActive(false);
    }

    private void Update()
    {
        // ── 위협 검사 (주기적으로만) ──
        _scanTimer += Time.deltaTime;
        if (_scanTimer >= scanInterval)
        {
            _scanTimer = 0f;
            bool hadThreat = _threat;
            _threat = HasIncomingMissile();
            if (_threat && !hadThreat && _soundManager != null)
                _soundManager.PlaySFXUI(SOUND_TYPE.SFX_UI_LOCKON_ALERT);
        }

        // ── 경고 표시 ──
        if (_threat)
        {
            _blinkTimer += Time.deltaTime;
            if (_blinkTimer >= blinkInterval)
            {
                _blinkTimer = 0f;
                if (alertRoot != null) alertRoot.SetActive(!alertRoot.activeSelf);
            }

        }
        else
        {
            if (alertRoot != null && alertRoot.activeSelf) alertRoot.SetActive(false);
            _blinkTimer = 0f;
        }
    }

    /// <summary>
    /// 씬의 활성 미사일 중 '내 함선(로컬 플레이어)'을 추적하는 것이 있는지 검사.
    /// targetTr이 로컬 플레이어(또는 그 자식 히트박스)면 위협으로 판단.
    /// (플레이어 자신의 미사일은 적을 추적하므로 자동 제외됨)
    /// 멀티에선 씬에 함선이 여럿이라 IsMine으로 걸러야 함 — 안 걸면 남이 락온당해도 내 경고가 뜸.
    /// 싱글은 Player가 항상 IsMine=true라 기존과 동일하게 동작.
    /// </summary>
    private bool HasIncomingMissile()
    {
        // FindObjectsOfType는 비활성(풀 대기) 미사일은 반환하지 않음 → 날아다니는 것만 검사
        Missile[] missiles = FindObjectsOfType<Missile>();
        for (int i = 0; i < missiles.Length; i++)
        {
            Transform t = missiles[i].targetTr;
            if (t == null)
                continue;
            Player p = t.GetComponentInParent<Player>();
            if (p != null && p.IsMine)
                return true;
        }
        return false;
    }
}
