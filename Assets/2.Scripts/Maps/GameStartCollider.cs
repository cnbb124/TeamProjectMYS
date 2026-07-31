using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStartCollider : MonoBehaviour
{
    // InputManager가 커서를 풀어야 하는지 판단할 때 봄.
    public static bool IsOpen { get; private set; }

    [SerializeField] private GameObject UI;
    [SerializeField] private PlayerTestCtrl player;

    private bool _playerInRange;
    private bool _UIOpen;

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
            if (_UIOpen)
            {
                CloseInteraction();
            }
            else
            {
                OpenInteraction();
            }
        }

        //if (_UIOpen&&_playerInRange && Input.GetKeyDown(KeyCode.E))
        //{
          
        //    CloseInteraction();
        //}
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    private void OpenInteraction()
    {
        player.canControl = false;
        _UIOpen = true;
        IsOpen = true;
        UI.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

    }

    public void CloseInteraction()
    {
        player.canControl = true;
        _UIOpen = false;
        IsOpen = false;
        UI.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
