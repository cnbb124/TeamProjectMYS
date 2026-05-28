using UnityEngine;
using UnityEngine.UI;

public class EnemyHPBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image hpFill;

    [Header("Settings")]
    [SerializeField] private bool hideWhenFull = true;  // 풀피면 숨기기
    [SerializeField] private Vector3 offset = new Vector3(0f, 2f, 0f); // 머리 위 위치

    private Unit _unit;
    private Camera _cam;
    private Canvas _canvas;

    void Awake()
    {
        _unit   = GetComponentInParent<Unit>();
        _cam    = Camera.main;
        _canvas = GetComponent<Canvas>();
    }

    void Update()
    {
        if (_unit == null || _cam == null) return;

        // 위치: 적 머리 위
        transform.position = _unit.transform.position + offset;

        // 빌보드: 항상 카메라를 향함
        transform.LookAt(
            transform.position + _cam.transform.rotation * Vector3.forward,
            _cam.transform.rotation * Vector3.up
        );

        // HP 비율 업데이트
        float ratio = _unit.maxHpRemaining > 0
            ? (float)_unit.curHpRemaining / _unit.maxHpRemaining
            : 0f;

        hpFill.fillAmount = ratio;

        // 색상: 초록 → 노랑 → 빨강
        hpFill.color = Color.Lerp(Color.red, Color.green, ratio);

        // 풀피일 때 숨기기
        if (_canvas != null)
            _canvas.enabled = !(hideWhenFull && ratio >= 1f);
    }
}