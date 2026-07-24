/*
 * [ShipPartDamageView]
 * 기체 도식 이미지의 각 부위를 파츠 손상 상태에 따라 색으로 칠하는 컴포넌트.
 * InspectorPanelUI의 파츠 색상 로직만 떼어낸 것 — 격납고 PlanePanel처럼
 * "기체 그림만 있고 스탯 바/텍스트는 없는" 곳에 재사용하려고 분리했다.
 *
 * [부착 / 연결]
 * 1. 기체 도식이 있는 오브젝트(예: 격납고 PlanePanel)에 부착
 * 2. 아래 6개 이미지 필드에 부위 이미지를 연결
 *    (Cockpit/Body/EngineCore/Thruster/BulletPos/MissilePos — InspectorPanelUI와 동일 구조)
 *    → 시작할 때 Part Bindings로 자동 변환됨. 직접 Part Bindings를 채워도 됨.
 * 3. player는 비워두면 GameManager.playerRef에서 자동으로 잡음
 *
 * [색 규칙] (4단계 고정, 시인성 위해 그라데이션 안 씀)
 *   초록 100~75% / 노랑 75~50% / 주황 50~25% / 빨강 25~0%
 *   회색   = 미장착(PartHp 전용)  /  검붉음 = 파괴(HP 0)
 *
 * ※ InspectorPanelUI와 색 로직이 겹친다. 나중에 하나로 합칠 수 있으나,
 *   인벤토리 인스펙터가 잘 도는 걸 건드리지 않으려고 지금은 독립 컴포넌트로 둔다.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShipPartDamageView : MonoBehaviour
{
    /// <summary>색을 무엇에서 뽑을지.</summary>
    public enum ColorSource
    {
        PartHp,      // 지정한 PART_TYPE 파츠의 개별 HP
        UnitHp,      // 기체 본체 HP
        UnitArmor,   // 기체 아머
        UnitShield,  // 기체 실드
    }

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

    [Header("부위 이미지 ↔ 파츠 매핑")]
    [SerializeField] private List<PartImageBinding> partBindings = new List<PartImageBinding>();

    [Header("구필드 (Part Bindings 자동 생성용 — 이관 후 비워도 됨)")]
    [SerializeField] private Image cockpitImage;    // → UnitHp
    [SerializeField] private Image bodyImage;       // → UnitArmor
    [SerializeField] private Image engineCoreImage; // → ENGINE
    [SerializeField] private Image thrusterImage;   // → THRUSTER
    [SerializeField] private Image bulletPosImage;  // → LAUNCHER_BULLET
    [SerializeField] private Image missilePosImage; // → LAUNCHER_MISSILE

    [Header("단계별 색상")]
    [SerializeField] private Color colorHealthy  = new Color(0.0f, 1.0f, 0.2f);
    [SerializeField] private Color colorCaution  = new Color(1.0f, 0.9f, 0.0f);
    [SerializeField] private Color colorWarning  = new Color(1.0f, 0.5f, 0.0f);
    [SerializeField] private Color colorCritical = new Color(1.0f, 0.1f, 0.1f);

    [Header("특수 상태 색상")]
    [SerializeField] private Color colorEmpty     = new Color(0.35f, 0.35f, 0.35f);
    [SerializeField] private Color colorDestroyed = new Color(0.35f, 0.0f, 0.0f);

    private UnitParts _parts;

    private void Start()
    {
        BuildBindingsFromLegacyFields();
    }

    private void Update()
    {
        if (player == null && GameManager.Instance != null)
            player = GameManager.Instance.playerRef;
        if (player == null) return;

        if (_parts == null || _parts.gameObject != player.gameObject)
            _parts = player.GetComponent<UnitParts>();

        foreach (PartImageBinding binding in partBindings)
        {
            if (binding == null || binding.image == null) continue;
            binding.image.color = ResolvePartColor(binding);
        }
    }

    [ContextMenu("파트 바인딩 자동 생성")]
    private void BuildBindingsFromLegacyFields()
    {
        if (partBindings != null && partBindings.Count > 0) return;

        partBindings = new List<PartImageBinding>();
        AddBinding(cockpitImage,    ColorSource.UnitHp);
        AddBinding(bodyImage,       ColorSource.UnitArmor);
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

    private float Ratio(int cur, int max) => max > 0 ? (float)cur / max : 0f;

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

        bool  found = false;
        float worstRatio = 1f;

        foreach (PartSlotEntry slot in _parts.partSlots)
        {
            if (slot == null || slot.slotType != binding.partType) continue;
            if (slot.equippedPart == null) continue;

            if (slot.equippedPart.maxPartHp <= 0) return RatioToColor(unitHpRatio);

            float ratio = (float)slot.curPartHp / slot.equippedPart.maxPartHp;
            if (!found || ratio < worstRatio)
            {
                worstRatio = ratio;
                found = true;
            }
        }

        if (!found)           return colorEmpty;
        if (worstRatio <= 0f) return colorDestroyed;

        return RatioToColor(worstRatio);
    }

    private Color RatioToColor(float ratio)
    {
        if (ratio > 0.75f) return colorHealthy;
        if (ratio > 0.50f) return colorCaution;
        if (ratio > 0.25f) return colorWarning;
        return colorCritical;
    }
}
