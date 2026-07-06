using UnityEngine;

// =====================================================================
// TestServerSave (임시 테스트용 — 서버 저장 확인 끝나면 삭제)
//
// 서버 저장/로드가 실제로 되는지 씬 제한(STATION) 없이 시험하는 키.
// ServerApi 오브젝트가 씬에 있고 로그인된 상태여야 함.
//
//   F6 : 골드 +100          (저장할 거리 만들기)
//   F5 : 현재 골드를 서버에 저장
//   F9 : 서버에서 불러와 골드 복원
//
// 시험 순서: F6 몇 번 → F5 저장 → 플레이 정지 → 다시 플레이 → F9
//           → 골드가 돌아오면 "재접속 시 유지" 성공!
// =====================================================================
public class TestServerSave : MonoBehaviour
{
    private void Update()
    {
        // 골드 +100
        if (Input.GetKeyDown(KeyCode.F6))
        {
            if (InventoryManager.Instance == null)
            {
                Debug.LogWarning("[TestServerSave] InventoryManager 없음");
                return;
            }
            InventoryManager.Instance.gold += 100;
            Debug.Log($"[TestServerSave] 골드 +100 → 현재 {InventoryManager.Instance.gold}");
        }

        // 서버에 저장
        if (Input.GetKeyDown(KeyCode.F5))
        {
            if (!CheckServerReady()) return;

            // 테스트용 SaveData 직접 조립 (본 저장은 GameManager.CollectSaveData가 담당)
            SaveData data = new SaveData();
            data.gold = InventoryManager.Instance != null ? InventoryManager.Instance.gold : 0;
            if (GameManager.Instance != null && GameManager.Instance.playerRef != null)
            {
                data.level = GameManager.Instance.playerRef.level;
            }

            StartCoroutine(ServerApi.Instance.SaveCo(data,
                onSuccess: () => Debug.Log($"[TestServerSave] ✅ 서버 저장 성공 (골드 {data.gold})"),
                onError:   err => Debug.LogError($"[TestServerSave] ❌ 저장 실패: {err}")));
        }

        // 서버에서 로드
        if (Input.GetKeyDown(KeyCode.F9))
        {
            if (!CheckServerReady()) return;

            StartCoroutine(ServerApi.Instance.LoadCo(
                data =>
                {
                    if (InventoryManager.Instance != null)
                        InventoryManager.Instance.gold = data.gold;
                    Debug.Log($"[TestServerSave] ✅ 서버 로드 성공 → 골드 {data.gold} 복원");
                },
                err => Debug.LogError($"[TestServerSave] ❌ 로드 실패: {err}")));
        }
    }

    private bool CheckServerReady()
    {
        if (ServerApi.Instance == null)
        {
            Debug.LogWarning("[TestServerSave] 씬에 ServerApi 오브젝트가 없음");
            return false;
        }
        if (!ServerApi.Instance.IsLoggedIn)
        {
            Debug.LogWarning("[TestServerSave] 아직 로그인 안 됨 (AutoLogin 체크했는지, 서버 켰는지 확인)");
            return false;
        }
        return true;
    }
}
