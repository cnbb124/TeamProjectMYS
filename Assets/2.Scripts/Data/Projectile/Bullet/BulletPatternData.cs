using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BulletPattern", menuName = "Barrage/Bullet Pattern")]
public class BulletPatternData : ScriptableObject
{
    [Header("Ammo PreFab")]
    public GameObject bulletPrefab;

    [Header("Wave List")]
    public List<PatternWave> waves = new List<PatternWave>();
}

[System.Serializable]
public class PatternWave
{
    public string waveName = "Wave";
    public float delay = 0.5f;
    public GameObject bulletPrefab;
    public List<PatternPoint> points = new List<PatternPoint>();
}

[System.Serializable]
public class PatternPoint
{
    public Vector2 localDir;
    public float speed = 10f;
    public bool aimAtPlayer = false;
}