using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인벤토리 인스펙터 패널.
///
/// [HitPanel] — 파트 이미지 색상으로 파손 상태 표시
///   이미지마다 "무엇을 기준으로 칠할지(Source)"를 골라 쓴다.
///     PartHp     : 해당 PART_TYPE 파츠의 개별 HP (UnitParts.partSlots의 curPartHp / maxPartHp)
///     UnitHp     : 기체 본체 HP
///     UnitArmor  : 기체 아머
///     UnitShield : 기체 실드
///   → 파츠마다 따로 물들기 때문에 "무기만 빨갛고 엔진은 멀쩡" 같은 상태가 한눈에 보임.
///
///   색 단계 (4단계 고정 — 시인성 위해 그라데이션 안 씀)
///     초록 100~75% / 노랑 75~50% / 주황 50~25% / 빨강 25~0%
///     회색   = 슬롯 비어있음(미장착, PartHp 전용)
///     검붉음 = 파괴됨(HP 0)
///
///   ※ 같은 PART_TYPE 슬롯이 여러 개면 그중 "가장 많이 깎인" 파츠 기준으로 칠한다(경고 목적).
///   ※ FRAME처럼 maxPartHp가 0인 파츠는 자동으로 본체 HP로 대체 표시된다.
///   ※ Body를 UnitArmor로 둔 이유: ARMOR 파츠는 기본 로드아웃에 없지만 유닛은 기본 아머를 가짐.
///      PartHp로 두면 STATUS 패널엔 "아머 150/150"인데 기체는 회색이라 모순돼 보임.
///      나중에 아머 파츠를 기본 지급하기로 하면 Source만 PartHp로 되돌리면 됨.
///
/// [HitPanel 텍스트] Level / Gauge(HP cur-max)
/// [StatPanel]       HP / Shield / Armor 바 + 텍스트
///
/// [부착 / 연결]
/// 1. InspectorPanel에 부착
/// 2. Part Bindings에 항목 추가 → Image + Source(+ PartHp면 PART_TYPE) 지정
///    (파츠가 늘어나도 인스펙터에서 항목만 추가하면 되고 코드 수정 불필요)
/// 3. 구버전 개별 이미지 필드를 쓰던 씬이면 그대로 둬도 됨 —
///    Part Bindings가 비어있으면 시작할 때 구필드에서 자동 생성함.
///    컴포넌트 우클릭 → "파트 바인딩 자동 생성"으로 미리 만들어둘 수도 있음.
/// </summary>
public class InspectorPanelUI : MonoBehaviour
{
    /// <summary>색을 무엇에서 뽑을지.</summary>
    public enum ColorSource
    {
        PartHp,      // 지정한 PART_TYPE 파츠의 개별 HP
        UnitHp,      // 기체 본체 HP
        UnitArmor,   // 기체 아머
        UnitShield,  // 기체 실드
    }

    /// <summary>파트 이미지 1개와 그 이미지가 나타낼 대상의 짝.</summary>
    [System.Serializable]
    public class PartImageBinding
    {
        public Image image;

        [Tooltip("색의 기준. PartHp일 때만 아래 Part Type을 사용함")]
        public ColorSource source = ColorSource.PartHp;

        [Tooltip("Source가 PartHp일 때 볼 파츠 종류")]
        public PART_TYPE partType;
    }

    [Header("References")]
    [SerializeField] private Player player;

    [Header("HitPanel — 파트 이미지 ↔ 파츠 매핑")]
    [SerializeField] private List<PartImageBinding> partBindings = new List<PartImageBinding>();

    [Header("구버전 이미지 필드 (Part Bindings 자동 생성용 — 이관 후 비워도 됨)")]
    [SerializeField] private Image cockpitImage;    // → FRAME (본체 HP)
    [SerializeField] private Image bodyImage;       // → ARMOR
    [SerializeField] private Image engineCoreImage; // → ENGINE
    [SerializeField] private Image thrusterImage;   // → THRUSTER
    [SerializeField] private Image bulletPosImage;  // → LAUNCHER_BULLET
    [SerializeField] private Image missilePosImage; // → LAUNCHER_MISSILE

