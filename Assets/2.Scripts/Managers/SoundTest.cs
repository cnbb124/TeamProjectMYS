using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundTest : MonoBehaviour
{
	public float sfx3DDelay;
	public float sfxUiDelay;
	float sfx3DTimer = 0f;
	float sfxUiTimer = 0f;
	SoundManager _soundManager;
	// Start is called before the first frame update
	void Start()
	{
		_soundManager = SoundManager.Instance;
		_soundManager.PlayBGM(SOUND_TYPE.BGM_LOBBY);
	}

	// Update is called once per frame
	void Update()
	{
		//타이머0에서 프레임반환시간더하기
		sfx3DTimer += Time.deltaTime;
		sfxUiTimer += Time.deltaTime;
		//타이머가 딜레이시간이 되면
		if (sfx3DTimer >= sfx3DDelay)
		{
			sfx3DTimer = 0;
			_soundManager.PlaySFX3DAtPosition(SOUND_TYPE.SFX_SHOOT, transform.position);

		}
		if (sfxUiTimer >= sfxUiDelay)
		{
			sfxUiTimer = 0;
			_soundManager.PlaySFXUI(SOUND_TYPE.SFX_UI_CLICK);
		}
	}
}
