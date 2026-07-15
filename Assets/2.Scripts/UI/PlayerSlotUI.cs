using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSlotUI : MonoBehaviour
{
    [Header("Player Information")]
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text readyIndicatorText;

    [Header("Ready Button")]
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;

    [Header("Indicator Colors")]
    [SerializeField] private Color readyColor = new Color(0.35f, 1f, 0.45f, 1f);
    [SerializeField] private Color notReadyColor = Color.white;

    private int actorNumber;
    private Action<int> onReadyButtonClicked;

    private void Awake()
    {
        if (readyButton != null)
        {
            readyButton.onClick.AddListener(HandleReadyButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (readyButton != null)
        {
            readyButton.onClick.RemoveListener(HandleReadyButtonClicked);
        }
    }

    public void Configure(
        int assignedActorNumber,
        string displayName,
        bool isReady,
        bool isHost,
        bool isLocalPlayer,
        Action<int> readyButtonCallback)
    {
        actorNumber = assignedActorNumber;
        onReadyButtonClicked = readyButtonCallback;

        if (nicknameText != null)
        {
            nicknameText.text = isHost
                ? displayName + " [HOST]"
                : displayName;
        }

        if (readyIndicatorText != null)
        {
            readyIndicatorText.text = isReady ? "READY" : "NOT READY";
            readyIndicatorText.color = isReady ? readyColor : notReadyColor;
        }

        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(isLocalPlayer);
            readyButton.interactable = isLocalPlayer;
        }

        if (readyButtonText != null)
        {
            readyButtonText.text = isReady ? "CANCEL" : "READY";
        }
    }

    private void HandleReadyButtonClicked()
    {
        onReadyButtonClicked?.Invoke(actorNumber);
    }
}
