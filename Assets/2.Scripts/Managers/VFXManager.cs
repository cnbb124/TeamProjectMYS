// ================================================================
// [외부 참조 가이드]
// ================================================================
// ▶ 이펙트 호출용
//   PlayEffectAtPosition(EFFECT_TYPE, Vector3 pos, Quaternion rot, float duration = 0f, Vector3 scale = default)
//     → 월드 좌표 고정 이펙트 (피격, 폭발 등). 위치 변경 없이 그 자리에서 재생.
//     → duration = 0f : 파티클 이펙트, 재생 종료 시 자동 반납 (EffectAutoReturn 필요)
//     → duration > 0f : 이미지 등 비파티클 이펙트, duration(초) 후 자동 반납
//     → scale 미지정(default) 시 Vector3.one 적용. 폭발 반경 등에 비례해 이펙트 크기 조절 시 사용.
//
//   PlayEffectAtUnit(EFFECT_TYPE, Transform unitTr, Vector3 pos, Quaternion rot, float duration = 0f)
//     → 유닛에 부착되는 이펙트 (머즐플래시 등). unitTr을 부모로 SetParent되어
//       이후 유닛이 움직이면 같이 따라감. pos/rot은 생성 시점 위치(총구 등) 기준.
//
//   예시)
//   // 미사일 폭발 (위치 고정)
//   VFXManager.Instance.PlayEffectAtPosition(EFFECT_TYPE.VFX_EXPLOSION_MISSILE, transform.position, Quaternion.identity);
//   // 머즐플래시 (유닛에 부착, 0.2초 후 반납)
//   VFXManager.Instance.PlayEffectAtUnit(EFFECT_TYPE.VFX_BULLET_MUZZLE, _unit.transform, firePos.position, firePos.rotation, 0.2f);
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
                else
                {
                    DontDestroyOnLoad(instance.gameObject);
                }
            }
            return instance;
        }
    }
	[Header("<size=22>사용시 주의사항</size>\n\n" +
        "<size=14>PlayEffect메서드들 사용시 파티클\n" +
        "duration 입력X EffectAutoReturn 부착O\n" +
        "이미지등 직접 반납해야하는것들\n" +
        "duration 입력O EffectAutoReturn 부착X</size>")]
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
                Debug.LogWarning($"[VFXManager] {config.effectType} prefab 미연결! 인스펙터 확인 필요");
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
            // 부착됐던 유닛이 파괴되면 이 이펙트도 같이 파괴됨 — 접근 전에 걸러야 예외가 안 남(해결책②).
            if (_timedEffects[i].obj == null)
            {
                Debug.LogWarning($"[VFXManager] 파괴된 이펙트 감지 @Update(_timedEffects 인덱스 {i}, type={_timedEffects[i].type}) — 접근 전 제거함. (원인: 부착 유닛이 파괴되며 같이 파괴됨)");
                _timedEffects.RemoveAt(i);
                continue;
            }
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
    /// <summary>
    /// 해당 좌표에서 표시될 이펙트(피격등 단발)
    /// </summary>
    /// <param name="type"></param>
    /// <param name="pos"></param>
    /// <param name="rot"></param>
    /// <param name="duration"></param>
    /// <param name="scale">이펙트 크기 배율. 미지정(default) 시 Vector3.one 적용. (예: 폭발 반경 비례 크기 조절)</param>
    public void PlayEffectAtPosition(EFFECT_TYPE type, Vector3 pos, Quaternion rot, float duration = 0f, Vector3 scale = default)
    {
        GameObject obj = GetFromPool(type);
        if (obj == null)
        {
            return;
        }

        obj.transform.SetPositionAndRotation(pos, rot);
        // scale은 넘겨줄 때(폭발 등 크기 변경 이펙트)만 세팅. 안 넘기면 안 건드림 → 프리팹 스케일 그대로.
        // (풀이 EFFECT_TYPE별로 분리라, 크기 안 바꾸는 이펙트의 오브젝트는 남이 안 깎음 → 리셋 불필요)
        if (scale != Vector3.zero)
        {
            obj.transform.localScale = scale;
        }

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

    /// <summary>
    /// 유닛에 부착하여 사용될 이펙트들(총알발사등)
    /// </summary>
    /// <param name="type"></param>
    /// <param name="unitTr">부착(SetParent) 대상. 유닛 루트처럼 파츠보다 오래 사는 안전한 transform.</param>
    /// <param name="pos">생성 시 위치(총구 위치 등)</param>
    /// <param name="rot">생성 시 회전(총구 방향 등)</param>
    /// <param name="duration">지속시간</param>
	public GameObject PlayEffectAtUnit(EFFECT_TYPE type, Transform unitTr, Vector3 pos, Quaternion rot, float duration = 0f)
	{
		GameObject obj = GetFromPool(type);
		if (obj == null)
		{
			return null;
		}

		obj.transform.SetPositionAndRotation(pos, rot);
		// 유닛(생명주기 안전한 대상)에 부착 → 이후 유닛이 움직이면 같이 따라감
		// 주의: 총구(파츠 자식)에 직접 붙이면 파츠 교체/파괴 시 같이 파괴되어 풀 손실됨
		obj.transform.SetParent(unitTr, true);

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

		return obj;
	}

	// =====================================================================
	// ReturnEffect — EffectAutoReturn 콜백 또는 Update 타이머에서 호출
	//
	// 이중 반납 방지: activeInHierarchy 체크로 이미 비활성화된 경우 무시
	// =====================================================================
	public void ReturnEffect(EFFECT_TYPE type, GameObject obj)
    {
        // 부착 유닛이 파괴되면 이펙트도 같이 파괴됨 — activeInHierarchy 접근 전에 걸러야 예외가 안 남(해결책②).
        if (obj == null)
        {
            Debug.LogWarning($"[VFXManager] 파괴된 이펙트 감지 @ReturnEffect(type={type}) — 접근 전 무시함. (원인: 부착 유닛이 파괴되며 같이 파괴됨)");
            return;
        }

        if (!obj.activeInHierarchy)
        {
            return;
        }

        obj.SetActive(false);

        // PlayEffectAtUnit으로 유닛에 부착됐던 경우 매니저 자식으로 복귀
        // (부착 대상에 매달린 채로 풀에 남으면, 추후 그 대상이 파괴될 때 같이 파괴될 수 있음)
        obj.transform.SetParent(this.transform);

        // PlayEffectAtPosition의 scale 파라미터로 크기가 바뀌었을 수 있으므로 원본 크기로 복원
        obj.transform.localScale = Vector3.one;

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

        // 파괴된 오브젝트가 큐에 섞여 있을 수 있으므로(씬 리로드 잔재) 살아있는 것을 만날 때까지 건너뜀(해결책②).
        while (queue.Count > 0)
        {
            GameObject pooled = queue.Dequeue();
            if (pooled == null)
            {
                Debug.LogWarning($"[VFXManager] 파괴된 이펙트 감지 @GetFromPool(type={type}) — 큐에서 건너뜀. (원인: 부착 유닛이 파괴되며 같이 파괴됨)");
                continue;
            }
            return pooled;
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
            // 이미 파괴된 오브젝트는 건너뜀(널가드) — 씬 리로드 도중 호출되면 있을 수 있음.
            if (pair.Key == null)
            {
                continue;
            }

            pair.Key.SetActive(false);
            // PlayEffectAtUnit으로 유닛에 부착된 이펙트는 매니저 자식으로 복귀시켜야
            // 이후 그 유닛이 씬 언로드로 파괴될 때 같이 파괴되지 않는다(ReturnEffect와 동일 처리).
            pair.Key.transform.SetParent(this.transform);

            if (_pools.TryGetValue(pair.Value, out var queue))
            {
                queue.Enqueue(pair.Key);
            }
        }
    }
}
