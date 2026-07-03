using System.Collections.Generic;
using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// 레이저 스킬. UseSkill()로 발동 → chargeTime 동안 차지 → 전방(owner.forward)으로
// beamDuration 동안 빔 유지. 빔 유지 중 damageInterval마다 전방 직선(RaycastNonAlloc)
// 관통 판정으로 사거리 내 적에게 지속 데미지. 조준은 전방 고정.
//
// [관통/막힘] 거리순으로 순회하며 데미지. IHittable.BlocksBeam이 true인 대상
//   (거대몹/소행성 등)을 만나면 데미지를 준 뒤 관통을 멈추고 빔이 거기서 끝남.
//   일반 적은 관통. 벽(IDamageable 아님, BlocksBeam만 true)은 데미지 없이 차폐.
//
// [시각] 판정 결과 길이를 LaserBeamVisual.SetLength()로 넘겨 빔 시각과 데미지 사거리를 일치시킴.
//   빔 프리팹은 beamEffectType으로 VFXManager에서 꺼내며, LaserBeamVisual 컴포넌트를 가져야 함.
//
// 채널링/지속은 코루틴 없이 SkillSystem.Update()가 매 프레임 부르는 UpdateSkill()에서
// Time.time 비교로 처리(WarpSkill과 동일 패턴).
// ================================================================

public class LaserSkill : ActiveSkill
{
	private LaserSkillData Data
	{
		get { return _activeSkillData as LaserSkillData; }
	}

	// 발동 단계
	private enum Phase { None, Charging, Firing }
	private Phase _phase = Phase.None;
	private float _phaseStartTime;   // 현재 단계 시작 시각
	private float _lastDamageTime;   // 마지막 데미지 판정 시각

	// 관통 판정용 버퍼(매 판정마다 new 방지). 동시 관통 대상 상한.
	private readonly RaycastHit[] _hitBuffer = new RaycastHit[32];
	private static readonly int _hitBoxMask = LayerMask.GetMask("HitBox");
	private static readonly HitDistanceComparer _hitComparer = new HitDistanceComparer();
	// 한 틱에서 같은 대상(파츠 HitBox 여러 개 등)에 중복 데미지 방지
	private readonly HashSet<IDamageable> _damagedThisTick = new HashSet<IDamageable>();

	// 발사 중 빔 시각 참조(SetLength로 길이 갱신). VFXManager에서 꺼낸 빔 오브젝트의 컴포넌트.
	private LaserBeamVisual _beamVisual;

	public LaserSkill(Unit owner, LaserSkillData skillData) : base(owner, skillData)
	{
	}

	protected override void UseSkill()
	{
		base.UseSkill();   // 쿨다운/사용횟수 갱신

		_phase = Phase.Charging;
		_phaseStartTime = Time.time;

		// 차지 VFX/사운드//따로 차지 포지션만들것
		_vfx.PlayEffectAtUnit(Data.chargeEffectType, _owner.transform, _owner.skillSystem.LaserChargePosition.position, _owner.transform.rotation, Data.chargeTime);
		if (Data.chargeSoundType != SOUND_TYPE.SFX_NONE)
		{
			_sound.PlaySFX3DAtUnit(Data.chargeSoundType, _owner.transform);
		}
	}

	public override void UpdateSkill()
	{
		switch (_phase)
		{
			case Phase.Charging:
				if (Time.time >= _phaseStartTime + Data.chargeTime)
				{
					StartFiring();
				}
				break;

			case Phase.Firing:
				if (Time.time >= _phaseStartTime + Data.beamDuration)
				{
					_phase = Phase.None;
					_beamVisual = null;   // 빔은 VFXManager duration 타이머로 자동 반납
				}
				else
				{
					UpdateBeam();
				}
				break;
		}
	}

	// 빔 발사 시작 — 빔 VFX/사운드 재생 + 빔 시각 참조 획득, 데미지 틱 리셋
	private void StartFiring()
	{
		_phase = Phase.Firing;
		_phaseStartTime = Time.time;
		_lastDamageTime = float.NegativeInfinity;   // 발사 첫 프레임에 즉시 1틱

		GameObject beam = _vfx.PlayEffectAtUnit(Data.beamEffectType, _owner.transform, _owner.skillSystem.LaserFirePosition.position, _owner.transform.rotation, Data.beamDuration);
		_beamVisual = (beam != null) ? beam.GetComponent<LaserBeamVisual>() : null;
		// 빔 시각 두께 = 판정 반경 × 2(지름). 판정(SphereCast radius)과 눈에 보이는 두께를 일치시킴.
		if (_beamVisual != null)
		{
			_beamVisual.ResetVisualRotation();
			_beamVisual.SetWidth(Data.beamRadius * 2f);
		}

		if (Data.fireSoundType != SOUND_TYPE.SFX_NONE)
		{
			_sound.PlaySFX3DAtUnit(Data.fireSoundType, _owner.transform);
		}
	}

