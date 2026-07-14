using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// =====================================================================
// PlayerSceneVisibility — "이 함선의 소유자가 나(로컬)와 같은 씬에 있을 때만 보이게" 필터.
//
// [왜 필요한가]
//   이 게임은 플레이어마다 다른 씬에 있을 수 있다(한 명 스테이션, 한 명 스테이지).
//   그래서 AutomaticallySyncScene(전원 씬 강제 통일)을 안 쓴다. 대신:
//     - 각 클라가 자기 현재 씬을 룸에 알린다(NetworkManager.PublishLocalScene → Player Custom Property).
//     - 각 함선은 '소유자의 씬'과 '내 현재 씬'을 비교해, 다르면 숨긴다.
//   네트워크 오브젝트는 씬과 무관하게 룸 전체에 존재하므로, 이 필터가 없으면 다른 씬에 있는
//   남의 함선이 로비/스테이션 화면에 유령처럼 떠 버린다.
//
// [숨기는 방식 — SetActive가 아니라 Renderer/Collider 토글]
//   루트를 SetActive(false)하면 이 컴포넌트도 꺼져 Photon 콜백을 못 받아 '다시 나타날' 수 없다.
//   또 PhotonView 동기화가 계속 돌아야 상대가 내 씬으로 돌아왔을 때 올바른 위치에 나타난다.
//   그래서 루트는 켜두고 보이는 것(Renderer)·부딪히는 것(Collider)만 껐다 켠다.
//
// [부착 위치] Player 프리팹 루트(PhotonView가 있는 오브젝트)에 부착.
// =====================================================================
public class PlayerSceneVisibility : MonoBehaviourPunCallbacks
{
    // NetworkManager와 공유하는 Player Custom Property 키.
    public const string SCENE_KEY = "scene";

    private void Start()
    {
        ApplyVisibility();
    }

    public override void OnEnable()
    {
        base.OnEnable(); // MonoBehaviourPunCallbacks의 Photon 콜백 등록
        SceneManager.sceneLoaded += OnLocalSceneLoaded;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        SceneManager.sceneLoaded -= OnLocalSceneLoaded;
    }

    // 내(로컬)가 새 씬으로 넘어가면 재평가.
    private void OnLocalSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyVisibility();
    }

    // 룸 내 누군가의 씬(커스텀 프로퍼티)이 바뀌면 재평가.
    // 주의: Photon.Realtime.Player를 반드시 전체 이름으로 써야 함 — 이 프로젝트의 게임 Player 클래스(함선,
    // 전역 네임스페이스)가 같은 파일 스코프에서 이름이 겹쳐 'using Photon.Realtime'보다 우선 해석되기 때문
    // (안 그러면 override 시그니처가 베이스와 달라져 CS0115 발생).
    public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        // 내 함선은 항상 내 씬에 있으니 항상 보임.
        if (photonView == null || photonView.IsMine)
        {
            SetVisible(true);
            return;
        }

        // 남 함선: 소유자의 '현재 씬'이 나의 현재 씬과 '확실히 다를' 때만 숨긴다.
        // 소유자 씬을 아직 모르면(프로퍼티 미수신/미설정) 숨기지 않는다 — 같은 씬인데 타이밍 때문에
        // 프로퍼티를 못 받아서 서로 안 보이는 비대칭 버그를 막기 위함. 정보가 오면 OnPlayerPropertiesUpdate가 재평가.
        string myScene = SceneManager.GetActiveScene().name;
        string ownerScene = null;
        if (photonView.Owner != null
            && photonView.Owner.CustomProperties.TryGetValue(SCENE_KEY, out object v))
        {
            ownerScene = v as string;
        }
        bool differentScene = !string.IsNullOrEmpty(ownerScene) && ownerScene != myScene;
        SetVisible(!differentScene);
    }

    // 루트는 켜둔 채(콜백/동기화 유지) 렌더러·콜라이더만 토글.
    // 파츠가 런타임에 붙으므로 매번 새로 수집한다(가시성 변경은 씬 전환 시점이라 드묾).
    private void SetVisible(bool visible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            r.enabled = visible;
        }
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider c in colliders)
        {
            c.enabled = visible;
        }
    }
}
