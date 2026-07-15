using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BulletPattern", menuName = "Barrage/Bullet Pattern")]
public class BulletPatternData : ScriptableObject
{
    // 총알 종류/스탯(프리팹 포함)은 발사 주체(EnemyBoss._bulletData)가 결정한다.
    // 이 SO는 '어떻게 뿌리는가'(방향/속도/타이밍)만 담당 — 프리팹 지정 필드는 두지 않는다.
    [Header("Wave List")]
    public List<PatternWave> waves = new List<PatternWave>();
}

[System.Serializable]
public class PatternWave
{
    public string waveName = "Wave";
    public float delay = 0.5f;
    public List<PatternPoint> points = new List<PatternPoint>();
}

[System.Serializable]
public class PatternPoint
{
    public Vector2 localDir;
    public float speed = 10f;
    public bool aimAtPlayer = false;
}