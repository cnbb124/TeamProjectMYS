using UnityEngine;
// =====================================================================
// [ShopNPCAnimController]
// 호감도(AffectionManager) + 플레이어 근접 여부에 따라 NPC 애니메이션 전환.
//
//  ▶ 호감도 낮음  : Angry(팔짱) 기본 Idle
//  ▶ 호감도 보통  : Standing Idle 기본
//  ▶ 호감도 높음  : Greeting을 기본 Idle처럼 사용
//  ▶ 플레이어 근접 시 : Greeting 우선 재생 (호감도와 무관하게 인사)
//  ▶ 거래 중        : Using Touchscreen
//
// AffectionManager.OnAffectionChanged 구독 — 이 NPC(npcId) 대상일 때만 갱신.
// =====================================================================

public class ShopNPCAnim : MonoBehaviour
{
    private enum NPC_ANIM_STATE
    {
        Idle,
        Angry,
        Greeting,
    }

    [SerializeField] private NPC_ID npcId;
    [SerializeField] private Animator animator;
    [SerializeField] private AffinityTierTable tierTable;

    [Header("호감도 레벨 구간 (1~10)")]
    [SerializeField] private int lowLevel = 2;   // 이 값 이하 -> 낮음 구간
    [SerializeField] private int highLevel = 7;  // 이 값 이상 -> 높음 구간

    [Header("구간별 기본 애니메이션 (Inspector에서 선택)")]
    [SerializeField] private NPC_ANIM_STATE lowLevelAnim = NPC_ANIM_STATE.Angry;
    [SerializeField] private NPC_ANIM_STATE midLevelAnim = NPC_ANIM_STATE.Idle;
    [SerializeField] private NPC_ANIM_STATE highLevelAnim = NPC_ANIM_STATE.Idle;

    // Animator State 실제 이름 매핑
    private readonly string IDLE = "Idle";
    private readonly string GREETING = "Greeting";
    private readonly string ANGRY = "Angry";
    private readonly string TRADING = "Using Touchscreen";

    private bool _playerNearby = false;
    private bool _isTrading = false;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInParent<Animator>();
        }
    }

    private void OnEnable()
    {
        if (AffectionManager.Instance != null)
        {
            AffectionManager.Instance.OnAffectionChanged += OnAffectionChanged;
        }
        RefreshIdleState();
    }

    private void OnDisable()
    {
        if (AffectionManager.Instance != null)
        {
            AffectionManager.Instance.OnAffectionChanged -= OnAffectionChanged;
        }
    }

    private void OnAffectionChanged(NPC_ID changed, int value)
    {
        if (changed != npcId)
        {
            return;
        }
        RefreshIdleState();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }
        _playerNearby = true;
        animator.CrossFade(GREETING, 0.25f);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }
        _playerNearby = false;
        if (!_isTrading)
        {
            RefreshIdleState();
        }
    }

    public void StartTrading()
    {
        _isTrading = true;
        animator.CrossFade(TRADING, 0.25f);
    }

    public void EndTrading()
    {
        _isTrading = false;
        RefreshIdleState();
    }

    // 플레이어 근접/거래 중이 아닐 때, 호감도 레벨 구간에 맞는 기본 애니메이션 재생
    private void RefreshIdleState()
    {
        if (_playerNearby || _isTrading)
        {
            return;
        }

        if (tierTable == null || AffectionManager.Instance == null)
        {
            Debug.Log("[ShopNPCAnim] tierTable 또는 AffectionManager가 null!");
            animator.CrossFade(IDLE, 0.25f);
            return;
        }

        int points = AffectionManager.Instance.GetAffection(npcId);
        int level = tierTable.GetLevel(points);
        Debug.Log($"[ShopNPCAnim] points={points}, level={level}, lowLevel={lowLevel}");

        NPC_ANIM_STATE targetState;
        if (level <= lowLevel)
        {
            targetState = lowLevelAnim;
        }
        else if (level >= highLevel)
        {
            targetState = highLevelAnim;
        }
        else
        {
            targetState = midLevelAnim;
        }

        PlayState(targetState);
    }

    private void PlayState(NPC_ANIM_STATE state)
    {
        switch (state)
        {
            case NPC_ANIM_STATE.Idle:
                animator.CrossFade(IDLE, 0.25f);
                break;
            case NPC_ANIM_STATE.Angry:
                animator.CrossFade(ANGRY, 0.25f);
                break;
            case NPC_ANIM_STATE.Greeting:
                animator.CrossFade(GREETING, 0.25f);
                break;
        }
    }
}

