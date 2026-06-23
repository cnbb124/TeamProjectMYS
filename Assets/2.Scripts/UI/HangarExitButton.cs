/*
 * [HangarExitButton]
 * 격납고 Exit(출항) 버튼 핸들러.
 * 버튼을 누르면 격납고를 나가 출항 씬으로 전환할 예정.
 * ※ 출항 씬이 아직 없으므로 실제 씬 로드는 비워둠 (씬 완성되면 OnExit 내부만 채우면 됨)
 *
 * [부착 / 연결]
 * 1. 아무 오브젝트(예: ExitButton 자신)에 이 스크립트 부착
 * 2. ExitButton의 OnClick 이벤트에 이 컴포넌트의 OnExit() 연결
 */

using UnityEngine;
// using UnityEngine.SceneManagement; // 출항 씬 만들면 주석 해제

public class HangarExitButton : MonoBehaviour
{
    [Header("출항 씬 (아직 미정 — 씬 완성 후 입력)")]
    [SerializeField] private string launchSceneName = ""; // 예: "LaunchScene"

    /// <summary>Exit 버튼 OnClick에 연결.</summary>
    public void OnExit()
    {
        // TODO: 출항 씬 완성되면 아래 로직 활성화
        // - 로딩 씬을 거치려면 LoadingManager.NextScene 방식 사용
        //
        // if (!string.IsNullOrEmpty(launchSceneName))
        // {
        //     LoadingManager.NextScene = launchSceneName;
        //     SceneManager.LoadScene("LoadingScene");
        // }

        Debug.Log("[HangarExitButton] 출항 — 아직 씬 미연결 (launchSceneName 비어있음)");
    }
}
