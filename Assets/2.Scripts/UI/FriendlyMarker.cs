/*
 * [FriendlyMarker]
 * 아군 마커 1개(=아군 1명)의 표시 담당. FriendlyPlayerUI 프리팹 루트에 부착.
 * 위치 계산과 대상 선정은 FriendlyMarkerUI가 하고, 이 스크립트는 "받은 값을 그리기"만 함.
 *
 * [왜 프리팹이 참조를 들고 있나]
 * 자식 이름으로 찾는 방식(transform.Find)은 이름이 조금만 달라도 조용히 실패함.
 * 프리팹 안에서 미리 연결해두면 인스턴스 전부가 그 참조를 물려받아 그런 사고가 안 남.
 *
 * [부착 / 연결]
 * 1. FriendlyPlayerUI 프리팹 루트에 이 스크립트 부착
 * 2. 아래 필드에 자식들을 드래그
 *      Name Text     : 아군 닉네임 (PlayerTag)
 *      Distance Text : 거리 표시 (아래 숫자)
 *      Arrow Image   : 화면 밖 방향 화살표 (없으면 비워둠)
 * 3. 이 프리팹을 FriendlyMarkerUI의 Marker Prefab에 연결
 */

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FriendlyMarker : MonoBehaviour
{
    [Header("표시 요소 (프리팹 안에서 연결)")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text distanceText;

    [Tooltip("아군을 가리키는 화살표. 없으면 비워둬도 됨")]
    [SerializeField] private Image    arrowImage;

    [Tooltip("체크 시 화면 안에 있는 아군은 화살표를 숨김.\n" +
             "아이콘 없이 화살표만 쓰는 구성이면 꺼둘 것 — 켜면 화면 안 아군은 글자만 남음")]
    [SerializeField] private bool     hideArrowOnScreen = false;

    [Tooltip("화살표 이미지가 '원래' 향하고 있는 방향 보정값(도).\n" +
             "위(12시)를 향해 그렸으면 0 / 아래(6시) 180 / 오른쪽 -90 / 왼쪽 90")]
    [SerializeField] private float    arrowAngleOffset = 180f;

    [Tooltip("체크 시 화살표가 회전하지 않고 항상 같은 방향으로 고정됨.\n" +
             "화면 밖 아군 방향을 가리키게 하려면 체크 해제")]
    [SerializeField] private bool     lockArrowRotation = true;

    private RectTransform _rect;

    /// <summary>이 마커의 RectTransform (위치 조정용).</summary>
    public RectTransform Rect
    {
        get
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            return _rect;
        }
    }

    /// <summary>아군 이름 표시. 빈 문자열이면 텍스트를 숨김.</summary>
    public void SetName(string playerName)
    {
        if (nameText == null) return;
        nameText.text = playerName;
        nameText.gameObject.SetActive(!string.IsNullOrEmpty(playerName));
    }

    /// <summary>거리 표시(m 단위 반올림).</summary>
    public void SetDistance(float distance)
    {
        if (distanceText == null) return;
        distanceText.text = $"{Mathf.RoundToInt(distance)}m";
    }

    /// <summary>
    /// 마커 색 지정. 아이콘을 안 쓰므로 현재는 화살표에만 적용된다.
    /// (화살표도 안 쓰면 아무 효과 없음 — 색은 프리팹에서 직접 칠하면 됨)
    /// </summary>
    public void SetColor(Color color)
    {
        if (arrowImage != null) arrowImage.color = color;
    }

    /// <summary>
    /// 화면 밖 상태 갱신.
    /// 화면 밖이면 화살표를 아군 방향으로 회전시킨다.
    /// 화면 안일 때는 hideArrowOnScreen 설정에 따라 숨기거나(아이콘이 따로 있는 구성),
    /// 회전만 원래대로 되돌리고 계속 표시한다(화살표가 곧 마커인 구성).
    /// </summary>
    public void SetOffScreen(bool offScreen, float angleDeg)
    {
        if (arrowImage == null) return;

        bool visible = offScreen || !hideArrowOnScreen;
        arrowImage.gameObject.SetActive(visible);
        if (!visible) return;

        // lockArrowRotation이면 방향 계산을 무시하고 항상 arrowAngleOffset 각도로 고정.
        // angleDeg는 "위를 향해 그린 화살표" 기준이라, 회전할 때만 아트 방향 보정을 더한다.
        float finalAngle = (lockArrowRotation || !offScreen)
            ? arrowAngleOffset
            : angleDeg + arrowAngleOffset;

        arrowImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, finalAngle);
    }
}
