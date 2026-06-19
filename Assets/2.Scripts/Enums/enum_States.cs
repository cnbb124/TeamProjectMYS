// =====================================================================
// [UNIT_STATE] — "지금 어떤 상태인가" (표현/물리 레이어)
//
// 애니메이션, 이펙트, 무적, Rigidbody 처리가 이 값에 의존.
// OnStateEnter/OnStateExit에서 전환 시 1회 처리.
// UpdateFSM()에서 매 프레임 OnIdle/OnMoving 등 호출.
//
//  ▶ Player : 입력이 직접 CurState를 설정
//  ▶ Enemy  : AI_STATE(행동 의도)가 UpdateAI() 끝에 단방향 동기화
//             AI_STATE.STANDBY → IDLE / 나머지 → MOVING
//             단, DODGE/DIE 중에는 동기화 스킵 — Unit FSM이 우선권 가짐
//
// ※ AI_STATE(Enemy.cs)와 혼동 주의 — 아래 참고
//    AI_STATE = "무엇을 할지" (행동 의도, Enemy 전용)
//    UNIT_STATE = "어떤 상태인지" (표현, Player/Enemy 공통)
// =====================================================================

/// <summary>
/// UNIT_STATE — "지금 어떤 상태인가" (표현/물리 레이어)
/// </summary>
public enum UNIT_STATE
{
	IDLE,       // 제자리 정지. 적: AI_STATE.STANDBY일 때 동기화됨
	DIE,        // 사망. TakeDamage()에서 HP 0 되면 진입. AI 동기화 스킵
	MOVING,     // 이동 중. 적: PATROL/CHASE/ATTACK일 때 동기화됨
	BOOSTING,   // 부스트 중. Player 전용 — AI_STATE에 대응 값 없음
	DODGE,      // 회피 무적 중. 진행 중엔 AI 동기화 스킵. 끝나면 자동 복귀
	HIT,        // 피격 리액션 (현재 미사용)
}


// AI 행동 상태. Unit.CurState(UNIT_STATE)와 별개로, "무엇을 할지"를 결정하는 상태.
// STANDBY(정지) 외에는 전부 이동하므로, UpdateAI()에서 CurState(UNIT_STATE)로 자동 매핑됨: STANDBY->IDLE, 나머지->MOVING혹은 그외 상황맞춰.
// 차후 주석추가
public enum AI_STATE
{
	STANDBY,
	PATROL,
	CHASE,
	ATTACK_HOLD,
	ATTACK_CHASE,
	ATTACK_PASS,
	REPOSITION,
	EVADE,
	DODGE,
	RELOAD,
}
public enum GAME_STATE
{
	MAIN_MENU,
	UI_MENU,
	PLAYING,
	PAUSED,
	GAME_OVER,
	CLEAR,
}

