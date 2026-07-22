/*
 * [FriendlyMarkerUI]
 * 아군(다른 플레이어) 위치 마커. EnemyMarkerUI의 아군 버전.
 * 화면 안 → 아군 위치에 마커 / 화면 밖 → 화면 가장자리에 클램프(+화살표).
 * 마커마다 닉네임과 거리(m)를 표시한다.
 *
 * [아군 판별]
 * 씬의 Player 중 IsMine == false 인 대상 = 남의 함선.
 * 싱글플레이에선 로컬 함선만 있고 그건 IsMine == true라 마커가 하나도 안 뜸(정상).
 * 매 프레임 전체 검색은 비싸므로 refreshInterval(기본 0.5초)마다만 다시 찾는다.
 *
 * [부착 / 연결]
 * 1. Canvas 하위 아무 오브젝트에 이 스크립트 부착 (EnemyMarkerUI와 같은 자리 가능)
 * 2. Marker Prefab  : FriendlyMarker 스크립트가 붙은 FriendlyPlayerUI 프리팹
 * 3. Marker Parent  : 마커를 담을 Canvas 하위 RectTransform (EnemyMarkerUI의 MarkerRoot 공용 사용 가능)
 * 4. 나머지(카메라/플레이어)는 비워두면 GameManager.playerRef / Camera.main에서 자동으로 잡음
 *
 * ※ 마커 프리팹의 앵커/피벗은 중앙(0.5, 0.5)이어야 위치가 맞음.
 */

