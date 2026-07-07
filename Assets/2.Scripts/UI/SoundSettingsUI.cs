/*
 * [SoundSettingsUI]
 * 시스템 세팅 - 사운드 탭. Master/BGM/SFX/UI 볼륨 슬라이더 4개를
 * SoundManager와 연동하고 PlayerPrefs로 저장/복원.
 *
 * [볼륨 매핑]
 * - Master : AudioListener.volume        (전체 소리에 곱해짐)
 * - BGM    : SoundManager.SetBGMVolume
 * - SFX    : SoundManager.SetSFX3DVolume (전투/월드 효과음)
 * - UI     : SoundManager.SetSFXUIVolume (버튼/알림음)
 *
 * [부착] SoundPanel에 부착.
 *
 * [인스펙터 연결]
 * - 각 슬라이더 (Min 0 / Max 1 확인!)
 * - 퍼센트 텍스트는 선택 (연결하면 "80%" 형식 표시)
 * - 슬라이더 OnValueChanged는 비워둠 — Start에서 자동 연결
 *
 * [저장]
 * 슬라이더 조작 즉시 적용 + PlayerPrefs 저장. 게임 재시작해도 유지됨.
 * 다른 씬에서 시작해도 SoundManager 볼륨을 맞추려면 ApplySavedVolumes()를 호출하면 됨
 * (이 컴포넌트가 OnEnable에서 자동으로 처리하므로 세팅창이 한 번이라도 열리면 반영됨).
 */

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SoundSettingsUI : MonoBehaviour
{
    // PlayerPrefs 키
    private const string KEY_MASTER = "Vol_Master";
    private const string KEY_BGM    = "Vol_BGM";
    private const string KEY_SFX    = "Vol_SFX";
    private const string KEY_UI     = "Vol_UI";

    [Header("Sliders (Min 0 / Max 1)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider uiSlider;

    [Header("Percent Texts (선택)")]
    [SerializeField] private TMP_Text masterText;
    [SerializeField] private TMP_Text bgmText;
    [SerializeField] private TMP_Text sfxText;
    [SerializeField] private TMP_Text uiText;

    private void Start()
    {
        // 슬라이더 리스너 자동 연결 (OnValueChanged는 인스펙터에서 비워둘 것)
        if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (bgmSlider != null)    bgmSlider.onValueChanged.AddListener(OnBGMChanged);
        if (sfxSlider != null)    sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        if (uiSlider != null)     uiSlider.onValueChanged.AddListener(OnUIChanged);
    }

    private void OnEnable()
    {
        // 저장값 로드 → 슬라이더 위치 복원 + 실제 볼륨 적용
        ApplySavedVolumes();
    }

    /// <summary>PlayerPrefs 저장값을 슬라이더/사운드에 반영.</summary>
    public void ApplySavedVolumes()
    {
        float master = PlayerPrefs.GetFloat(KEY_MASTER, 1f);
        float bgm    = PlayerPrefs.GetFloat(KEY_BGM,    1f);
        float sfx    = PlayerPrefs.GetFloat(KEY_SFX,    1f);
        float ui     = PlayerPrefs.GetFloat(KEY_UI,     1f);

        // SetValueWithoutNotify: 리스너 중복 발동 없이 슬라이더 위치만 복원
        if (masterSlider != null) masterSlider.SetValueWithoutNotify(master);
        if (bgmSlider != null)    bgmSlider.SetValueWithoutNotify(bgm);
        if (sfxSlider != null)    sfxSlider.SetValueWithoutNotify(sfx);
        if (uiSlider != null)     uiSlider.SetValueWithoutNotify(ui);

        ApplyMaster(master);
        ApplyBGM(bgm);
        ApplySFX(sfx);
        ApplyUI(ui);
    }

    // ── 슬라이더 콜백: 적용 + 저장 ──

    private void OnMasterChanged(float v)
    {
        ApplyMaster(v);
        PlayerPrefs.SetFloat(KEY_MASTER, v);
    }

    private void OnBGMChanged(float v)
    {
        ApplyBGM(v);
        PlayerPrefs.SetFloat(KEY_BGM, v);
    }

    private void OnSFXChanged(float v)
    {
        ApplySFX(v);
        PlayerPrefs.SetFloat(KEY_SFX, v);
    }

    private void OnUIChanged(float v)
    {
        ApplyUI(v);
        PlayerPrefs.SetFloat(KEY_UI, v);

        // 조작 피드백 — 바뀐 볼륨으로 UI 효과음 재생 (SOUND_TYPE에 버튼음 있으면 교체)
        // if (SoundManager.Instance != null) SoundManager.Instance.PlaySFXUI(SOUND_TYPE.SFX_UI_CLICK);
    }

    // ── 실제 적용 (SoundManager 없는 테스트 씬에서도 에러 안 나게 가드) ──

    private void ApplyMaster(float v)
    {
        AudioListener.volume = Mathf.Clamp01(v);
        SetPercentText(masterText, v);
    }

    private void ApplyBGM(float v)
    {
        if (SoundManager.Instance != null) SoundManager.Instance.SetBGMVolume(v);
        SetPercentText(bgmText, v);
    }

    private void ApplySFX(float v)
    {
        if (SoundManager.Instance != null) SoundManager.Instance.SetSFX3DVolume(v);
        SetPercentText(sfxText, v);
    }

    private void ApplyUI(float v)
    {
        if (SoundManager.Instance != null) SoundManager.Instance.SetSFXUIVolume(v);
        SetPercentText(uiText, v);
    }

    private void SetPercentText(TMP_Text label, float v)
    {
        if (label != null) label.text = $"{Mathf.RoundToInt(v * 100f)}%";
    }

    private void OnDisable()
    {
        PlayerPrefs.Save(); // 세팅창 닫힐 때 디스크에 확정 저장
    }
}
