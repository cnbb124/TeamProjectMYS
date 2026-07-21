
using UnityEngine;

public class HUDOnOff : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        if (HUDManager.Instance != null)
            HUDManager.Instance.gameObject.SetActive(true);
    }

}
