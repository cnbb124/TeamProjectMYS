/*
 * [HangarExitButton]
 * 격납고 Exit(출항) 버튼 핸들러.
 * 버튼을 누르면 ShipLanding의 이륙 연출(PlayTakeoff)을 재생하고,
 * 연출이 끝나면(OnTakeoffComplete) 출항 씬으로 전환할 예정.
 * ※ 출항 씬이 아직 없으므로 실제 씬 로드는 비워둠 (씬 완성되면 LoadLaunchScene 내부만 채우면 됨)
 *
 * [부착 / 연결]
 * 1. 아무 오브젝트(예: ExitButton 자신)에 이 스크립트 부착
 * 2. shipLanding 필드에 착지선 오브젝트(ShipLanding 컴포넌트) 드래그
 * 3. ExitButton의 OnClick 이벤트에 이 컴포넌트의 OnExit() 연결
 */
using UnityEngine;
// using UnityEngine.SceneManagement; // 출항 씬 만들면 주석 해제

public class HangarExitButton : MonoBehaviour
{
    [Header("이륙 연출을 담당하는 ShipLanding")]
    [SerializeField] private ShipLanding shipLanding;

    [Header("출항 씬 (아직 미정 — 씬 완성 후 입력)")]
    [SerializeField] private string launchSceneName = ""; // 예: "LaunchScene"

    private bool exitTriggered = false; // 버튼 중복 클릭 방지

    private void OnEnable()
    {
        if (shipLanding != null)
            shipLanding.OnTakeoffComplete += HandleTakeoffComplete;
    }

    private void OnDisable()
    {
        if (shipLanding != null)
            shipLanding.OnTakeoffComplete -= HandleTakeoffComplete;
    }

    /// <summary>Exit 버튼 OnClick에 연결.</summary>
    public void OnExit()
    {
        if (exitTriggered) return; // 이미 출항 진행 중이면 무시
        if (shipLanding == null)
        {
            Debug.LogWarning("[HangarExitButton] shipLanding이 연결되어 있지 않습니다.");
            return;
        }

        bool started = shipLanding.PlayTakeoff();
        if (started)
        {
            exitTriggered = true; // 실제로 이륙이 시작됐을 때만 잠금
        }
        else
        {
            Debug.Log("[HangarExitButton] 아직 착지 중이라 이륙할 수 없습니다. 착지 완료 후 다시 눌러주세요.");
        }
    }

    /// <summary>이륙 연출이 끝나면 ShipLanding에서 호출됨.</summary>
    private void HandleTakeoffComplete()
    {



        // TODO: 출항 씬 완성되면 아래 로직 활성화
        // - 로딩 씬을 거치려면 LoadingManager.NextScene 방식 사용
        //
        if (string.IsNullOrEmpty(launchSceneName)) return;
        LoadingManager.NextScene = launchSceneName;
        GameManager.Instance.LoadScene(SCENE_TYPE.LOADING_SEQUENCE);

        // }
        Debug.Log("[HangarExitButton] 이륙 연출 완료 — 출항 씬 아직 미연결 (launchSceneName 비어있음)");
    }
}