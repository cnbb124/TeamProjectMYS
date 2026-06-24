using UnityEngine;

public class SunFollow : MonoBehaviour
{
    public Transform player;
    public Vector3 worldOffset = new Vector3(800f, 400f, 750f);

    // private Vector3 originalScale;

    void Start()
    {
       // originalScale = transform.localScale;
    }

    void LateUpdate()
    {
        transform.position = player.position + worldOffset;

        //if (Camera.main != null)
        //{
            
        //    Vector3 camToSunDir = (transform.position - Camera.main.transform.position).normalized;
        //    float cosAngle = Vector3.Dot(Camera.main.transform.forward, camToSunDir);

            
        //    if (cosAngle > 0)
        //    {
                
        //        transform.localScale = originalScale * cosAngle;
        //    }

            
        //    transform.rotation = Camera.main.transform.rotation;
        //}
    }
}
