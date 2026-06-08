// ================================================================
// [외부 참조 가이드]
// ================================================================
// ▶ 이펙트 호출용
//   PlayEffect(EFFECT_TYPE, Vector3 pos, Quaternion rot)
//     → 파티클 이펙트. 재생 종료 시 자동 반납.
//     → 프리팹 루트에 EffectAutoReturn 컴포넌트 부착만 하면 됨.
//
//   PlayEffect(EFFECT_TYPE, Vector3 pos, Quaternion rot, float duration)
//     → 이미지 등 비파티클 이펙트. duration(초) 후 자동 반납.
//
//   예시)
//   // 미사일 폭발
//   VFXManager.Instance.PlayEffect(EFFECT_TYPE.EXPLOSION_MISSILE, transform.position, Quaternion.identity);
//   // 머즐플래시 (0.05초)
//   VFXManager.Instance.PlayEffect(EFFECT_TYPE.MUZZLE_BULLET, firePos.position, firePos.rotation, 0.05f);
//
// ▶ 씬 전환팀 참조용
//   ReturnAll() : 씬 전환·게임오버 시 GameManager.LoadSceneRoutine()에서 호출
// ================================================================

using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// VFXEntry
// 인스펙터에서 이펙트 종류 / 프리팹 / 초기 사이즈를 설정하는 직렬화 클래스.
// VFXManager.vfxConfigs 배열에 항목을 추가해서 등록.
// =====================================================================
[System.Serializable]
public class VFXEntry
{
    [Tooltip("이펙트 종류. enum_Types.cs의 EFFECT_TYPE 값 선택.")]
    public EFFECT_TYPE effectType;

    [Tooltip("이펙트 프리팹. 인스펙터에서 연결.")]
    public GameObject prefab;

    [Tooltip("게임 시작 시 미리 생성할 개수. 부족하면 자동 확장 + 경고 로그.")]
    public int initialSize = 10;
}

