/*
 * [BoundaryAlertUI]
 * 맵 경계 이탈 경고 UI. 배틀필드식 "전장 이탈 → 복귀 카운트다운".
 * WorldBoundary가 조절하는 비네트 이미지 알파를 읽어 "경계를 넘었는지"를 판단하고,
 * 넘은 동안 경고 오브젝트(AlertCountdownUI)를 blinkInterval 간격으로 깜빡이며 남은 시간을 표시한다.
 *
 * [왜 이 방식인가]
 * WorldBoundary(맵 담당 종찬님 파일)를 안 건드리려고, 이미 그쪽이 조절 중인 비네트 알파를 신호로 씀.
 * 알파 > 임계값이면 경계 밖 → 경고 표시. 별도 플레이어 참조/거리 계산 불필요.
 *
 * [부착 위치]
 * 비네트 '부모' 오브젝트(항상 활성)에 부착. 자식 AlertCountdownUI를 깜빡이므로
 * 자기 자신을 끄면 Update가 멈춰 깜빡임이 안 됨 — 반드시 부모에 둘 것 (AlertSystemUI와 동일).
 *
 * [인스펙터 연결]
 * - alertRoot      : 경계 밖일 때 켜둘 경고 오브젝트 (AlertCountdownUI) — 깜빡이지 않고 계속 표시
 * - blinkTarget    : 깜빡일 대상 (보통 countdownText와 같은 오브젝트). 비우면 countdownText를 깜빡임
 * - vignetteImage  : WorldBoundary가 알파를 조절하는 비네트 Image (형제)
 * - countdownText  : 남은 시간 표시 TMP (0:05 형식)
 * - returnTime     : WorldBoundary.returnTime과 같은 값으로 (기본 10)
 * - (선택) boundary : WorldBoundary 연결 시 returnTime을 자동으로 가져옴
 *
 * ※ 카운트다운은 이 UI가 자체 타이머로 셈. WorldBoundary의 실제 타이머와 시작 시점이 거의 같아
 *   (알파가 오르는 순간 = 경계 넘는 순간) 큰 오차 없이 맞음. 복귀(알파 0)하면 타이머 리셋.
 *   나중에 WorldBoundary가 남은시간을 이벤트로 넘겨주면 그걸 쓰는 게 더 정확함(종찬님 협의).
 */

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BoundaryAlertUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("경계 밖일 때 켜둘 경고 오브젝트 (AlertCountdownUI). 깜빡이지 않고 계속 표시")]
    [SerializeField] private GameObject alertRoot;

    [Tooltip("깜빡일 대상 (보통 카운트다운 텍스트). 비우면 countdownText를 깜빡임")]
    [SerializeField] private GameObject blinkTarget;

    [Tooltip("WorldBoundary가 알파를 조절하는 비네트 Image")]
    [SerializeField] private Image vignetteImage;

    [Tooltip("남은 시간 표시 TMP (0:05 형식)")]
    [SerializeField] private TMP_Text countdownText;

    [Header("설정")]
    [Tooltip("경고 깜빡임 간격(초). 켜짐/꺼짐이 이 간격으로 번갈아")]
    [SerializeField] private float blinkInterval = 1.5f;

    [Tooltip("비네트 알파가 이 값보다 크면 '경계 밖'으로 판단")]
    [SerializeField] private float alphaThreshold = 0.05f;

    [Tooltip("복귀 제한 시간(초). WorldBoundary.returnTime과 맞출 것")]
    [SerializeField] private float returnTime = 10f;

    [Tooltip("(선택) 연결하면 returnTime을 여기서 자동으로 가져옴")]
    [SerializeField] private WorldBoundary boundary;

    private float _outOfBoundsTimer;   // 경계 밖에 머문 시간
    private float _blinkTimer;         // 깜빡임 위상
    private bool  _blinkOn;
    private bool  _wasOutOfBounds;

    // 깜빡일 실제 대상 — blinkTarget 우선, 없으면 countdownText 오브젝트
    private GameObject BlinkObj =>
        blinkTarget != null ? blinkTarget
        : (countdownText != null ? countdownText.gameObject : null);

    private void Start()
    {
        // WorldBoundary가 연결돼 있으면 제한 시간을 맞춰옴 (인스펙터 값 불일치 방지)
        if (boundary != null) returnTime = boundary.returnTime;

        if (alertRoot != null) alertRoot.SetActive(false);
    }

    private void Update()
    {
        bool outOfBounds = vignetteImage != null && vignetteImage.color.a > alphaThreshold;

        if (!outOfBounds)
        {
            // 안전지대 복귀 — 경고 끄고 초기화
            if (_wasOutOfBounds) ResetAlert();
            _wasOutOfBounds = false;
            return;
        }

        // 방금 경계를 넘은 순간 — 타이머/깜빡임 시작(즉시 표시)
        if (!_wasOutOfBounds)
        {
            _outOfBoundsTimer = 0f;
            _blinkTimer = 0f;
            _blinkOn = true;
            if (alertRoot != null) alertRoot.SetActive(true);   // 경고판은 계속 켜둠
            SetBlinkVisible(true);
        }
        _wasOutOfBounds = true;

        _outOfBoundsTimer += Time.deltaTime;

        UpdateBlink();
        UpdateCountdown();
    }

    // blinkInterval 간격으로 '깜빡일 대상만' 켜짐/꺼짐 토글 (경고판 전체는 계속 켜둠)
    private void UpdateBlink()
    {
        _blinkTimer += Time.deltaTime;
        if (_blinkTimer >= blinkInterval)
        {
            _blinkTimer -= blinkInterval;
            _blinkOn = !_blinkOn;
            SetBlinkVisible(_blinkOn);
        }
    }

    private void SetBlinkVisible(bool visible)
    {
        GameObject obj = BlinkObj;
        if (obj != null) obj.SetActive(visible);
    }

    // 남은 시간 표시 (분:초, 예 0:05). 올림이라 0.1초 남아도 1초로 보임.
    private void UpdateCountdown()
    {
        if (countdownText == null) return;

        int totalSec = Mathf.CeilToInt(Mathf.Max(0f, returnTime - _outOfBoundsTimer));
        int min = totalSec / 60;
        int sec = totalSec % 60;
        countdownText.text = $"{min}:{sec:00}";   // 0:05 형식
    }

    private void ResetAlert()
    {
        _outOfBoundsTimer = 0f;
        _blinkTimer = 0f;
        _blinkOn = false;
        if (alertRoot != null) alertRoot.SetActive(false);
        SetBlinkVisible(true);   // 다음에 켜질 때 보이도록 원복
    }
}
