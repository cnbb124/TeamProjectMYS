using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStartCollider : MonoBehaviour
{
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

    private void OpenInteraction()
    {
        player.canControl = false;
        _UIOpen = true;
        UI.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

    }

    public void CloseInteraction()
    {
        player.canControl = true;
        _UIOpen = false;
        UI.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