using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class FriendlyMarkerUI : MonoBehaviour
{
    [Header("Prefab / Parent")]
    [Tooltip("FriendlyMarker가 붙은 마커 프리팹")]
    [SerializeField] private GameObject    markerPrefab;
    [Tooltip("마커가 생성될 부모 (Canvas 하위). EnemyMarkerUI의 MarkerRoot와 공유해도 됨")]
    [SerializeField] private RectTransform markerParent;

    [Header("References (비우면 자동 연결)")]
    [SerializeField] private Camera    mainCam;
    [SerializeField] private Transform playerTransform;

    [Header("설정")]
    [Tooltip("화면 가장자리 클램프 여백(px)")]
    [SerializeField] private float edgePadding    = 20f;
    [SerializeField] private float onScreenScale  = 1f;
    [SerializeField] private float offScreenScale = 0.8f;

    [Tooltip("이 거리보다 먼 아군은 숨김. 0이면 거리 제한 없음")]
    [SerializeField] private float maxDistance    = 0f;

    [Tooltip("체크 시 화면 밖 아군은 가장자리에 붙이지 않고 아예 숨김")]
    [SerializeField] private bool  hideWhenOffScreen = false;

    [Tooltip("아군 목록을 다시 검색하는 주기(초). 스폰/퇴장 반영용")]
    [SerializeField] private float refreshInterval = 0.5f;

    [Tooltip("닉네임이 없을 때 쓸 이름 형식. {0}에 포톤 액터번호가 들어감 (예: \"P{0}\" → P2)")]
    [SerializeField] private string fallbackNameFormat = "P{0}";

    [Header("색상")]
    [SerializeField] private Color markerColor = new Color(0.2f, 0.8f, 1f, 1f); // 하늘색(아군)

    private readonly List<FriendlyMarker> _markers = new List<FriendlyMarker>();
    private readonly List<Player>         _allies  = new List<Player>();
    private float _nextRefreshTime;

    private void Update()
    {
        if (!EnsureReferences()) return;

        if (Time.time >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.time + refreshInterval;
            RefreshAllies();
        }

        // 마커 수를 아군 수에 맞춤 (남는 건 끄기)
        while (_markers.Count < _allies.Count) AddMarker();
        for (int i = _allies.Count; i < _markers.Count; i++)
            _markers[i].gameObject.SetActive(false);

        for (int i = 0; i < _allies.Count; i++)
        {
            Player ally = _allies[i];
            if (ally == null)
            {
                _markers[i].gameObject.SetActive(false);
                continue;
            }
            UpdateMarker(_markers[i], ally);
        }
    }

    // 인스펙터 연결 우선, 비어있으면 GameManager.playerRef / Camera.main로 폴백
    private bool EnsureReferences()
    {
        if (playerTransform == null && GameManager.Instance != null && GameManager.Instance.playerRef != null)
            playerTransform = GameManager.Instance.playerRef.transform;

        if (mainCam == null) mainCam = Camera.main;

        return mainCam != null && markerPrefab != null && markerParent != null;
    }

    // 씬의 Player 중 내 함선이 아닌 것만 수집.
    private void RefreshAllies()
    {
        _allies.Clear();

        Player[] all = FindObjectsOfType<Player>();
        foreach (Player p in all)
        {
            if (p == null) continue;
            if (p.IsMine) continue;                       // 내 함선은 제외 (HUD가 이미 보여줌)
            if (playerTransform != null && p.transform == playerTransform) continue;
            _allies.Add(p);
        }
    }

    private void UpdateMarker(FriendlyMarker m, Player ally)
    {
        Vector3 originPos = playerTransform != null ? playerTransform.position : mainCam.transform.position;
        float   dist      = Vector3.Distance(originPos, ally.transform.position);

        if (maxDistance > 0f && dist > maxDistance)
        {
            m.gameObject.SetActive(false);
            return;
        }

        Vector3 screenPos = mainCam.WorldToScreenPoint(ally.transform.position);

        float halfW = Screen.width  * 0.5f;
        float halfH = Screen.height * 0.5f;

        bool isOnScreen = screenPos.z > 0f
            && screenPos.x > 0f && screenPos.x < Screen.width
            && screenPos.y > 0f && screenPos.y < Screen.height;

        if (!isOnScreen && hideWhenOffScreen)
        {
            m.gameObject.SetActive(false);
            return;
        }

        m.gameObject.SetActive(true);

        if (isOnScreen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                markerParent, screenPos, null, out Vector2 localPos);
            m.Rect.localPosition = localPos;

            m.SetOffScreen(false, 0f);
            m.Rect.localScale = Vector3.one * onScreenScale;
        }
        else
        {
            // 카메라 뒤쪽이면 방향이 반전돼서 나오므로 뒤집어준다
            if (screenPos.z < 0f)
            {
                screenPos.x = Screen.width  - screenPos.x;
                screenPos.y = Screen.height - screenPos.y;
            }

            Vector2 dir = new Vector2(screenPos.x - halfW, screenPos.y - halfH).normalized;

            // 화면(직사각형) 가장자리까지 늘렸을 때의 배율 중 작은 쪽이 실제 접점
            float maxX   = halfW - edgePadding;
            float maxY   = halfH - edgePadding;
            float scaleX = dir.x != 0f ? maxX / Mathf.Abs(dir.x) : float.MaxValue;
            float scaleY = dir.y != 0f ? maxY / Mathf.Abs(dir.y) : float.MaxValue;

            m.Rect.localPosition = dir * Mathf.Min(scaleX, scaleY);
            m.Rect.localScale    = Vector3.one * offScreenScale;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            m.SetOffScreen(true, angle);
        }

        m.SetName(GetPlayerName(ally));
        m.SetDistance(dist);
    }

    // 포톤 닉네임 우선, 없으면 "P + 액터번호"로 대체.
    // 현재 프로젝트는 PhotonNetwork.NickName을 설정하는 곳이 없어서 사실상 항상 아래쪽으로 감.
    // (로그인 UI가 붙어서 닉네임을 넣기 시작하면 자동으로 실제 이름이 뜬다)
    // ※ WaitingRoomUI.GetPlayerDisplayName과 같은 규칙 — 대기실과 마커에서 같은 사람이 다르게 보이면 안 되므로.
    // ※ ActorNumber는 방 입장 순서대로 1부터 증가하지만, 중간에 나갔다 들어오면 번호가 건너뛸 수 있음(P1, P3 등).
    private string GetPlayerName(Player ally)
    {
        PhotonView view = ally.GetComponent<PhotonView>();
        if (view == null || view.Owner == null)
            return ally.gameObject.name;   // 싱글/PhotonView 없는 함선

        if (!string.IsNullOrWhiteSpace(view.Owner.NickName))
            return view.Owner.NickName;

        return string.Format(fallbackNameFormat, view.Owner.ActorNumber);
    }

    private void AddMarker()
    {
        GameObject go = Instantiate(markerPrefab, markerParent);

        FriendlyMarker marker = go.GetComponent<FriendlyMarker>();
        if (marker == null)
        {
            Debug.LogError("[FriendlyMarkerUI] markerPrefab에 FriendlyMarker 스크립트가 없습니다.");
            marker = go.AddComponent<FriendlyMarker>();
        }

        marker.SetColor(markerColor);
        _markers.Add(marker);
    }
}
