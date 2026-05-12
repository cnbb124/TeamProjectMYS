public enum LAYER_TYPE
{
	Default = 0,
	TransparentFX = 1,
	IgnoreRaycast = 2,
	None = 3,
	Water = 4,
	UI = 5,
	Unit_Player = 6,
	Unit_Enemy = 7,
	Projectile_Player = 8,
	Projectile_Enemy = 9,
	Environment = 10,
}


//������
public enum ENEMY_TYPE
{
	DROPSHIP,
	GUNSHIP,
	MISSLIESHIP,
	BOSS,
}
//��������
public enum DAMAGE_TYPE
{
	BULLET, //�Ѿ�
	LASER, //������(��ų�� �����ϰų� ��ų���̰ɷ�)
	EXPLOSION, //������(�̻���)
	CONTACT, //�浹��(��ġ��)
}

//�������(����ü)
public enum PROJECTILE_TYPE
{
	BULLET, //�Ѿ�
	LASER, //������(��ų�� �����ϰų� ��ų���̰ɷ�)

	//(�̻���)
	MISSILE,
	//MISSILE_BOTH,//����̻��

	//��ü����
	ALL,//�ʿ��Ѱ�?

}

//�ѱ�����
public enum FIREPOS_TYPE
{
	BULLET_LEFT,
	BULLET_RIGHT,
	MISSILE_LEFT,
	MISSILE_RIGHT,
	LASER,
}
public enum BOOSTPOS_TYPE
{
	LEFT,
	RIGHT,
	FRONT,
	BACK,
}

public enum SOUND_TYPE
{
	BGM_LOBBY,          // ������(����) �����
	BGM_BATTLE,         // ���� ���� �����
						//UI����
	SFX_DICE_ROLL,      // �ֻ��� ������ �Ҹ�
	SFX_UI_CLICK,       // ��ư Ŭ����
						// �߻���
	SFX_BULLETSHOOT,    //ź
	SFX_MISSILESHOOT,   //�̻���
	SFX_LASERSHOOT,     //������
						// �ǰ���
	SFX_BULLETHIT,      //ź
	SFX_EXPLOSION,      // �̻��ϵ� ������
	SFX_CONTACTSHIP,    //�ε�������.
	SFX_CONTACTGROUND,  //�༺�� �ε�������. ���� �ʵ�� �����Ҽ�����
	SFX_LASERHIT,       //������


	SFX_NONE,//��ż�����
}