using System.Collections.Generic;
using UnityEngine;

// ================================================================
// [외부 참조 가이드]
// ================================================================
// RegisterPlayer(Unit)           플레이어 등록 — Player.Start()에서 호출
// UnregisterPlayer(Unit)         플레이어 해제 — Player.Die()에서 호출
// GetNearestPlayer(Vector3 from) 가장 가까운 플레이어 Transform 반환 (없으면 null)
//                                 → Enemy에서 target 갱신 시 사용
//
// RegisterEnemy(Unit)            적 등록 — Enemy.Start()에서 호출
// UnregisterEnemy(Unit)          적 해제 — Enemy.Die()에서 호출
// GetAllEnemies()                현재 살아있는 적 리스트 반환 (읽기전용)
//                                 → 레이더 UI팀에서 적 방향 계산 시 사용
// ================================================================

public class UnitManager : MonoBehaviour
{
    private static UnitManager instance;
    public static UnitManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<UnitManager>();
                if (instance == null)
                {
                    Debug.Log("씬에 UnitManager 누락! 하이어라키에 추가 필요");
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Debug.LogWarning("중복된 UnitManager 발견. 파괴 후 실행");
            Destroy(gameObject);
        }
    }

    private readonly List<Unit> _players = new List<Unit>();
    private readonly List<Unit> _enemies = new List<Unit>();

    public void RegisterPlayer(Unit player)
    {
        if (!_players.Contains(player))
        {
            _players.Add(player);
        }
    }

    public void UnregisterPlayer(Unit player)
    {
        _players.Remove(player);
    }

    public void RegisterEnemy(Unit enemy)
    {
        if (!_enemies.Contains(enemy))
        {
            _enemies.Add(enemy);
        }
    }

    public void UnregisterEnemy(Unit enemy)
    {
        _enemies.Remove(enemy);
    }

    // 현재 살아있는 적 리스트 반환. 뒤에서부터 순회해 null 항목 자동 정리.
    public System.Collections.Generic.IReadOnlyList<Unit> GetAllEnemies()
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            if (_enemies[i] == null)
            {
                _enemies.RemoveAt(i);
            }
        }
        return _enemies;
    }

    // 가장 가까운 살아있는 플레이어 Transform 반환. 리스트를 뒤에서부터 순회해 null(파괴된 오브젝트)은 자동 정리.
    public Transform GetNearestPlayer(Vector3 from)
    {
        Transform nearest = null;
        float minDist = float.MaxValue;

        for (int i = _players.Count - 1; i >= 0; i--)
        {
            if (_players[i] == null)
            {
                _players.RemoveAt(i);
                continue;
            }

            float dist = Vector3.Distance(from, _players[i].transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = _players[i].transform;
            }
        }

        return nearest;
    }
}
