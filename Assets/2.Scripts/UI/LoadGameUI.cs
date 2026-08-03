/*
 * [LoadGameUI]
 * 게임 로드 화면 관리자. 서버에서 세이브 슬롯 목록을 받아 슬롯 UI를 5개 생성하고,
 * 슬롯을 고르면 그 데이터를 로드해 정거장(STATION)에서 이어서 시작한다.
 *
 * ★B안: SaveData에 씬/위치 정보가 없어 "저장 지점 그대로 복귀"는 불가.
 *   대신 캐릭터 상태(레벨/장비/골드 등)를 복원하고 STATION에서 재개한다.
 *   (위치 복귀는 서버 스키마에 sceneName/position 추가 후 가능 — 발표 후 과제)
 *
 * [흐름]
 *   OnEnable → ServerApi.ListSavesCo → 슬롯 0~4 생성(저장된 건 정보, 빈 건 NEW GAME)
 *   슬롯 클릭 → LoadingManager.NextScene = STATION → GameManager.LoadGame(slot)
 *            → (로드+ApplySaveData+LOADING_SEQUENCE는 GameManager가 처리)
 *
 * [부착 / 연결]
 * 1. 로드 화면 루트(Panel)에 이 스크립트 부착
 * 2. Slot Prefab   : LoadSlotUI가 붙은 슬롯 프리팹 (씬의 LoadDataPanel을 프리팹화)
 * 3. Slot Parent   : 슬롯이 생성될 곳 (Scroll View > Content)
 * 4. (선택) Status Text : "불러오는 중..." / 에러 메시지 표시
 *
 * ※ 로그인이 돼 있어야 목록이 옴(ServerApi.IsLoggedIn). 로그인 화면 이후에 오는 구조.
 */

