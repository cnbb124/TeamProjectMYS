using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // 씬 전환 기능에 필요

public class LobbyUIManager : MonoBehaviour
{
    // 서버검색 버튼 눌렀을시 실행될 함수
    public void OnClickServerSearchButton()
    {
        // ServerListUI 씬으로 넘어가게 게임매니저의 로드씬을 호출하도록 변경할것
        //SceneManager.LoadScene("ServerListUI");
        //변경완료
        GameManager.Instance.LoadScene("ServerListUI");
    }

	public void OnClickNewGame()
	{
		GameManager.Instance.LoadScene("Test"); // 또는 SCENE_TYPE.STAGE1
	}
}
