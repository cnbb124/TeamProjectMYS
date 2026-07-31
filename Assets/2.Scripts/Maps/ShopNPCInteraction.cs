using UnityEngine;

public class ShopNPCInteraction : MonoBehaviour
{
    // InputManager가 커서를 풀지 판단할 때 봄
    public static bool IsOpen { get; private set; }

    [SerializeField] private GameObject affinityUI;
    [SerializeField] private PlayerTestCtrl player;

    private bool _playerInRange;
    private bool _isOpen;

    private void OnDisable()
    {
        IsOpen = false;
    }

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
            if (_isOpen)
            {
                CloseInteraction();
            }
            else
            {
                OpenInteraction();
            }
        }
    }

    private void OpenInteraction()
    {
        player.canControl = false;
        _isOpen = true;
        IsOpen = true;
        affinityUI.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
   
    }

    public void CloseInteraction()
    {
        player.canControl = true;
        _isOpen = false;
        IsOpen = false;
        affinityUI.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}