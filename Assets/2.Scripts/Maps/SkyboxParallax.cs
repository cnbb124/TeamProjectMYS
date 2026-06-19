using UnityEngine;

public class SkyboxParallax : MonoBehaviour
{
    public Transform player;
    public Material skyboxMaterial;
    public float parallaxStrength = 10.0f;
    public float returnSpeed = 2f; // 0으로 돌아오는 속도

    private Vector3 lastPos;
    private Vector3 offset = Vector3.zero;

    void Start()
    {
        lastPos = player.position;
    }

    void Update()
    {
        Vector3 frameDelta = player.position - lastPos;
        lastPos = player.position;

        offset += frameDelta * parallaxStrength;

        offset = Vector3.Lerp(offset, Vector3.zero, Time.deltaTime * returnSpeed);

        skyboxMaterial.SetVector("_ParallaxOffset", offset);
    }
}