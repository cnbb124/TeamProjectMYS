/*
 * [AffinityTalkUI]
 * 호감도 Talk 화면. DialogueData(질문+선택지)를 표시하고, 선택 시:
 *   ① 호감도 증감(AffectionManager) ② NPC 초상화 무드 교체 ③ 반응 대사 출력
 *
 * [부착] DialoguePanel (또는 Talk 콘텐츠 루트)
 *
 * [인스펙터 연결]
 * - npc            : 이 NPC의 NPC_ID (호감도 저장 대상)
 * - startDialogue  : 시작 시 보여줄 DialogueData
 * - portraitImage  : NPC 초상화 Image (AvatarPanel)
 * - moodSprites    : 무드별 초상화 5종 (순서: Normal, Like, Happy, Disappointment, Betrayal)
 * - questionText   : 질문/반응 대사 TMP (QuestionPanel)
 * - choiceButtons  : 선택지 버튼들 (AnswerPanel) — 각 항목에 Button + 라벨 TMP 연결
 *
 * [이벤트]
 * onAffinityChanged : 선택으로 호감도가 바뀐 직후 발행 → 레벨/마커 UI Refresh 연결용
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class AffinityTalkUI : MonoBehaviour
{
    [System.Serializable]
    public class ChoiceButton
    {
        public Button   button;
        public TMP_Text label;
    }

    [Header("Data")]
    [SerializeField] private NPC_ID       npc;
    [SerializeField] private DialogueData startDialogue;

    [Header("Portrait")]
    [SerializeField] private Image    portraitImage;
    [Tooltip("순서: Normal, Like, Happy, Disappointment, Betrayal")]
    [SerializeField] private Sprite[] moodSprites = new Sprite[5];

    [Header("Texts")]
    [SerializeField] private TMP_Text questionText;

    [Header("Choices (AnswerPanel)")]
    [SerializeField] private List<ChoiceButton> choiceButtons = new List<ChoiceButton>();

    [Header("Events")]
    [Tooltip("호감도 변동 직후 발행 — 레벨/마커 UI의 Refresh를 연결")]
    public UnityEvent onAffinityChanged;

    private DialogueData _current;

    private void OnEnable()
    {
        ShowDialogue(startDialogue);
    }

    /// <summary>대화 묶음 표시 (질문 + 선택지 세팅).</summary>
    public void ShowDialogue(DialogueData dialogue)
    {
        _current = dialogue;
        if (dialogue == null) return;

        if (questionText != null) questionText.text = dialogue.question;
        SetPortrait(AffinityMood.Normal);

        // 선택지 버튼 채우기 / 남는 버튼은 숨김
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            ChoiceButton cb = choiceButtons[i];
            if (cb == null || cb.button == null) continue;

            if (i < dialogue.choices.Count)
            {
                int idx = i; // 클로저 캡쳐 주의
                DialogueChoice choice = dialogue.choices[i];

                cb.button.gameObject.SetActive(true);
                if (cb.label != null) cb.label.text = choice.choiceText;

                cb.button.onClick.RemoveAllListeners();
                cb.button.onClick.AddListener(() => OnChoiceSelected(idx));
            }
            else
            {
                cb.button.gameObject.SetActive(false);
            }
        }
    }

    private void OnChoiceSelected(int index)
    {
        if (_current == null || index < 0 || index >= _current.choices.Count) return;

        DialogueChoice choice = _current.choices[index];

        // ① 호감도 증감 (oneTime이면 1회만)
        if (!choice.oneTime || !choice.consumed)
        {
            if (AffectionManager.Instance != null && choice.affinityDelta != 0)
                AffectionManager.Instance.AddAffection(npc, choice.affinityDelta);
            choice.consumed = true;
            onAffinityChanged?.Invoke();
        }

        // ② 초상화 무드 교체
        SetPortrait(choice.mood);

        // ③ 반응 대사 출력
        if (questionText != null && !string.IsNullOrEmpty(choice.responseLine))
            questionText.text = choice.responseLine;
    }

    private void SetPortrait(AffinityMood mood)
    {
        if (portraitImage == null || moodSprites == null) return;
        int idx = (int)mood;
        if (idx < 0 || idx >= moodSprites.Length) return;
        if (moodSprites[idx] != null) portraitImage.sprite = moodSprites[idx];
    }
}
