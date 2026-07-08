/*
 * [SettingMenuUI]
 * 시스템 세팅 창 루트 컨트롤러 (싱글톤 자기등록).
 * 씬에 배치만 하면 PauseMenuUI 등이 수동 연결 없이 SettingMenuUI.Instance로 자동으로 찾아 씀.
 *
 * [작동 원리]
 * 비활성 오브젝트는 FindObjectOfType으로 못 찾으므로,
 * "씬에서 켜둔 채 시작 → Awake에서 Instance 등록 → 즉시 자기 숨김" 패턴 사용.
 * ★ 씬에 배치할 때 반드시 "활성 상태"로 둘 것 (시작 시 숨김은 코드가 처리)
 *
 * [부착] 세팅 창 루트(SettingMenuUI 오브젝트)에 부착.
 *
 * [사용]
 *   SettingMenuUI.Instance.Show();   // 열기
 *   SettingMenuUI.Instance.Hide();   // 닫기
 * 닫기(X/Back) 버튼 OnClick → 이 컴포넌트의 Hide() 연결 (또는 PauseMenuUI.CloseOptions)
 */

using UnityEngine;

public class SettingMenuUI : MonoBehaviour
{
    public static SettingMenuUI Instance { get; private set; }

    [Header("표시할 패널 (비우면 이 오브젝트 자체를 켜고 끔)")]
    [SerializeField] private GameObject panel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // 씬 중복 배치 방어
            return;
        }
        Instance = this;

        Hide(); // 등록 끝났으니 시작 시엔 숨김
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>세팅 창 표시.</summary>
    public void Show()
    {
        Target().SetActive(true);
    }

    /// <summary>세팅 창 숨김. 닫기 버튼 OnClick에 연결 가능.</summary>
    public void Hide()
    {
        Target().SetActive(false);
    }

    /// <summary>현재 표시 중인지.</summary>
    public bool IsShown => Target().activeSelf;

    private GameObject Target()
    {
        return panel != null ? panel : gameObject;
    }
}
