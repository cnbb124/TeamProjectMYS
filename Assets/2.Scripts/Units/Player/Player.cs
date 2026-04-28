using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : Unit
{
	// =====================[추가]======================
	// 총알 교대 발사용 인덱스 (0=왼쪽, 1=오른쪽 → 0→1→0 순환)
	// bulletFirePos는 Unit에 있는 배열 그대로 사용
	// missileFirePos, laserFirePos도 Unit 그대로 사용
	private int _bulletFireIndex = 0;
	// =================================================

	// Start is called before the first frame update
	protected override void Start()
	{
		base.Start(); //유닛 초기화 호출
	}

	// Update is called once per frame
	protected override void Update()
	{
		base.Update(); //FSM, 실드/부스트 회복 호출
					   // 입력처리 - InputManager 구현 뒤 여기서 호출
					   // ex. InputManager.Instance.HandleInput(this);
	}



	//===============자식에서 직접 override==================
	protected override void OnStateEnter(UNIT_STATE state)
	{
		switch (state)
		{
			case UNIT_STATE.DIE:
				Debug.Log("[Player] 사망");
				// GameManager.Instance.OnPlayerDie();
				break;
		}
	}
	protected override void OnStateExit(UNIT_STATE state) { }
	protected override void OnIdle() { }
	protected override void OnMoving() { }
	protected override void OnDodge() { }
	protected override void OnDying() { }

	// 피격 반동 - 카메라 쉐이크, 넉백 등
	protected override void OnHitReaction(DamageInfo info)
	{
		// 크리티컬이면 강한 쉐이크
		// ex. if (info.isCritical) CameraShake.Strong(); else CameraShake.Light();
	}

	// 사망처리
	protected override void Die()
	{
		// ex. GameManager.Instance.OnPlayerDie();
	}

	// ===========================================
	// Shoot - Unit의 배열 총구 사용
	// 총알: 좌우 교대 고정
	// 미사일: 좌/우/동시 선택
	// 레이저: 머리 중앙 고정
	// ALL: 전체 동시
	// =================================================

	//사격및 소리재생
	public override void Shoot(SHOOT_TYPE type)
	{
		_playSoundType = GetPlaySoundType(type);
		switch (type)
		{
			case SHOOT_TYPE.BULLET:
				_soundManager.PlaySFX3DAtPosition(_playSoundType, transform.position, 0.9f, 1.1f);
				ShootBullet();
				break;
			case SHOOT_TYPE.LASER:
				_soundManager.PlaySFX3DAtPosition(_playSoundType, transform.position);
				ShootLaser();
				break;
			case SHOOT_TYPE.MISSILE_LEFT:
				_soundManager.PlaySFX3DAtPosition(_playSoundType, transform.position);
				ShootMissile(FIREPOS_TYPE.MISSILE_LEFT);
				break;
			case SHOOT_TYPE.MISSILE_RIGHT:
				_soundManager.PlaySFX3DAtPosition(_playSoundType, transform.position);
				ShootMissile(FIREPOS_TYPE.MISSILE_RIGHT);
				break;
			case SHOOT_TYPE.MISSILE_BOTH://소리클경우 양쪽말고 한쪽이나 중간점 으로 따로만들어서진행
				_soundManager.PlaySFX3DAtPosition(_playSoundType, GetFirePos(FIREPOS_TYPE.MISSILE_LEFT).position);
				_soundManager.PlaySFX3DAtPosition(_playSoundType, GetFirePos(FIREPOS_TYPE.MISSILE_RIGHT).position);
				ShootMissile(FIREPOS_TYPE.MISSILE_LEFT);
				ShootMissile(FIREPOS_TYPE.MISSILE_RIGHT);
				break;
			case SHOOT_TYPE.ALL:
				_soundManager.PlaySFX3DAtPosition(GetPlaySoundType(SHOOT_TYPE.BULLET), transform.position, 0.9f, 1.1f);
				_soundManager.PlaySFX3DAtPosition(GetPlaySoundType(SHOOT_TYPE.MISSILE_LEFT), transform.position);
				_soundManager.PlaySFX3DAtPosition(GetPlaySoundType(SHOOT_TYPE.LASER), transform.position);
				ShootBullet();
				ShootMissile(FIREPOS_TYPE.MISSILE_LEFT);
				ShootMissile(FIREPOS_TYPE.MISSILE_RIGHT);
				ShootLaser();
				break;
		}
	}

	// =====================[추가]======================
	// 총알 - 좌우 교대 발사
	// bulletFirePos[0]=왼쪽, bulletFirePos[1]=오른쪽
	// =================================================
	//사격시 사용메서드
	private void ShootBullet()
	{
		FIREPOS_TYPE[] bulletTypes = { FIREPOS_TYPE.BULLET_LEFT, FIREPOS_TYPE.BULLET_RIGHT };
		curFirePos = GetFirePos(bulletTypes[_bulletFireIndex]);
		if (curFirePos == null)
		{
			return;
		}
		_bulletFireIndex = (_bulletFireIndex + 1) % bulletTypes.Length; // 좌우 순환

		//발사로직필요// poolmanager 구현 뒤 넣기
		//ex.PoolManager.Instance.GetBullet(curFirePos.position, curFirePos.forward, this);
	}



	// =====================[추가]======================
	// 미사일 - 타입으로 좌우선택
	//
	// =================================================
	private void ShootMissile(FIREPOS_TYPE firePosType)
	{
		curFirePos = GetFirePos(firePosType);
		if (curFirePos == null)
		{
			return;
		}

		//발사로직필요// poolmanager 구현 뒤 넣기
		//ex.PoolManager.Instance.GetMissile(curFirePos.position, curFirePos.forward, this);
	}

	// =====================[추가]======================
	// 레이저 - 머리 중앙 고정 (laserFirePos 단일 Transform)
	// =================================================
	private void ShootLaser()
	{

		curFirePos = GetFirePos(FIREPOS_TYPE.LASER);

		//발사로직필요// poolmanager 구현 뒤 넣기
		//ex.PoolManager.Instance.GetLaser(curFirePos.position, curFirePos.forward, this);
	}
}
