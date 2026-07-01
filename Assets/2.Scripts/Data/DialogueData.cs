/*
 * [DialogueData]
 * 호감도 대화 한 묶음(질문 + 선택지들)을 담는 SO. Talk 화면이 이 데이터를 읽어 질문/선택지를 표시.
 *
 * [만들기]
 * Project 창 우클릭 → Create → Affinity → Dialogue
 *
 * [구성]
 * question : NPC가 던지는 질문/대사
 * choices  : 선택지 목록 — 각 선택지는 호감도 증감(+/-), 무드(초상화), 반응 대사를 가짐
 *
 * [무드(AffinityMood)]
 * 선택에 대한 NPC의 즉각 감정 표현. 마커/레벨과 별개로 "초상화 이미지"만 교체.
 * Normal / Like / Happy / Disappointment / Betrayal (5종)
 */

using System.Collections.Generic;
using UnityEngine;

/// <summary>NPC 즉각 감정(초상화 교체용). 레벨/마커와 별개.</summary>
public enum AffinityMood
{
    Normal,         // 평상시
    Like,           // 약간 호감
    Happy,          // 매우 기뻐함
    Disappointment, // 실망
    Betrayal        // 배신감
}

[System.Serializable]
public class DialogueChoice
{
    [TextArea]
    [Tooltip("선택지 버튼에 표시될 문구")]
    public string choiceText;

    [Tooltip("호감도 증감 (양수=상승, 음수=하락)")]
    public int affinityDelta = 0;

    [Tooltip("이 선택 시 NPC 무드 → 해당 초상화로 교체")]
    public AffinityMood mood = AffinityMood.Normal;

    [TextArea]
    [Tooltip("선택 후 NPC가 하는 반응 대사")]
    public string responseLine;

    [Tooltip("체크 시 호감도 증감이 처음 1회만 적용됨 (반복 파밍 방지)")]
    public bool oneTime = false;

    [Tooltip("이 선택 후 이어질 다음 대화. 비워두면 responseLine만 출력하고 대화 종료(분기 끝).")]
    public DialogueData nextDialogue;

    // 런타임: 이미 호감도 반영했는지 (oneTime용). SO 원본 오염 방지 위해 직렬화 안 함.
    [System.NonSerialized] public bool consumed = false;
}

[CreateAssetMenu(fileName = "Dialogue", menuName = "Affinity/Dialogue")]
public class DialogueData : ScriptableObject
{
    [TextArea]
    [Tooltip("NPC가 던지는 질문/대사")]
    public string question;

    public List<DialogueChoice> choices = new List<DialogueChoice>();
}
