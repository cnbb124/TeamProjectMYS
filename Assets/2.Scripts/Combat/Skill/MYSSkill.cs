using System.Collections.Generic;
using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// 미사일 연발(사일로) 스킬. UseSkill()에서 사일로 개방 애니메이션(LauncherAnim.PlayOpen())을
// 트리거 → MYSSkillData.siloOpenTime만큼 대기(개방 연출) → fireDurationTime 동안
// missileCount발을 균등 간격으로 발사. 코루틴 없이 Time.time 비교(WarpSkill과 동일 방식).
//
// 발사위치(_siloPositions)는 SkillSystem.MysSiloPositions(고정, 장비 파츠와 무관한 사일로
// 하드포인트)에서 생성 시점에 캐싱 — WeaponSystem의 미사일 발사위치(파츠 장착/해제로 변하는 값)는
// 쓰지 않음(스킬이 무기 장비 상태에 의존하지 않게 하기 위함).
// 타겟은 WeaponSystem.lockOnSystem.TargetsInLockonRange(거리순 정렬)에서 라운드로빈 배정.
// ================================================================

public class MYSSkill : ActiveSkill
{
	private MYSSkillData MYSSkillData

	{
		get
		{
			return _activeSkillData as MYSSkillData;
		}
	}



	// 테스트용. 데이터로옮김
	////발사할 미사일 수
	//private int _missileCount;

	////발사할 타입
	//private SKILL_MSY_TYPE msyType;

	private List<Transform> _siloPositions;
	private Dictionary<Transform, LauncherAnim> _siloAnims = new Dictionary<Transform, LauncherAnim>();
	private int _fireIndex;
	private int _targetIndex;

	private bool _isOpening;
	private float _openStartTime;

	private bool _isFiring;
	private float _fireStartTime;
	private int _firedCount;

	public MYSSkill(Unit owner, MYSSkillData skillData) : base(owner, skillData)
	{
		_siloPositions = new List<Transform>(owner.skillSystem.MysSiloPositions);
		foreach (Transform pos in _siloPositions)
		{
			LauncherAnim anim = pos.GetComponentInParent<LauncherAnim>();
			if (anim != null)
			{
				_siloAnims[pos] = anim;
			}
		}
	}

	/// <summary>
	/// 스킬 사용. 사일로 개방 애니메이션 트리거 후 오프닝 타이머 시작(실제 발사는 UpdateSkill에서).
	/// </summary>
	protected override void UseSkill()
	{
		base.UseSkill();

		foreach (LauncherAnim anim in _siloAnims.Values)
		{
			anim.PlayOpen();
		}

		_isOpening = true;
		_openStartTime = Time.time;
	}

	public override void UpdateSkill()
	{
		if (_isOpening)
		{
			// siloOpenTime(개방 연출 시간)이 아직 안 끝났으면 대기.
			if (Time.time < _openStartTime + MYSSkillData.siloOpenTime)
			{
				return;
			}

			_isOpening = false;
			_isFiring = true;
			_fireStartTime = Time.time;
			_firedCount = 0;
			_targetIndex = 0;
			_fireIndex = 0;
		}

		if (_isFiring)
		{
			UpdateFiring();
		}
	}

	/// <summary>
	/// fireDurationTime 동안 missileCount발을 균등 간격으로 발사(첫발 즉시, 마지막발이 fireDurationTime 시점).
	/// </summary>
	private void UpdateFiring()
	{
		int totalCount = Mathf.Max(1, MYSSkillData.missileCount);
		float interval = totalCount > 1 ? MYSSkillData.fireDurationTime / (totalCount - 1) : 0f;

		if (Time.time >= _fireStartTime + interval * _firedCount)
		{
			FireOneMissile();
			_firedCount++;
		}

		if (_firedCount >= totalCount)
		{
			_isFiring = false;
		}
	}

	/// <summary>
	/// 사일로 1곳에서 미사일 1발 발사. msyType에 따라 HOMING(타겟 1개)/CLUSTER(타겟 전체 배정) 분기.
	/// </summary>
	private void FireOneMissile()
	{
		if (_siloPositions == null || _siloPositions.Count == 0)
		{
			return;
		}

		MissileData data = MYSSkillData.missileData;
		if (data == null)
		{
			return;
		}

		Transform firePos = _siloPositions[_fireIndex];
		_fireIndex = (_fireIndex + 1) % _siloPositions.Count;

		if (_siloAnims.TryGetValue(firePos, out LauncherAnim anim))
		{
			anim.PlayFire();
		}

		Projectile proj = _pool.GetProjectile(data.curMissilePoolType);
		Missile missile = proj as Missile;
		if (missile == null)
		{
			return;
		}

		_vfx.PlayEffectAtUnit(data.muzzleEffectType, _owner.transform, firePos.position, firePos.rotation, 0.2f);
		_sound.PlaySFX3DAtUnit(data.shootSoundType, _owner.transform, firePos);

		List<Transform> targets = _owner.weaponSystem.lockOnSystem.TargetsInLockonRange;

		switch (MYSSkillData.msyType)
		{
			case SKILL_MSY_TYPE.HOMING:
				missile.Init(firePos.position, firePos.forward, _owner, GetNextTarget(targets));
				break;

			case SKILL_MSY_TYPE.CLUSTER:
				ClusterMissile cluster = proj as ClusterMissile;
				if (cluster != null)
				{
					cluster.Init(firePos.position, firePos.forward, _owner, targets);
				}
				else
				{
					missile.Init(firePos.position, firePos.forward, _owner);
				}
				break;
		}
	}

	// TargetsInLockonRange(거리순 정렬)에서 라운드로빈으로 1개씩 배정. 타겟 없으면 null(직진).
	private Transform GetNextTarget(List<Transform> targets)
	{
		if (targets == null || targets.Count == 0)
		{
			return null;
		}

		Transform target = targets[_targetIndex % targets.Count];
		_targetIndex++;
		return target;
	}
}