// =====================================================================
// VFXManager
//
// 역할:
//   1. 게임 시작 시 vfxConfigs 배열을 순회하여 풀 초기화
//   2. PlayEffect() 로 풀에서 이펙트 꺼내 세계 좌표에 배치
//   3. ReturnEffect() 로 이펙트 반납 (비활성화 → 큐 반환)
//   4. 풀 부족 시 자동 확장 + 경고 로그
//   5. ReturnAll() 로 씬 전환/게임오버 시 전체 강제 반납
//
// 반납 방식 (자동):
//   - 파티클 이펙트 : EffectAutoReturn 컴포넌트 + ParticleSystem Stop Action = Script
//   - 이미지/기타   : PlayEffect duration 파라미터 지정 → Update 타이머 자동 반납
//
// 추가 방법 (신규 이펙트 등록 시):
//   1. enum_Types.cs의 EFFECT_TYPE에 값 추가
//   2. 인스펙터 vfxConfigs 배열에 항목 추가, 프리팹 연결, 사이즈 설정
// =====================================================================
public class VFXManager : MonoBehaviour
{
    // =====================================================================
    // 싱글톤
    // =====================================================================
    private static VFXManager instance;
    public static VFXManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<VFXManager>();
                if (instance == null)
                {
                    Debug.LogError("[VFXManager] 씬에 VFXManager 없음! 하이어라키에 추가 필요");
                }
            }
            return instance;
        }
    }

    // =====================================================================
    // 인스펙터 설정
    // =====================================================================
    [Header("======= VFX 풀 설정 =======")]
    [Tooltip("배열 항목 추가 후 EFFECT_TYPE / 프리팹 / 초기 사이즈 설정으로 풀 등록")]
    public VFXEntry[] vfxConfigs;

    // =====================================================================
    // 내부 풀
    // _pools            : 사용 가능한 오브젝트 큐 (O(1) 꺼내기/반납)
    // _allObjects       : 생성된 전체 오브젝트 ↔ 타입 매핑 (ReturnAll용)
    // _autoReturnCache  : EffectAutoReturn 컴포넌트 캐시 (생성 시 1회만 GetComponent)
    // _timedEffects     : duration 기반 반납 대기 목록 (머즐플래시 등)
    // =====================================================================
    private Dictionary<EFFECT_TYPE, Queue<GameObject>> _pools= new Dictionary<EFFECT_TYPE, Queue<GameObject>>();

    private Dictionary<GameObject, EFFECT_TYPE> _allObjects = new Dictionary<GameObject, EFFECT_TYPE>();

    private Dictionary<GameObject, EffectAutoReturn> _autoReturnCache = new Dictionary<GameObject, EffectAutoReturn>();

    private List<TimedEffect> _timedEffects = new List<TimedEffect>();

    private struct TimedEffect
    {
        public GameObject obj;
        public EFFECT_TYPE type;
        public float returnAt;
    }

    // =====================================================================
    // 싱글톤 Awake
    // =====================================================================
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitPools();
        }
        else if (instance != this)
        {
            Debug.LogWarning("[VFXManager] 중복 감지. 기존 인스턴스 유지, 새 오브젝트 파괴");
            Destroy(gameObject);
        }
    }

    private void InitPools()
    {
        foreach (var config in vfxConfigs)
        {
            if (config.prefab == null)
            {
                Debug.LogError($"[VFXManager] {config.effectType} prefab 미연결! 인스펙터 확인 필요");
                continue;
            }

            var queue = new Queue<GameObject>(config.initialSize);
            for (int i = 0; i < config.initialSize; i++)
            {
                GameObject obj = CreatePooledObject(config.prefab, config.effectType);
                queue.Enqueue(obj);
            }
            _pools[config.effectType] = queue;
        }
    }

    // 오브젝트 생성 + _allObjects / _autoReturnCache 등록을 한 곳에서 처리
    private GameObject CreatePooledObject(GameObject prefab, EFFECT_TYPE type)
    {
        GameObject obj = Instantiate(prefab, transform);
        obj.SetActive(false);
        _allObjects[obj] = type;
        _autoReturnCache[obj] = obj.GetComponent<EffectAutoReturn>();
        return obj;
    }

    // =====================================================================
    // Update — duration 기반 자동 반납 처리
    // =====================================================================
    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            return;
        }

        for (int i = _timedEffects.Count - 1; i >= 0; i--)
        {
            if (Time.time >= _timedEffects[i].returnAt)
            {
                ReturnEffect(_timedEffects[i].type, _timedEffects[i].obj);
                _timedEffects.RemoveAt(i);
            }
        }
    }

    // =====================================================================
    // PlayEffect
    //
    // duration = 0f (기본값) : 파티클 종료 콜백으로 자동 반납
    //                          → 프리팹에 EffectAutoReturn 컴포넌트 필요
    //                          → ParticleSystem Stop Action = Script 설정 필요
    // duration > 0f          : 지정 시간 후 Update 타이머로 자동 반납
    //                          → 머즐플래시 이미지 등 파티클 아닌 이펙트에 사용
    // =====================================================================
    public void PlayEffect(EFFECT_TYPE type, Vector3 pos, Quaternion rot, float duration = 0f)
    {
        GameObject obj = GetFromPool(type);
        if (obj == null)
        {
            return;
        }

        obj.transform.SetPositionAndRotation(pos, rot);

        // 파티클 콜백 반납용 컴포넌트에 type 주입 (캐시에서 조회)
        if (_autoReturnCache.TryGetValue(obj, out EffectAutoReturn autoReturn) && autoReturn != null)
        {
            autoReturn.effectType = type;
        }

        obj.SetActive(true);

        if (duration > 0f)
        {
            TimedEffect te;
            te.obj = obj;
            te.type = type;
            te.returnAt = Time.time + duration;
            _timedEffects.Add(te);
        }
    }

    // =====================================================================
    // ReturnEffect — EffectAutoReturn 콜백 또는 Update 타이머에서 호출
    //
    // 이중 반납 방지: activeInHierarchy 체크로 이미 비활성화된 경우 무시
    // =====================================================================
    public void ReturnEffect(EFFECT_TYPE type, GameObject obj)
    {
        if (!obj.activeInHierarchy)
        {
            return;
        }

        obj.SetActive(false);

        if (_pools.TryGetValue(type, out var queue))
        {
            queue.Enqueue(obj);
        }
        else
        {
            Debug.LogWarning($"[VFXManager] ReturnEffect: 풀 없음 ({type}). 오브젝트 파괴.");
            Destroy(obj);
        }
    }

    // =====================================================================
    // GetFromPool — 풀에서 꺼내기. 비어있으면 자동 확장.
    // =====================================================================
    private GameObject GetFromPool(EFFECT_TYPE type)
    {
        if (!_pools.TryGetValue(type, out var queue))
        {
            Debug.LogError($"[VFXManager] 풀 없음: {type}. vfxConfigs에 등록 필요.");
            return null;
        }

        if (queue.Count > 0)
        {
            return queue.Dequeue();
        }

        // 풀 부족 → 자동 확장 (lambda 없이 foreach 탐색)
        VFXEntry config = null;
        foreach (var c in vfxConfigs)
        {
            if (c.effectType == type)
            {
                config = c;
                break;
            }
        }

        if (config == null || config.prefab == null)
        {
            Debug.LogError($"[VFXManager] {type} 자동 확장 실패. prefab 확인 필요.");
            return null;
        }

        Debug.LogWarning($"[VFXManager] {type} 풀 자동 확장.");
        // 바로 사용되므로 큐에 넣지 않음. ReturnEffect 호출 시 큐에 합류.
        return CreatePooledObject(config.prefab, type);
    }

    // =====================================================================
    // ReturnAll — 씬 전환/게임오버 시 GameManager에서 호출
    // 활성 중인 모든 이펙트를 강제 반납하고 큐를 재구성
    // =====================================================================
    public void ReturnAll()
    {
        _timedEffects.Clear();

        // 큐 비우기
        foreach (var pair in _pools)
        {
            pair.Value.Clear();
        }

        // _allObjects 기준으로 전체 비활성화 후 큐 재등록
        foreach (var pair in _allObjects)
        {
            pair.Key.SetActive(false);

            if (_pools.TryGetValue(pair.Value, out var queue))
            {
                queue.Enqueue(pair.Key);
            }
        }
    }
}
