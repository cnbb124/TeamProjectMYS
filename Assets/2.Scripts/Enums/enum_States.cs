public enum UNIT_STATE
{
	IDLE,
	DIE,
	MOVING,//필요한가?
	BOOSTING,//필요한가?
	DODGE,//무적로직 회피
	HIT,//필요한가?
}

public enum UNIT_PLAYER_STATE
{
	DODGE,
	BOOSTING,
	MOVING
}
public enum UNIT_ENEMY_STATE
{
	
	//차후 FSM관련추가?

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

