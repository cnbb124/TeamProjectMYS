/*
 * [ChatUI]
 * 로비 서버 채팅창 UI. 지금은 로컬 전용(에코) — 포톤 연결 시 onSendRequested 이벤트만 물리면 됨.
 *
 * [씬 구성]
 * ChatPanel
 *  ├ Scroll View (ScrollRect)          ← scrollRect 연결
 *  │   └ Viewport
 *  │       └ Content                   ← content 연결
 *  │           (Vertical Layout Group + Content Size Fitter(Vertical=Preferred),
 *  │            Anchor=Top-Stretch, Pivot Y=1)
 *  └ InputField (TMP_InputField)       ← inputField 연결
 *
 * [메시지 프리팹]
 * 한 줄 텍스트(TMP_Text) 하나면 충분. Layout Element로 최소 높이 지정 권장.
 *
 * [포톤 연결법 (나중에)]
 * - 보내기: chatUI.onSendRequested += msg => photonChat.SendMessage(msg);  (localEcho 끄기)
 * - 받기  : 포톤 수신 콜백에서 chatUI.AddMessage(sender, msg) 호출
 *
 * [동작]
 * - Enter로 전송 (입력 후 포커스 유지 — 연속 채팅 가능)
 * - 새 메시지 오면 자동으로 맨 아래 스크롤
 * - 최대 보관 줄 수 초과 시 오래된 메시지 제거 (메모리/성능)
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChatUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScrollRect     scrollRect;
    [SerializeField] private Transform      content;        // 메시지 쌓일 곳
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private GameObject     messagePrefab;  // TMP_Text 한 줄 프리팹

    [Header("Settings")]
    [SerializeField] private int    maxMessages = 50;       // 초과 시 오래된 줄 제거
    [SerializeField] private string myName      = "Me";     // 로컬 표시 이름 (포톤 연결 시 닉네임으로 교체)
    [SerializeField] private bool   localEcho   = true;     // 보낸 메시지를 바로 창에 표시 (포톤 연결 후엔 끄기 — 서버 수신으로 표시)

    [Header("Colors")]
    [SerializeField] private Color nameColor    = new Color(0.4f, 0.8f, 1f); // 발신자 이름 색
    [SerializeField] private Color systemColor  = new Color(1f, 0.85f, 0.4f); // 시스템 메시지 색

    /// <summary>전송 요청 이벤트 — 포톤 연결 시 여기에 SendMessage를 물리면 됨.</summary>
    public event Action<string> onSendRequested;

    private readonly Queue<GameObject> _messages = new Queue<GameObject>();

    private void Start()
    {
        if (inputField != null)
            inputField.onSubmit.AddListener(OnSubmit); // Enter 입력 시
    }

    // ── 전송 ──
    private void OnSubmit(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        onSendRequested?.Invoke(text);          // 포톤(또는 외부)으로 전송 요청

        if (localEcho)                          // 로컬 테스트: 바로 내 창에 표시
            AddMessage(myName, text);

        inputField.text = "";
        inputField.ActivateInputField();        // 포커스 유지 — 연속 입력 가능
    }

    // ── 수신/표시 (포톤 수신 콜백에서도 이걸 호출) ──

    /// <summary>채팅 메시지 추가. 포톤 수신 시에도 이 메서드 호출.</summary>
    public void AddMessage(string sender, string message)
    {
        string hex = ColorUtility.ToHtmlStringRGB(nameColor);
        AppendLine($"<color=#{hex}>{sender}</color> : {message}");
    }

    /// <summary>시스템 메시지 (입장/퇴장 알림 등).</summary>
    public void AddSystemMessage(string message)
    {
        string hex = ColorUtility.ToHtmlStringRGB(systemColor);
        AppendLine($"<color=#{hex}>[System] {message}</color>");
    }

    private void AppendLine(string line)
    {
        if (content == null || messagePrefab == null) return;

        GameObject go = Instantiate(messagePrefab, content);
        TMP_Text text = go.GetComponentInChildren<TMP_Text>();
        if (text != null) text.text = line;

        _messages.Enqueue(go);

        // 보관 한도 초과 시 오래된 메시지 제거
        while (_messages.Count > maxMessages)
            Destroy(_messages.Dequeue());

        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        if (scrollRect == null) return;
        Canvas.ForceUpdateCanvases();                 // 레이아웃 즉시 갱신 (안 하면 한 프레임 늦음)
        scrollRect.verticalNormalizedPosition = 0f;   // 0 = 맨 아래
    }
}
