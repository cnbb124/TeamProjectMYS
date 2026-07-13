using Photon.Pun;
using UnityEngine;

// =====================================================================
// PlayerSpawner — '이 씬에서 무엇을·어디에 스폰할지'를 담당하는 씬별 컴포넌트.
//
// [왜 씬별인가]
//   연결/방 상태는 NetworkManager(DDOL, 영속)가 담당하고, 스폰은 씬마다 다르다.
//   전투씬은 전투기를, 스테이션씬은 다른 유닛을, 시작 위치도 씬마다 다르다.
//   그래서 스폰 대상 프리팹과 위치를 '그 씬에 놓인 이 컴포넌트'가 들고 있는다.
//   (NetworkManager에 프리팹/위치를 고정으로 두면 씬마다 다르게 못 씀 → 분리)
//
// [동작]
//   Start()에서 NetworkManager가 이미 방에 있으면 즉시 스폰,
//   아직 연결 중이면 OnRoomReady 이벤트를 구독해 준비되는 순간 스폰한다.
//   (씬은 AutomaticallySyncScene로 전원이 같이 로드 → 각 클라의 PlayerSpawner가 자기 로컬 플레이어를 스폰)
//
// [에디터 세팅]
//   1. 게임플레이/스테이션 등 각 씬에 빈 오브젝트 만들고 이 스크립트 부착.
//   2. playerPrefab : 그 씬에서 스폰할 프리팹을 드래그 (반드시 Resources 폴더 안의 것).
//   3. spawnPoints  : 그 씬의 스폰 위치 오브젝트들을 드래그(비우면 이 오브젝트 위치에 스폰).
// =====================================================================
public class PlayerSpawner : MonoBehaviour
{
    [Header("이 씬에서 스폰할 플레이어")]
    [Tooltip("이 씬에서 스폰할 프리팹을 드래그. 반드시 Resources 폴더 안의 것이어야 함(PhotonNetwork.Instantiate 제약). " +
             "씬마다 다르게 지정 가능 — 전투씬=전투기, 스테이션=스테이션 유닛.")]
    [SerializeField] private GameObject playerPrefab;

    [Header("이 씬의 스폰 위치")]
    [Tooltip("여러 명이면 입장 순서대로 하나씩 배정. 비우면 이 오브젝트 위치에 스폰.")]
    [SerializeField] private Transform[] spawnPoints;

    // 중복 스폰 방지(이벤트가 두 번 오거나 Start와 이벤트가 겹치는 경우).
    private bool _spawned;

    private void Start()
    {
        NetworkManager net = NetworkManager.Instance;
        if (net == null)
        {
            Debug.LogWarning("[PlayerSpawner] NetworkManager 없음 — 스폰 불가");
            return;
        }

        if (net.IsReady)
        {
            SpawnNow();
        }
        else
        {
            // 아직 방 입장 전(연결 중)이면 준비되는 순간 스폰.
            net.OnRoomReady += SpawnNow;
        }
    }

    private void OnDestroy()
    {
        // DDOL 매니저에 죽은 스포너 구독이 남지 않도록 정리(씬 나갈 때).
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnRoomReady -= SpawnNow;
        }
    }

    private void SpawnNow()
    {
        if (_spawned)
        {
            return;
        }
        if (playerPrefab == null)
        {
            Debug.LogWarning("[PlayerSpawner] playerPrefab 미지정 — 스폰 불가");
            return;
        }

        Transform spawn = GetSpawnPoint();
        Vector3 pos = spawn != null ? spawn.position : transform.position;
        Quaternion rot = spawn != null ? spawn.rotation : transform.rotation;

        NetworkManager.Instance.SpawnPlayer(playerPrefab.name, pos, rot);
        _spawned = true;

        // 이벤트로 들어온 경우 구독 해제(1회성).
        NetworkManager.Instance.OnRoomReady -= SpawnNow;
    }

    /// <summary>내 순번에 맞는 스폰 위치를 고른다. 부족하면 순환.</summary>
    private Transform GetSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return null;
        }
        // 내 순번 = 방에 나보다 먼저 들어온 사람 수(대략) → PlayerCount-1로 근사.
        int index = (PhotonNetwork.CurrentRoom.PlayerCount - 1) % spawnPoints.Length;
        return spawnPoints[index];
    }
}