	// 매 프레임 전방 직선 판정. 데미지는 damageInterval 게이트, 빔 길이는 매 프레임 갱신.
	private void UpdateBeam()
	{
		Vector3 origin = _owner.skillSystem.LaserFirePosition.position;  
		Vector3 dir = _owner.transform.forward;

		// 빔 두께(beamRadius)만큼 원통형 판정 — 중심선만 보는 Raycast와 달리 빔 시각 두께에 걸친 적도 잡힘
		int count = Physics.SphereCastNonAlloc(origin, Data.beamRadius, dir, _hitBuffer, Data.range, _hitBoxMask);
		if (count > 1)
		{
			// SphereCastNonAlloc도 거리순 정렬을 보장하지 않음 → 가까운 순으로 정렬해야 관통/막힘 순서가 맞음
			System.Array.Sort(_hitBuffer, 0, count, _hitComparer);
		}

		bool doDamage = Time.time >= _lastDamageTime + Data.damageInterval;
		if (doDamage)
		{
			_lastDamageTime = Time.time;
			_damagedThisTick.Clear();
		}

		float beamEnd = Data.range;   // 막는 게 없으면 최대 사거리까지
		bool blocked = false;         // BlocksBeam 대상에 실제로 막혔는지(허공 max range면 false → 빔끝 impact 끔)

		for (int i = 0; i < count; i++)
		{
			RaycastHit hit = _hitBuffer[i];
			IHittable hittable = hit.collider.GetComponentInParent<IHittable>();
			if (hittable == null)
			{
				continue;
			}

			// 자기 자신 제외
			MonoBehaviour mono = hittable as MonoBehaviour;
			if (mono != null && mono.gameObject == _owner.gameObject)
			{
				continue;
			}

			// 같은 팀 제외 (관통, 데미지 없음) — Unit인 경우만 태그 비교
			Unit unit = hittable as Unit;
			if (unit != null && _owner.CompareTag(unit.tag))
			{
				continue;
			}

			// 틱 게이트: damageInterval마다만 데미지/피격반응 처리
			if (doDamage)
			{
				HitInfo info = new HitInfo
				{
					type = DAMAGE_TYPE.LASER,
					damageAmount = Data.damagePerTick,
					hitPosition = hit.point,
					hitDiriection = dir,
					attacker = _owner.gameObject,
					// 지속 데미지라 히트 사운드는 무음(반복 소음 방지). SFX_NONE 명시 안 하면 enum 0번(BGM) 재생 함정.
					hitSoundType = SOUND_TYPE.SFX_NONE,
					ignoreArmor = Data.ignoreArmor,
					shieldDamageMultiplier = Data.shieldDamageMultiplier,
					hitVfxType = Data.hitEffectType,
					shieldHitVfxType = Data.shieldHitEffectType,
				};

				if (hittable is IDamageable damageable)
				{
					// 데미지 받는 대상(유닛/소행성): 틱당 같은 대상 중복 방지 후 데미지(내부에서 OnHitReaction 호출됨)
					if (_damagedThisTick.Add(damageable))
					{
						damageable.TakeDamage(info);
					}
				}
				else
				{
					// 데미지 안 받는 순수 IHittable(벽/지형 등): 피격 반응(VFX/SFX)만 — 안 그럼 그냥 막히기만 하고 아무것도 안 남
					hittable.OnHitReaction(info);
				}
			}

			// 막는 대상이면 데미지 준 뒤 여기서 빔 끝 + 관통 중단
			if (hittable.BlocksBeam)
			{
				beamEnd = hit.distance;
				blocked = true;
				break;
			}
		}

		// 빔 시각 길이 = 판정 끝점(막힌 지점 또는 최대 사거리) → 시각과 데미지 사거리 일치
		if (_beamVisual != null)
		{
			_beamVisual.SetLength(beamEnd);
			// 실제로 막힌 경우(BlocksBeam)만 빔끝 impact 표시. 허공 max range면 꺼서 공중에 안 뜨게.
			_beamVisual.SetImpactActive(blocked);
		}
	}

	// RaycastHit 거리 오름차순 비교자(관통 순회를 가까운 순으로)
	private class HitDistanceComparer : IComparer<RaycastHit>
	{
		public int Compare(RaycastHit a, RaycastHit b)
		{
			return a.distance.CompareTo(b.distance);
		}
	}
}
