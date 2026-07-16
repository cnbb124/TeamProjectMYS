using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MasSelectorUI : MonoBehaviour
{
	public void OnClickButtonMap1()
	{
		LoadingManager.NextScene = "STAGE1";
		GameManager.Instance.LoadScene(SCENE_TYPE.LOADING_SEQUENCE);


	}
	public void OnClickButtonMap2()
	{
		//LoadingManager.NextScene = "STAGE2";
		//GameManager.Instance.LoadScene(SCENE_TYPE.LOADING_SEQUENCE);
	}
}
