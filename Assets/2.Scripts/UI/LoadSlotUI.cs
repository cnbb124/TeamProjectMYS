/*
 * [LoadSlotUI]
 * 세이브 슬롯 한 칸. 서버에서 받은 저장 요약(레벨/골드/저장시각)을 표시하고,
 * 로드 버튼을 누르면 자기 슬롯 번호를 관리자(LoadGameUI)에게 알린다.
 *
 * [프리팹 구조] (씬의 LoadDataPanel 기준)
 * LoadDataPanel (이 스크립트 부착)
 *  ├ LoadDataText (TMP)   ← 슬롯 정보 표시 (Lv/골드/시각 또는 "NEW GAME")
 *  └ Button               ← 로드 버튼 (이 슬롯 로드)
 *
 * [사용]
 * LoadGameUI가 프리팹을 5개 생성 → 각 슬롯에 Setup(slot, summary) 호출.
 * summary가 null이면 빈 슬롯("NEW GAME")으로 표시.
 */

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadSlotUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text infoText;   // LoadDataText
    [SerializeField] private Button   loadButton;  // Button

    [Header("빈 슬롯 표시")]
    [SerializeField] private string emptyLabel = "NEW GAME";

    /// <summary>이 슬롯 번호 (0~4).</summary>
    public int SlotIndex { get; private set; }

    /// <summary>저장 데이터가 있는 슬롯인지 (빈 슬롯이면 false).</summary>
    public bool HasData { get; private set; }

    /// <summary>로드 버튼 클릭 시 발행 — 자기 슬롯 번호 전달.</summary>
    public event Action<int> onLoadClicked;

    private void Awake()
    {
        if (loadButton != null)
            loadButton.onClick.AddListener(() => onLoadClicked?.Invoke(SlotIndex));
    }

    /// <summary>슬롯 채우기. summary가 null이면 빈 슬롯으로.</summary>
    public void Setup(int slot, ServerApi.SaveSummary summary)
    {
        SlotIndex = slot;
        HasData   = summary != null;

        if (infoText != null)
        {
            if (HasData)
                infoText.text = $"SLOT {slot + 1}\nLv.{summary.level}   Gold {summary.gold}\n{FormatTime(summary.updatedAt)}";
            else
                infoText.text = $"SLOT {slot + 1}\n{emptyLabel}";
        }
    }

    // 서버 저장시각 문자열 다듬기 (형식이 길면 앞부분만). 실패해도 원문 그대로.
    private string FormatTime(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        // ISO 형식(2026-07-30T14:20:...)이면 T 앞뒤로 날짜+시각만
        int t = raw.IndexOf('T');
        if (t > 0 && raw.Length >= t + 6)
            return raw.Substring(0, t) + " " + raw.Substring(t + 1, 5);
        return raw;
    }
}
