using UnityEngine;

public class BaseLandingTrigger : MonoBehaviour
{
    private bool _triggered;
    public GameObject HUD;

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        Player player = other.GetComponentInParent<Player>();
        if (player == null || !player.IsMine) return;   // ★ 내 함선만 (필수)

        _triggered = true;
        HUD.SetActive(false);
        LoadingManager.NextScene = SCENE_TYPE.BASE_LANDING.ToString();
        GameManager.Instance.LoadScene(SCENE_TYPE.LOADING_SEQUENCE);
    }
}
