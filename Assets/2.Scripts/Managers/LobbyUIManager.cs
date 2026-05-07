using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // 씬 전환 기능을 쓰려면 이 녀석이 꼭 필요해!

public class LobbyUIManager : MonoBehaviour
{
    // '서버 검색' 버튼을 딱! 눌렀을 때 실행될 함수야
    public void OnClickServerSearchButton()
    {
        // "ServerListUI" 씬으로 슝~ 넘어가게 해주는 핵심 코드!
        SceneManager.LoadScene("ServerListUI");
    }
}
