using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// =====================================================================
// PhotonPoolAdapter — PUN의 네트워크 스폰을 우리 PoolManager(풀링)에 연결하는 어댑터.
//
// [왜 필요한가]
//   PhotonNetwork.Instantiate는 기본적으로 Resources에서 프리팹을 로드해 매번 실제
//   Instantiate/Destroy 한다(DefaultPool). 적처럼 자주 생성/소멸하는 오브젝트는 이게 낭비다.
//   IPunPrefabPool을 구현해 PhotonNetwork.PrefabPool에 등록하면, PUN의 생성/파괴 호출을
//   가로채 "실제 파괴 대신 SetActive on/off로 재사용"하게 만들 수 있다.
//   그러면서도 PhotonView·네트워크ID·동기화·late join 버퍼링·Master 승계는 PUN이 그대로 처리한다.
//
// [규약 — 중요]
//   Instantiate()는 반드시 '비활성' GameObject를 돌려줘야 한다. PUN이 그 오브젝트에
//   PhotonView.ViewID를 세팅한 뒤 직접 활성화한다(활성 상태로 주면 PUN이 경고).
//
// [prefabId 규칙]
//   풀 대상(적 등)은 PhotonNetwork.Instantiate에 넘기는 이름을 POOL_TYPE 이름으로 맞춘다
//   (예: PhotonNetwork.Instantiate(poolType.ToString(), ...)). 어댑터가 그 이름을 POOL_TYPE으로
//   파싱해 PoolManager에서 꺼낸다. 파싱 안 되는 이름(플레이어 등)은 DefaultPool로 폴백(Resources).
//
// [등록]
//   NetworkManager.Awake에서 PhotonNetwork.PrefabPool = new PhotonPoolAdapter(); 한 줄.
// =====================================================================
public class PhotonPoolAdapter : IPunPrefabPool
{
    // 풀 대상이 아닌 것(플레이어 등)은 PUN 기본 방식(Resources 로드 후 실제 생성/파괴) 그대로 위임.
    private DefaultPool _fallback = new DefaultPool();

    // 우리 풀에서 꺼내 준 오브젝트 추적 → Destroy 시 '실제 파괴'가 아니라 '풀 반납'으로 처리하기 위함.
    private HashSet<GameObject> _fromPool = new HashSet<GameObject>();

    public GameObject Instantiate(string prefabId, Vector3 position, Quaternion rotation)
    {
        // prefabId가 유효한 POOL_TYPE이면 로컬 PoolManager 풀에서 재사용, 아니면 기본 풀(Resources).
        if (System.Enum.TryParse(prefabId, out POOL_TYPE poolType)
            && System.Enum.IsDefined(typeof(POOL_TYPE), poolType)
            && PoolManager.Instance != null)
        {
            GameObject pooled = PoolManager.Instance.GetInactive(poolType);
            if (pooled != null)
            {
                pooled.transform.SetPositionAndRotation(position, rotation);
                _fromPool.Add(pooled);
                return pooled;   // 비활성 상태로 반환 (PUN이 ViewID 세팅 후 활성화)
            }
        }

        return _fallback.Instantiate(prefabId, position, rotation);
    }

    public void Destroy(GameObject gameObject)
    {
        // 우리 풀 출신이면 실제 파괴하지 않고 비활성화 반납(재사용).
        if (_fromPool.Remove(gameObject))
        {
            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Return(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
            return;
        }

        // 풀 대상 아님(플레이어 등) → 기본 방식으로 실제 파괴.
        _fallback.Destroy(gameObject);
    }
}
