using System.Collections.Generic;
using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// 레이저 스킬. UseSkill()로 발동 → chargeTime 동안 차지 → 전방(owner.forward)으로
// beamDuration 동안 빔 유지. 빔 유지 중 damageInterval마다 전방 직선(RaycastNonAlloc)
// 관통 판정으로 사거리 내 적에게 지속 데미지. 조준은 전방 고정.
//
// [관통/막힘] 거리순으로 순회하며 데미지. IHittable.BlocksLineOfSight가 true인 대상
//   (거대몹/소행성 등)을 만나면 데미지를 준 뒤 관통을 멈추고 빔이 거기서 끝남.
//   일반 적은 관통. 벽(IDamageable 아님, BlocksLineOfSight만 true)은 데미지 없이 차폐.
//   실드가 켜진 유닛도 빔을 막음(BlocksThisBeam) — 실드 껍질에서 빔이 끊겨야 이펙트가 맞아 보이기 때문.
//
// [사운드] 차지음/발사음 모두 루프(PlaySFX3DLoop)로 재생하고 단계가 바뀔 때 끔.
//   차지 종료 → 차지음 정지 + 발사음 시작, beamDuration 만료 → 발사음 정지.
//   단발 재생이면 클립 길이에 묶여 chargeTime/beamDuration과 안 맞고 두 소리가 겹침.
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
		// 루프로 재생 — 단발이면 클립 길이대로 끝나버려서 chargeTime과 안 맞음. 차지 종료 시 StartFiring에서 끔
		if (Data.chargeSoundType != SOUND_TYPE.SFX_NONE)
		{
			_sound.PlaySFX3DLoop(Data.chargeSoundType, _owner.transform);
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
					StopFireSound();
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

		// 차지음을 끄고 발사음으로 교체 — 겹쳐서 같이 울리지 않게 순서가 중요함
		StopChargeSound();
		if (Data.fireSoundType != SOUND_TYPE.SFX_NONE)
		{
			_sound.PlaySFX3DLoop(Data.fireSoundType, _owner.transform);
		}
	}

	// 스킬 강제 중단 — SkillSystem.OnDisable()이 호출(유닛 사망/풀 반납).
	// 루프 사운드는 명시적으로 꺼야 하고, 안 끄면 activeLoopSounds 등록이 그대로 남음.
	// 빔 VFX는 VFXManager duration 타이머가 알아서 반납하므로 참조만 놓음.
	public override void StopSkill()
	{
		StopChargeSound();
		StopFireSound();
		_phase = Phase.None;
		_beamVisual = null;
	}

	// 차지음 정지. 차지가 끝났거나 스킬이 끊겼을 때 호출.
	private void StopChargeSound()
	{
		if (Data.chargeSoundType != SOUND_TYPE.SFX_NONE)
		{
			_sound.StopSFX3DLoop(Data.chargeSoundType, _owner.transform);
		}
	}

	// 발사음 정지. beamDuration 만료 시 호출 — 빔이 꺼졌는데 소리만 남는 것 방지.
	private void StopFireSound()
	{
		if (Data.fireSoundType != SOUND_TYPE.SFX_NONE)
		{
			_sound.StopSFX3DLoop(Data.fireSoundType, _owner.transform);
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

		// 데미지 판정은 소유자 클라에서만(_hasDamageAuthority). 원격 복제 빔은 시각(길이/막힘)만 갱신하고 데미지/카메라쉐이크는 스킵.
		bool doDamage = _hasDamageAuthority && Time.time >= _lastDamageTime + Data.damageInterval;
		if (doDamage)
		{
			_lastDamageTime = Time.time;
			_damagedThisTick.Clear();
			// 주포 지속 떨림 — 데미지 틱마다 약한 카메라 흔들림(플레이어 위치 기준, 거리 감쇠 거의 없음)
			CameraShaker.Instance?.ShakeAt(_owner.transform.position, Data.cameraShakeStrength);
		}

		float beamEnd = Data.range;   // 막는 게 없으면 최대 사거리까지
		bool blocked = false;         // 막는 대상에 실제로 막혔는지(허공 max range면 false → 빔끝 impact 끔)

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

				//// [임시 진단] LaserHit 안 뜨는 원인 확인 — 위치(hit.point)/실드상태/선택 vfx 로그
				//Unit dbgUnit = hittable as Unit;
				//Debug.Log($"[LaserHit-DEBUG] collider={hit.collider.name}, hitPoint={hit.point}, dist={hit.distance:F2}, " +
				//	$"isDamageable={hittable is IDamageable}, shield={(dbgUnit != null ? dbgUnit.curShieldRemaining : -1)}, " +
				//	$"hitVfx={Data.hitEffectType}, shieldHitVfx={Data.shieldHitEffectType}");

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
			if (BlocksThisBeam(hittable))
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
			// 실제로 막힌 경우(BlocksThisBeam)만 빔끝 impact 표시. 허공 max range면 꺼서 공중에 안 뜨게.
			_beamVisual.SetImpactActive(blocked);
		}
	}

	// 이 대상에서 빔이 멈추는지. 공통 차폐(BlocksLineOfSight)에 더해 실드가 켜진 유닛도 막음.
	// 실드가 있으면 Unit.UpdateShieldHitboxState()가 본체 HitBox를 끄고 실드 콜라이더만 켜므로
	// 레이가 먼저 맞는 게 실드 표면임 → 빔이 실드 껍질에서 끊기고 impact도 거기 찍힘.
	// 락온 차폐(LockOnSystem)는 이 조건을 안 씀 — 실드 켠 적이 뒤의 적을 가리면 안 되기 때문.
	private static bool BlocksThisBeam(IHittable hittable)
	{
		if (hittable.BlocksLineOfSight)
		{
			return true;
		}

		Unit unit = hittable as Unit;
		return unit != null && unit.curShieldRemaining > 0;
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
