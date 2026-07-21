using UnityEngine;

public class ShopNPCInteraction : MonoBehaviour
{
    [SerializeField] private GameObject affinityUI;
    [SerializeField] private PlayerTestCtrl player;

    private bool _playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = false;
    }

    private void Update()
    {
        if (_playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            OpenInteraction();
        }
    }

    private void OpenInteraction()
    {
        player.canControl = false;
        affinityUI.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
   
    }

    public void CloseInteraction()
    {
        player.canControl = true;
        affinityUI.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}