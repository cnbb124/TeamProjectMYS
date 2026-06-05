using UnityEngine;

public class cs_Map_Planet : MonoBehaviour
{
    [Header("자전 설정")]
    public Vector3 rotationAxis = Vector3.up;
    public float rotationSpeed = 5f;

    [Header("플라즈마 설정")]
    public ParticleSystem plasmaEffect;
    public float pulseInterval = 3f;      // 분출 간격
    public float pulseVariance = 1f;      // 간격 랜덤 편차

    private float nextPulseTime;

    void Start()
    {
        ScheduleNextPulse();
    }

    void Update()
    {
        // 느린 자전
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);

        // 플라즈마 분출
        if (plasmaEffect != null && Time.time >= nextPulseTime)
        {
            plasmaEffect.Emit(Random.Range(20, 50));
            ScheduleNextPulse();
        }
    }

    void ScheduleNextPulse()
    {
        nextPulseTime = Time.time + pulseInterval + Random.Range(-pulseVariance, pulseVariance);
    }
}
