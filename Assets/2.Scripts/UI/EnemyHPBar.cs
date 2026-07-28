using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// EnemyHPBar — 적 머리 위에 HP/실드를 표시하는 월드 스페이스 게이지.
//
// [에디터 세팅]
//   EnemyHPBar 프리팹을 적 프리팹의 '자식'으로 넣을 것.
//   부모 쪽에서 Unit을 찾으므로 자식이 아니면 아무것도 안 뜬다.
//
// [멀티플레이]
//   별도 처리 없음. curHpRemaining/curShieldRemaining이 Unit.OnPhotonSerializeView로
//   모든 클라에 흘러오므로, 각자 로컬에서 그 값을 읽어 그리기만 하면 그대로 맞는다.
//   (⚠ 적 프리팹 PhotonView의 Observed Components에 Enemy 계열 컴포넌트가 들어 있어야 값이 옴)
// =====================================================================
public class EnemyHPBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("HP 게이지 Image. Image Type을 Filled로 둘 것.")]
    [SerializeField] private Image hpFill;

    [Tooltip("실드 게이지 Image. 비워두면 실드는 표시 안 함.")]
    [SerializeField] private Image shieldFill;

    [Tooltip("아머 게이지 Image. 비워두면 아머는 표시 안 함.\n" +
             "아머는 실드 다음, HP 앞에서 데미지를 먹는 층이라 안 보여주면\n" +
             "'맞히는데 피가 안 깎인다'로 보임.")]
    [SerializeField] private Image armorFill;

    [Header("Settings")]
    [Tooltip("HP도 실드도 가득 차 있으면 숨김. 하나라도 깎이면 다시 나타남.")]
    [SerializeField] private bool hideWhenFull = true;

    [Tooltip("적 기준 게이지 위치(월드). 기체가 클수록 Y를 올릴 것.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 2f, 0f);

    [Tooltip("이 거리보다 멀면 숨김. 0 이하면 거리 제한 없음.\n" +
             "적이 많을 때 화면 밖 게이지까지 그리는 낭비를 막음.")]
    [SerializeField] private float maxViewDistance = 0f;

    [Header("Color")]
    [Tooltip("HP가 가득일 때 색. 낮아질수록 emptyHpColor로 섞임.")]
    [SerializeField] private Color fullHpColor = Color.green;
    [SerializeField] private Color emptyHpColor = Color.red;

    private Unit _unit;
    private Camera _cam;
    private Canvas _canvas;

    void Awake()
    {
        _unit   = GetComponentInParent<Unit>();
        _canvas = GetComponentInChildren<Canvas>();

        if (_unit == null)
        {
            Debug.LogWarning($"[EnemyHPBar] {name} 위쪽에 Unit이 없음 — 적 프리팹의 자식으로 넣어야 동작함.");
        }
    }

    void LateUpdate()
    {
        // 카메라는 잡을 때까지만 탐색함(잡은 뒤엔 null 체크에서 바로 빠져나가 비용 0).
        // Awake에서 한 번만 잡으면 적이 카메라보다 먼저 스폰되거나, 씬 전환으로 vCam/카메라가 파괴되면 null로 굳어 바가 영영 안 뜸.
        if (_cam == null)
        {
            _cam = Camera.main;
        }

        if (_unit == null || _cam == null || hpFill == null) return;

        // 죽었으면 무조건 숨김 — 사망 연출 중에 빈 게이지가 남아 떠다니는 것 방지.
        // (풀 반납 전까지 오브젝트가 살아있으므로 여기서 직접 꺼야 함)
        if (_unit.CurState == UNIT_STATE.DIE)
        {
            SetShown(false);
            return;
        }

        // 거리 컬링 — 숨긴 뒤에는 아래 갱신을 전부 건너뛰어 적이 많아도 비용이 안 늘어남
        if (maxViewDistance > 0f)
        {
            float sqrDist = (_unit.transform.position - _cam.transform.position).sqrMagnitude;
            if (sqrDist > maxViewDistance * maxViewDistance)
            {
                SetShown(false);
                return;
            }
        }

        // 비율 계산 — 최대치가 0인 유닛(실드 없는 적 등)은 0으로 두고, 넘치는 값은 잘라냄.
        // 멀티에서 최대치는 프리팹 값이라 클라마다 같고, 현재치만 흘러온다.
        float hpRatio = _unit.maxHpRemaining > 0
            ? Mathf.Clamp01((float)_unit.curHpRemaining / _unit.maxHpRemaining)
            : 0f;

        float shieldRatio = _unit.maxShieldCapacity > 0
            ? Mathf.Clamp01((float)_unit.curShieldRemaining / _unit.maxShieldCapacity)
            : 0f;

        float armorRatio = _unit.maxArmor > 0
            ? Mathf.Clamp01((float)_unit.curArmorRemaining / _unit.maxArmor)
            : 0f;

        // HP·실드·아머가 전부 가득이면 숨김.
        // 실드/아머가 아예 없는 적은 그 항목을 조건에서 빼야 함 — 비율이 항상 0이라
        // 그대로 두면 영원히 안 숨겨짐.
        bool hasShield = _unit.maxShieldCapacity > 0;
        bool hasArmor = _unit.maxArmor > 0;
        bool isFull = hpRatio >= 1f
                   && (!hasShield || shieldRatio >= 1f)
                   && (!hasArmor || armorRatio >= 1f);
        if (hideWhenFull && isFull)
        {
            SetShown(false);
            return;
        }

        SetShown(true);

        // 위치: 적 머리 위
        transform.position = _unit.transform.position + offset;

        // 빌보드: 항상 카메라를 향함
        transform.LookAt(
            transform.position + _cam.transform.rotation * Vector3.forward,
            _cam.transform.rotation * Vector3.up
        );

        hpFill.fillAmount = hpRatio;
        hpFill.color = Color.Lerp(emptyHpColor, fullHpColor, hpRatio);

        // 실드/아머가 아예 없는 적이면 게이지 자체를 꺼둠 — 빈 칸이 남아 있으면 있는 걸로 오해됨
        UpdateSubGauge(shieldFill, hasShield, shieldRatio);
        UpdateSubGauge(armorFill, hasArmor, armorRatio);
    }

    // 실드/아머처럼 '유닛에 따라 아예 없을 수도 있는' 게이지 갱신.
    // 없으면 오브젝트를 꺼서 빈 칸이 안 남게 하고, 있으면 비율만 반영함.
    private void UpdateSubGauge(Image fill, bool exists, float ratio)
    {
        if (fill == null)
        {
            return;
        }

        if (fill.gameObject.activeSelf != exists)
        {
            fill.gameObject.SetActive(exists);
        }

        if (exists)
        {
            fill.fillAmount = ratio;
        }
    }

    // 캔버스만 끄고 켬 — 오브젝트를 SetActive로 끄면 이 Update까지 멈춰서 다시 켜줄 주체가 없어짐.
    private void SetShown(bool shown)
    {
        if (_canvas != null && _canvas.enabled != shown)
        {
            _canvas.enabled = shown;
        }
    }
}
