/*
 * [PlayerButtonUI]
 * 싱글/멀티 선택 버튼. NetworkManager로 모드를 설정한 뒤 로딩 씬을 거쳐 각 목적지로 이동.
 * (팀장 오더 기준 구현)
 *
 * [흐름]
 *  싱글 : NetworkManager.StartSingleplayer()  → NextScene = MAP_SELECT  → LOADING_SEQUENCE
 *  멀티 : NetworkManager.ConnectMultiplayer() → NextScene = MULTIPLAYER → LOADING_SEQUENCE
 *  ※ 실제 목적지 로드는 로딩 씬(LoadingManager)이 담당.
 *
 * [부착 / 연결]
 * 1. PlayerButtonUI 오브젝트(패널)에 이 스크립트 부착
 * 2. 싱글 버튼 OnClick → OnClickSingle()
 * 3. 멀티 버튼 OnClick → OnClickMulti()
 */

using UnityEngine;

public class PlayerButtonUI : MonoBehaviour
{
    // 로딩 씬이 읽어갈 목적지 씬
    private const string SingleNextScene = "MAP_SELECT";
    private const string MultiNextScene  = "MULTIPLAYER";

    /// <summary>싱글 플레이 시작 — 싱글 모드 설정 후 맵 선택으로.</summary>
    public void OnClickSingle()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.StartSingleplayer();
        }
        else
        {
            Debug.LogWarning("[PlayerButtonUI] NetworkManager 없음 — 씬에 배치 필요");
        }

        LoadingManager.NextScene = SingleNextScene;
        GameManager.Instance.LoadScene(SCENE_TYPE.LOADING_SEQUENCE);
    }

    /// <summary>멀티 플레이 시작 — 포톤 접속 후 멀티(대기실)로.</summary>
    public void OnClickMulti()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.ConnectMultiplayer();
        }
        else
        {
            Debug.LogWarning("[PlayerButtonUI] NetworkManager 없음 — 씬에 배치 필요");
        }

        LoadingManager.NextScene = MultiNextScene;
        GameManager.Instance.LoadScene(SCENE_TYPE.LOADING_SEQUENCE);
    }
}
