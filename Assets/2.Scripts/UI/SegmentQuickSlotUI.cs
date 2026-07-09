/*
 * [SegmentQuickSlotUI]
 * 부채꼴(파이 조각) 퀵슬롯 UI — 반원을 조각으로 나눠 지정 키로 발동하는 방식.
 * 좌반원=아이템, 우반원=스킬처럼 이 컴포넌트를 2개 배치해서 사용.
 *
 * [원리]
 * Image Type=Filled + Radial360으로 원 스프라이트를 부채꼴로 잘라 조각 수만큼 회전 배치.
 * 조각별 지정 키(KeyCode) 입력 → 하이라이트 플래시 + onSlotUsed 이벤트 발행.
 *
 * [씬 구성]
 * ItemQuickSlot (이 스크립트 부착, 앵커/피벗 중앙)
 *  └ (조각들은 런타임 자동 생성)
 *
 * [조각 프리팹 (wedgePrefab)]
 * Wedge (Image — 원형 스프라이트(Knob/원 아무거나), 앵커·피벗 중앙)
 *  └ Icon (Image, 이름 "Icon") — 아이템/스킬 아이콘 (선택)
 * ※ Image Type 설정은 코드가 자동 처리 (Filled/Radial360)
 *
 * [인스펙터 설정 예]
 * 좌반원(아이템): startAngle = 180, totalAngle = 180, keys = [Alpha1, Alpha2, Alpha3]
 * 우반원(스킬)  : startAngle = 0,   totalAngle = 180, keys = [Alpha4, Alpha5, Alpha6]
 * (startAngle: 0=12시 기준 시계방향 도(deg). 조각 수 = keys 배열 길이)
 *
 * [외부 연동]
 * onSlotUsed(index)      : 조각 발동 시 — 실제 아이템/스킬 사용 로직 연결
 * SetIcon(index, sprite) : 조각 아이콘 세팅
 */

using System;
using UnityEngine;
using UnityEngine.UI;

public class SegmentQuickSlotUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject wedgePrefab;   // 원형 스프라이트 Image 프리팹

    [Header("부채꼴 구성")]
    [Tooltip("시작 각도(도). 0=12시, 시계방향. 좌반원=180, 우반원=0")]
    [SerializeField] private float startAngle = 180f;
    [Tooltip("전체 펼침 각도. 반원=180")]
    [SerializeField] private float totalAngle = 180f;
    [Tooltip("조각 크기(원 지름). 부모 기준")]
    [SerializeField] private float diameter = 300f;
    [Tooltip("아이콘을 놓을 중심 거리 (0~반지름)")]
    [SerializeField] private float iconRadius = 100f;
    [Tooltip("조각 사이 간격(도) — 경계선 느낌")]
    [SerializeField] private float gapAngle = 2f;

    [Header("발동 키 (조각 수 = 배열 길이)")]
    [SerializeField] private KeyCode[] keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };

    [Header("색상")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 150f / 255f); // 흰색, 알파 150
    [SerializeField] private Color flashColor  = new Color(0.3f, 0.7f, 1f, 1f);   // 발동 순간
    [SerializeField] private float flashTime   = 0.15f;                            // 플래시 지속

    /// <summary>조각 발동 시 발행 (index) — 아이템/스킬 사용 로직 연결.</summary>
    public event Action<int> onSlotUsed;

    private Image[] _wedges;
    private Image[] _icons;
    private float[] _flashTimers;

    private void Start()
    {
        BuildWedges();
    }

    private void Update()
    {
        if (_wedges == null) return;

        // 지정 키 입력 → 발동
        for (int i = 0; i < keys.Length; i++)
        {
            if (Input.GetKeyDown(keys[i]))
                Use(i);
        }

        // 플래시 페이드
        for (int i = 0; i < _wedges.Length; i++)
        {
            if (_flashTimers[i] > 0f)
            {
                _flashTimers[i] -= Time.deltaTime;
                float t = Mathf.Clamp01(_flashTimers[i] / flashTime);
                if (_wedges[i] != null)
                    _wedges[i].color = Color.Lerp(normalColor, flashColor, t);
            }
        }
    }

    // ── 조각 생성 ──
    private void BuildWedges()
    {
        if (wedgePrefab == null) return;

        int count = Mathf.Max(1, keys.Length);
        float segAngle = totalAngle / count;

        _wedges      = new Image[count];
        _icons       = new Image[count];
        _flashTimers = new float[count];

        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(wedgePrefab, transform);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(diameter, diameter);

            // 부채꼴 세팅 — Radial360으로 원을 조각으로 자름
            Image img = go.GetComponent<Image>();
            img.type            = Image.Type.Filled;
            img.fillMethod      = Image.FillMethod.Radial360;
            img.fillOrigin      = (int)Image.Origin360.Top;   // 12시 기준
            img.fillClockwise   = true;
            img.fillAmount      = (segAngle - gapAngle) / 360f;
            img.color           = normalColor;

            // 조각 위치로 회전 (fillOrigin이 12시이므로 시작각만큼 반시계로 돌림)
            float wedgeStart = startAngle + segAngle * i + gapAngle * 0.5f;
            rt.localRotation = Quaternion.Euler(0f, 0f, -wedgeStart);

            // 아이콘 — 조각의 중앙각 방향에 배치 + 역회전으로 똑바로 세움
            Transform iconTr = go.transform.Find("Icon");
            if (iconTr != null)
            {
                float midAngle = wedgeStart + (segAngle - gapAngle) * 0.5f;
                float rad = midAngle * Mathf.Deg2Rad;

                RectTransform iconRt = iconTr as RectTransform;
                // 부모(조각)가 회전돼 있으므로, 회전 전 좌표계(12시 기준)로 배치
                float localRad = ((segAngle - gapAngle) * 0.5f) * Mathf.Deg2Rad;
                iconRt.anchoredPosition = new Vector2(Mathf.Sin(localRad), Mathf.Cos(localRad)) * iconRadius;
                iconRt.localRotation = Quaternion.Euler(0f, 0f, wedgeStart); // 역회전 — 아이콘 똑바로

                _icons[i] = iconTr.GetComponent<Image>();
            }

            _wedges[i] = img;
        }
    }

    // ── 발동 ──
    private void Use(int index)
    {
        if (index < 0 || index >= _wedges.Length) return;
        _flashTimers[index] = flashTime;   // 하이라이트 플래시
        onSlotUsed?.Invoke(index);         // 실제 사용 로직은 구독자가 처리
    }

    /// <summary>조각 아이콘 세팅 (아이템/스킬 데이터 연동용).</summary>
    public void SetIcon(int index, Sprite sprite)
    {
        if (_icons == null || index < 0 || index >= _icons.Length) return;
        if (_icons[index] != null)
        {
            _icons[index].sprite  = sprite;
            _icons[index].enabled = sprite != null;
        }
    }
}
