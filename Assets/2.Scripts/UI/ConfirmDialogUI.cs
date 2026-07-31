/*
 * [ConfirmDialogUI]
 * 예/아니오 확인 팝업. (세이브 덮어쓰기 확인 등에 재사용)
 * Yes 누르면 등록된 동작 실행 + 닫힘, No 누르면 그냥 닫힘.
 *
 * [프리팹 구조] (SaveAlertUI 기준)
 * SaveAlertUI (이 스크립트 부착)
 *  ├ Text (TMP)   ← 메시지 (선택)
 *  ├ YesButton
 *  └ NoButton
 *
 * [사용법] 코드에서 열기:
 *   confirmDialog.Open("슬롯 1을 덮어쓸까요?", () => GameManager.Instance.SaveGame(1));
 *   → Yes 누르면 콜백 실행 후 닫힘 / No 누르면 콜백 없이 닫힘
 *
 * [연결]
 * - messageText : 메시지 TMP (선택)
 * - yesButton / noButton : 각 버튼
 * ※ 시작 시 스스로 꺼짐(SetActive false). Open()이 켜줌.
 */

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ConfirmDialogUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text messageText;   // 선택
    [SerializeField] private Button   yesButton;
    [SerializeField] private Button   noButton;

    // Yes 눌렀을 때 실행할 동작 (Open에서 넘겨받음)
    private Action _onConfirm;

    private void Awake()
    {
        if (yesButton != null) yesButton.onClick.AddListener(HandleYes);
        if (noButton  != null) noButton.onClick.AddListener(Close);

        gameObject.SetActive(false);   // 평소엔 꺼둠
    }

    /// <summary>확인 팝업 열기. Yes 누르면 onConfirm 실행.</summary>
    public void Open(string message, Action onConfirm)
    {
        _onConfirm = onConfirm;
        if (messageText != null) messageText.text = message;
        gameObject.SetActive(true);
    }

    /// <summary>팝업 닫기 (No 버튼 / 외부에서도 호출 가능).</summary>
    public void Close()
    {
        _onConfirm = null;
        gameObject.SetActive(false);
    }

    private void HandleYes()
    {
        // 콜백을 지역 변수로 옮긴 뒤 닫기 — Close가 _onConfirm을 비워도 실행 보장
        Action cb = _onConfirm;
        Close();
        cb?.Invoke();
    }
}