    [Header("HitPanel — 텍스트")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text gaugeText;   // HP 수치 (cur / max)

    [Header("StatPanel — HP")]
    [SerializeField] private Image    hpFill;
    [SerializeField] private TMP_Text hpText;

    [Header("StatPanel — Shield")]
    [SerializeField] private Image    shieldFill;
    [SerializeField] private TMP_Text shieldText;

    [Header("StatPanel — Armor")]
    [SerializeField] private Image    armorFill;
    [SerializeField] private TMP_Text armorText;

    [Header("단계별 색상")]
    [SerializeField] private Color colorHealthy  = new Color(0.0f, 1.0f, 0.2f); // 초록  100~75%
    [SerializeField] private Color colorCaution  = new Color(1.0f, 0.9f, 0.0f); // 노랑   75~50%
    [SerializeField] private Color colorWarning  = new Color(1.0f, 0.5f, 0.0f); // 주황   50~25%
    [SerializeField] private Color colorCritical = new Color(1.0f, 0.1f, 0.1f); // 빨강   25~ 0%

    [Header("특수 상태 색상")]
    [Tooltip("슬롯이 비어있음(미장착). 파괴와 구분하기 위해 무채색.")]
    [SerializeField] private Color colorEmpty     = new Color(0.35f, 0.35f, 0.35f);
    [Tooltip("파츠 파괴됨(HP 0). 빨강보다 어둡게 해서 '아직 살아있는 위험' 과 구분.")]
    [SerializeField] private Color colorDestroyed = new Color(0.35f, 0.0f, 0.0f);

    private UnitParts _parts;   // player의 UnitParts 캐시

    private void Start()
    {
        BuildBindingsFromLegacyFields();
        Refresh();
    }

    // 구버전 개별 이미지 필드를 Part Bindings로 옮김.
    // 이미 항목이 있으면 손대지 않는다(인스펙터에서 직접 구성한 걸 덮어쓰지 않기 위해).
    [ContextMenu("파트 바인딩 자동 생성")]
    private void BuildBindingsFromLegacyFields()
    {
        if (partBindings != null && partBindings.Count > 0) return;

        partBindings = new List<PartImageBinding>();
        AddBinding(cockpitImage,    ColorSource.UnitHp);                                 // FRAME은 파츠HP 없음
        AddBinding(bodyImage,       ColorSource.UnitArmor);                              // 아머 파츠 미지급이라 유닛 아머로
        AddBinding(engineCoreImage, ColorSource.PartHp, PART_TYPE.ENGINE);
        AddBinding(thrusterImage,   ColorSource.PartHp, PART_TYPE.THRUSTER);
        AddBinding(bulletPosImage,  ColorSource.PartHp, PART_TYPE.LAUNCHER_BULLET);
        AddBinding(missilePosImage, ColorSource.PartHp, PART_TYPE.LAUNCHER_MISSILE);
    }

    private void AddBinding(Image img, ColorSource source, PART_TYPE type = PART_TYPE.FRAME)
    {
        if (img == null) return;
        partBindings.Add(new PartImageBinding { image = img, source = source, partType = type });
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        // 인스펙터 연결 우선, 비어있으면 GameManager.playerRef에서 자동 폴백
        if (player == null && GameManager.Instance != null)
            player = GameManager.Instance.playerRef;

        if (player == null) return;

        UpdatePartImages();
        UpdateHitPanel();
        UpdateStatPanel();
    }

    // ── 파트별 이미지 색상 ────────────────────────────────────

    private void UpdatePartImages()
    {
        // player가 바뀌었으면(리스폰/씬전환) UnitParts 캐시도 다시 잡는다.
        if (_parts == null || _parts.gameObject != player.gameObject)
            _parts = player.GetComponent<UnitParts>();

        foreach (PartImageBinding binding in partBindings)
        {
            if (binding == null || binding.image == null) continue;
            binding.image.color = ResolvePartColor(binding);
        }
    }

    private float Ratio(int cur, int max) => max > 0 ? (float)cur / max : 0f;

    // 바인딩 하나가 나타낼 색을 결정.
    // 미장착 → 회색 / 파괴 → 검붉음 / 그 외 → 남은 비율의 4단계 색.
    private Color ResolvePartColor(PartImageBinding binding)
    {
        float unitHpRatio = Ratio(player.curHpRemaining, player.maxHpRemaining);

        switch (binding.source)
        {
            case ColorSource.UnitHp:
                return RatioToColor(unitHpRatio);
            case ColorSource.UnitArmor:
                return RatioToColor(Ratio(player.curArmorRemaining, player.maxArmor));
            case ColorSource.UnitShield:
                return RatioToColor(Ratio(player.curShieldRemaining, player.maxShieldCapacity));
        }

        // 이하 ColorSource.PartHp
        if (_parts == null) return colorEmpty;

        // 같은 종류 슬롯이 여러 개일 수 있으므로(무기 슬롯 등) 가장 많이 깎인 파츠를 대표로 삼는다.
        bool  found = false;
        float worstRatio = 1f;

        foreach (PartSlotEntry slot in _parts.partSlots)
        {
            if (slot == null || slot.slotType != binding.partType) continue;
            if (slot.equippedPart == null) continue;          // 빈 슬롯은 후보에서 제외

            // maxPartHp가 0인 파츠(FRAME 등)는 파츠 HP 개념이 없음 → 본체 HP로 표시
            if (slot.equippedPart.maxPartHp <= 0) return RatioToColor(unitHpRatio);

            float ratio = (float)slot.curPartHp / slot.equippedPart.maxPartHp;
            if (!found || ratio < worstRatio)
            {
                worstRatio = ratio;
                found = true;
            }
        }

        if (!found)            return colorEmpty;      // 이 종류로 장착된 파츠가 하나도 없음
        if (worstRatio <= 0f)  return colorDestroyed;  // 파괴됨

        return RatioToColor(worstRatio);
    }

    private Color RatioToColor(float ratio)
    {
        if (ratio > 0.75f) return colorHealthy;
        if (ratio > 0.50f) return colorCaution;
        if (ratio > 0.25f) return colorWarning;
        return colorCritical;
    }

    // ── HitPanel 텍스트 ───────────────────────────────────────

    private void UpdateHitPanel()
    {
        if (levelText != null)
            levelText.text = $"Lv. {player.level}";

        if (gaugeText != null)
            gaugeText.text = $"{player.curHpRemaining} / {player.maxHpRemaining}";
    }

    // ── StatPanel 바 ──────────────────────────────────────────

    private void UpdateStatPanel()
    {
        UpdateBar(hpFill,     hpText,     player.curHpRemaining,     player.maxHpRemaining);
        UpdateBar(shieldFill, shieldText, player.curShieldRemaining, player.maxShieldCapacity);
        UpdateBar(armorFill,  armorText,  player.curArmorRemaining,  player.maxArmor);
    }

    private void UpdateBar(Image fill, TMP_Text label, int cur, int max)
    {
        if (fill != null)
            fill.fillAmount = max > 0 ? (float)cur / max : 0f;

        if (label != null)
            label.text = $"{cur} / {max}";
    }
}
