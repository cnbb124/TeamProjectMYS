using System.Collections.Generic;
using UnityEngine;

// ================================================================
// [PART_TYPE 별 담당 스탯 정리]
// ================================================================
//
// FRAME
//   - HP_MAX (statBonuses로 기체 본체 HP 제공, 레벨 기본HP에 추가)
//   - providedSlots (이 프레임이 허용하는 파츠 슬롯 종류 결정)
//   - 무게 적재 한도 제공 (미구현 — weight 시스템 추가 예정)
//   - maxPartHp = 0 고정 (파츠 피격 시스템 제외)
//   - 50% 패널티: 없음 (유닛 본체 HP 그 자체)
//
// ENGINE
//   - SHIELD_REGEN_RATE (실드 회복률)
//   - BOOST_MAX        (최대 부스트량)
//   - BOOST_REGEN_RATE (부스트 회복률)
//   - FUEL_MAX         (최대 연료)
//   - 무게 적재 한도 제공 (미구현)
//   - 50% 패널티: 위 스탯 모두 50%
//
// ARMOR
//   - ARMOR_MAX (최대 아머량)
//   - ARMOR_DEF (데미지 경감 수치)
//   - 50% 패널티: 방어력 50%
//
// THRUSTER  (정방향 추진기)
//   - MOVE_SPEED_BASE  (가속도)
//   - MOVE_SPEED_MAX   (최대 속도)
//   - MOVE_SPEED_BOOST (부스트 속도)
//   - 50% 패널티: 위 스탯 모두 50%
//
// THRUSTER_REVERSE  (역추진기)
//   - 담당 스탯 미정
//   - 없을 시 패널티: 감속 불량 (입력 해제 시 관성 유지) + S키 후진 불가
//
// THRUSTER_SIDE  (측면 추진기)
//   - 담당 스탯 미정
//   - 없을 시 패널티: A/D 좌우 이동 불가
//
// LAUNCHER_BULLET
//   - 연사속도 (fireBulletDelay)
//   - CRI_RATE    (치명타 확률)
//   - CRI_DMG_MULT (치명타 데미지 배율)
//   - 50% 패널티: 연사속도 50%
//
// LAUNCHER_MISSILE
//   - 연사속도 (fireMissileDelay)
//   - CRI_RATE
//   - CRI_DMG_MULT
//   - 50% 패널티: 연사속도 50%
//
// LAUNCHER_LASER
//   - 폐기됨 (레이저는 스킬로 전환 확정. WeaponSystem 발사 파이프라인 정리 완료.
//     enum 값 자체는 직렬화 데이터 보호 위해 남겨둠 — 이 파츠로 새 PartData 만들지 말 것)
//
// SHIELD_BATTERY  (추가 예정 — enum_Types.cs PART_TYPE에 미등록)
//   - SHIELD_MAX (최대 실드량)
//   - 50% 패널티: 미정
//
// ================================================================
// [무게 시스템] — 확정, 미구현
//   - 파츠마다 weight 수치 보유 (PartData에 weight 필드 추가 필요)
//   - FRAME + ENGINE 합산이 적재 한도 제공
//   - 한도 초과 시 패널티 적용 (장착 자체는 허용)
// ================================================================

[System.Serializable]
public class PartStatBonus
{
    public STAT_TYPE statType;
    public float value;
}

[CreateAssetMenu(fileName = "New Part Data", menuName = "Create Data/Item/Part Data")]
public class PartData : ItemData
{
    [Header("Part")]
    //public string partID;// enum_ID로 변경해서 안씀
    public PART_TYPE partType;

    [Header("장착시 기체에 붙일 프리펩(비주얼), 툴팁확인")]
    [Tooltip("LAUNCHER류는 WeaponFirePos필수")]
    public GameObject partPrefab;

    [Tooltip("기체 중심 기준 장착 오프셋. 플레이어 프리팹에 임시 배치해서 localPosition 값 옮겨오기.")]
    public Vector3 mountOffset;

    [Header("Frame Slots (FRAME 파츠 전용)")]
    [Tooltip("이 프레임이 제공하는 파츠 슬롯 목록. FRAME 타입 파츠에만 설정.")]
    public List<PART_TYPE> providedSlots = new List<PART_TYPE>();

    [Header("Part HP 프레임 제외")]
    [Tooltip("이 파츠의 최대 HP. 0이면 HP 없음(FRAME은제외).")]
    public int maxPartHp = 100;

    [Header("Stat Bonuses")]
    public List<PartStatBonus> statBonuses = new List<PartStatBonus>();
}
