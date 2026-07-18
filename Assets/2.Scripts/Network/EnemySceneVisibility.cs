using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

// =====================================================================
// EnemySceneVisibility — "이 적이 태어난 씬에 내가 있을 때만 보이게" 필터.
//
// [왜 필요한가]
//   Photon은 '씬'을 모르고 방 단위로만 동작한다. 적은 룸 오브젝트라 방에 있는 모든 클라에 존재하므로,
//   필터가 없으면 스테이지에서 스폰된 적이 로비/대기실 화면에도 그대로 떠 버린다.
//
// [기준이 '스폰된 씬'인 이유]
//   적은 전부 방장 소유라 '소유자의 현재 씬'으로 판단하면, 방장이 스테이션으로 돌아가는 순간
//   스테이지에 남은 적들이 스테이지 플레이어에게서 사라져 버린다.
//   그래서 소유자와 무관하게 '스폰 당시 씬'을 기준으로 삼는다.
//   그 값은 스폰할 때 InstantiateRoomObject의 마지막 인자로 실어보낸다(SpawnManager/Enemy/MidBoss 참고).
//
// [숨기는 방식]
//   PlayerSceneVisibility와 동일 — 루트를 끄면 동기화가 멈추므로 Renderer/Collider만 토글한다.
//
// [부착 위치] 네트워크 스폰되는 적/드랍 프리팹 루트(PhotonView가 있는 오브젝트).
// =====================================================================
public class EnemySceneVisibility : MonoBehaviourPun
{
    // 이 오브젝트가 태어난 씬. InstantiationData로 받음. 못 받으면 null(=필터 안 함).
    private string _spawnScene;

    // 풀에서 재사용될 때마다 새 InstantiationData가 들어오므로 매번 다시 읽는다.
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnLocalSceneLoaded;
        ReadSpawnScene();
        ApplyVisibility();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnLocalSceneLoaded;
    }

    // 내가 다른 씬으로 넘어가면 다시 판단.
    private void OnLocalSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyVisibility();
    }

    private void ReadSpawnScene()
    {
        _spawnScene = null;
        if (photonView == null)
        {
            return;
        }
        object[] data = photonView.InstantiationData;
        if (data == null || data.Length <= PlayerSceneVisibility.SPAWN_SCENE_DATA_INDEX)
        {
            return;
        }
        _spawnScene = data[PlayerSceneVisibility.SPAWN_SCENE_DATA_INDEX] as string;
    }

    private void ApplyVisibility()
    {
        // 스폰 씬을 모르면(비네트워크 스폰/씬 배치 적 등) 숨기지 않음 — 모르면 일단 보여줌.
        bool differentScene = PlayerSceneVisibility.IsDifferentFromLocalScene(_spawnScene);
        SetVisible(!differentScene);
    }

    // 루트는 켜둔 채(콜백/동기화 유지) 렌더러·콜라이더만 토글.
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