using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LoadGameUI : MonoBehaviour
{
    // 같은 슬롯 목록 UI를 로드/저장 양쪽에 씀 — 슬롯 클릭 시 동작만 다름
    public enum Mode { Load, Save }

    [Header("모드")]
    [Tooltip("Load: 슬롯 클릭 시 로드 / Save: 슬롯 클릭 시 그 슬롯에 저장")]
    [SerializeField] private Mode mode = Mode.Load;

    [Header("슬롯 생성")]
    [SerializeField] private GameObject    slotPrefab;   // LoadSlotUI 프리팹
    [SerializeField] private Transform     slotParent;   // Scroll View > Content
    [SerializeField] private int           slotCount = 5;

    [Header("상태 표시 (선택)")]
    [SerializeField] private TMP_Text statusText;

    [Header("저장 덮어쓰기 확인 팝업 (Save 모드)")]
    [Tooltip("이미 저장된 슬롯에 저장할 때 뜨는 확인 팝업 (SaveAlertUI)")]
    [SerializeField] private ConfirmDialogUI overwriteConfirm;

    [Header("로드 후 이동할 씬")]
    [Tooltip("B안: 저장 지점이 아니라 이 씬에서 재개 (기본 STATION)")]
    [SerializeField] private string destinationScene = "BASE_STATION";

    private readonly List<LoadSlotUI> _slots = new List<LoadSlotUI>();
    private bool _loading;   // 로드 진행 중 중복 클릭 방지

    private void OnEnable()
    {
        RefreshSlots();
    }

    /// <summary>슬롯 목록을 다시 그림. 로컬 세이브를 먼저 깔고 서버 목록을 그 위에 덮음.</summary>
    // 서버만 보면 비로그인·서버다운일 때 목록이 통째로 비어, 로컬에 세이브가 있어도 못 불러옴.
    // 저장은 항상 로컬에 되므로 로컬을 기본으로 깔고, 서버가 응답하면 그쪽을 우선으로 씀.
    public void RefreshSlots()
    {
        if (slotPrefab == null || slotParent == null) return;

        if (ServerApi.Instance == null || !ServerApi.Instance.IsLoggedIn)
        {
            BuildSlots(null);   // 로컬만으로 그림
            SetStatus(HasAnyLocalSave() ? "" : "저장된 게임이 없습니다.");
            return;
        }

        BuildSlots(null);       // 서버 응답 전에도 로컬 기준으로 먼저 보여줌
        SetStatus("불러오는 중...");
        ServerApi.Instance.StartCoroutine(ServerApi.Instance.ListSavesCo(
            summaries =>
            {
                BuildSlots(summaries);
                SetStatus("");
            },
            err =>
            {
                SetStatus($"서버 목록을 불러오지 못했습니다(로컬만 표시): {err}");
                BuildSlots(null);
            }));
    }

    private bool HasAnyLocalSave()
    {
        if (GameManager.Instance == null)
        {
            return false;
        }
        for (int i = 0; i < slotCount; i++)
        {
            if (GameManager.Instance.HasSave(i))
            {
                return true;
            }
        }
        return false;
    }

    // 슬롯 0~(slotCount-1) 생성. summaries에서 슬롯번호가 일치하는 것만 데이터로, 나머지는 빈 슬롯.
    private void BuildSlots(ServerApi.SaveSummary[] summaries)
    {
        // 기존 슬롯 제거
        foreach (LoadSlotUI s in _slots)
            if (s != null) Destroy(s.gameObject);
        _slots.Clear();

        for (int i = 0; i < slotCount; i++)
        {
            GameObject go = Instantiate(slotPrefab, slotParent);
            LoadSlotUI slot = go.GetComponent<LoadSlotUI>();
            if (slot == null) continue;

            slot.Setup(i, FindSummary(summaries, i));
            slot.onLoadClicked += OnSlotLoadClicked;
            _slots.Add(slot);
        }
    }

    // 해당 슬롯 번호의 저장 요약 찾기. 서버 것이 있으면 그걸, 없으면 로컬 파일에서 읽음.
    // 둘 다 없으면 null = 빈 슬롯.
    private ServerApi.SaveSummary FindSummary(ServerApi.SaveSummary[] summaries, int slot)
    {
        if (summaries != null)
        {
            foreach (ServerApi.SaveSummary s in summaries)
            {
                if (s != null && s.slot == slot)
                {
                    return s;
                }
            }
        }
        return GameManager.Instance != null ? GameManager.Instance.GetLocalSaveSummary(slot) : null;
    }

    private void OnSlotLoadClicked(int slot)
    {
        if (_loading) return;

        if (GameManager.Instance == null)
        {
            SetStatus("GameManager 없음 (MAIN 거쳐 진입 필요)");
            return;
        }

        LoadSlotUI target = _slots.Find(s => s != null && s.SlotIndex == slot);
        bool hasData = target != null && target.HasData;

        if (mode == Mode.Save) HandleSaveClick(slot, hasData);
        else                   HandleLoadClick(slot, hasData);
    }

    // ── 로드 모드 ──
    private void HandleLoadClick(int slot, bool hasData)
    {
        if (!hasData) { SetStatus("빈 슬롯입니다."); return; }   // 빈 슬롯은 로드 불가

        _loading = true;
        SetStatus("게임을 불러오는 중...");

        // B안: 저장 지점 대신 STATION에서 재개. 로드+씬전환은 GameManager.LoadGame이 처리.
        LoadingManager.NextScene = destinationScene;
        GameManager.Instance.LoadGame(slot);
    }

    // ── 저장 모드 ──
    private void HandleSaveClick(int slot, bool hasData)
    {
        // STATION 등 저장 가능한 상태인지 GameManager가 판단
        if (!GameManager.Instance.CanSave)
        {
            SetStatus("여기서는 저장할 수 없습니다. (정거장에서만 가능)");
            return;
        }

        if (hasData && overwriteConfirm != null)
        {
            // 이미 데이터가 있으면 덮어쓰기 확인 팝업 → Yes일 때만 저장
            // 문구는 SaveAlertUI 프리팹에 써둔 것을 그대로 사용(메시지 안 넘김)
            overwriteConfirm.Open(() => DoSave(slot));
        }
        else
        {
            // 빈 슬롯이거나 팝업 미연결 → 바로 저장
            DoSave(slot);
        }
    }

    private void DoSave(int slot)
    {
        GameManager.Instance.SaveGame(slot);
        SetStatus($"슬롯 {slot + 1}에 저장했습니다.");
        RefreshSlots();   // 저장 후 목록 갱신(레벨/골드/시각 반영)
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }
}
