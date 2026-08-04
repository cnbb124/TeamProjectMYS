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
    [Tooltip("화면 가장자리 클램프 여백. 단위는 Marker Parent의 로컬 단위임\n" +
             "(CanvasScaler 레퍼런스가 1920x1080이면 그 기준의 px와 같음)")]
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

    [Header("월드 마커")]
    [Tooltip("마커 크기. 키우면 화면에서 크게 보임.")]
    [SerializeField] private float markerSize = 0.05f;

    [Tooltip("마커가 최대로 커지는 거리. 이보다 가까워져도 더 커지지 않음. 0이면 안 씀.")]
    [SerializeField] private float markerMaxSizeDistance = 50f;

    [Tooltip("마커가 최대로 작아지는 거리. 이보다 멀어져도 더 작아지지 않음. 0이면 안 씀.")]
    [SerializeField] private float markerMinSizeDistance = 800f;

    [Header("색상")]
    [SerializeField] private Color markerColor = new Color(0.2f, 0.8f, 1f, 1f); // 하늘색(아군)

    [Header("디버그")]
    [Tooltip("체크 시 아군 탐색 결과를 Console에 출력 (누가 아군으로 잡혔는지 확인용)")]
    [SerializeField] private bool debugLog = false;

    private const float MarkerCanvasSize = 100f;

    private readonly List<FriendlyMarker> _edgeMarkers = new List<FriendlyMarker>();
    private readonly List<FriendlyMarker> _worldMarkers = new List<FriendlyMarker>();
    private readonly List<Player>         _allies  = new List<Player>();
    private Transform _worldRoot;
    private float _nextRefreshTime;

    private void Update()
    {
        if (!EnsureReferences()) return;

        if (Time.time >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.time + refreshInterval;
            RefreshAllies();
        }

        for (int i = 0; i < _allies.Count; i++)
        {
            Player ally = _allies[i];
            if (ally == null)
            {
                SetActiveAt(_edgeMarkers, i, false);
                SetActiveAt(_worldMarkers, i, false);
                continue;
            }
            UpdateMarker(i, ally);
        }

        // 마커 수를 아군 수에 맞춤 (남는 건 끄기)
        HideFrom(_edgeMarkers, _allies.Count);
        HideFrom(_worldMarkers, _allies.Count);
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
    // 자기 제외를 3중으로 건다 — IsMine이 기대와 다르게 나오는 경우(PhotonView 미부착 등)가 있어서,
    // 하나만 믿으면 내 함선 위에 마커가 붙는 사고가 남.
    private void RefreshAllies()
    {
        _allies.Clear();

        Player localPlayer = GameManager.Instance != null ? GameManager.Instance.playerRef : null;

        Player[] all = FindObjectsOfType<Player>();
        foreach (Player p in all)
        {
            if (p == null) continue;
            if (p.IsMine) continue;                                          // ① 내 소유
            if (localPlayer != null && p == localPlayer) continue;           // ② GameManager가 아는 내 함선
            if (playerTransform != null && p.transform == playerTransform) continue; // ③ 캐시된 내 트랜스폼
            _allies.Add(p);
        }

        if (debugLog)
        {
            string names = "";
            foreach (Player p in _allies) names += $"{p.gameObject.name}(IsMine={p.IsMine}) ";
            Debug.Log($"[FriendlyMarkerUI] Player 총 {all.Length}개 / 아군 {_allies.Count}개 → {names}" +
                      $"| localPlayer={(localPlayer != null ? localPlayer.gameObject.name : "null")}");
        }
    }

    private void UpdateMarker(int index, Player ally)
    {
        Vector3 originPos = playerTransform != null ? playerTransform.position : mainCam.transform.position;
        float   dist      = Vector3.Distance(originPos, ally.transform.position);

        if (maxDistance > 0f && dist > maxDistance)
        {
            SetActiveAt(_edgeMarkers, index, false);
            SetActiveAt(_worldMarkers, index, false);
            return;
        }

        // 화면 안/밖 판정은 뷰포트(0~1) 기준. 픽셀(Screen.width)로 재면 VR에서 눈 텍스처
        // 해상도와 창 해상도가 달라 통째로 어긋남.
        Vector3 viewportPos = mainCam.WorldToViewportPoint(
            ally.transform.position,
            Camera.MonoOrStereoscopicEye.Mono);

        bool isOnScreen = viewportPos.z > 0f
            && viewportPos.x > 0f && viewportPos.x < 1f
            && viewportPos.y > 0f && viewportPos.y < 1f;

        if (!isOnScreen && hideWhenOffScreen)
        {
            SetActiveAt(_edgeMarkers, index, false);
            SetActiveAt(_worldMarkers, index, false);
            return;
        }

        if (isOnScreen)
        {
            SetActiveAt(_edgeMarkers, index, false);

            FriendlyMarker world = GetWorldMarker(index);
            world.transform.parent.gameObject.SetActive(true);
            world.transform.parent.position = ally.transform.position;

            world.SetOffScreen(false, 0f);
            world.Rect.localScale = Vector3.one * onScreenScale;
            world.SetName(GetPlayerName(ally));
            return;
        }

        SetActiveAt(_worldMarkers, index, false);

        FriendlyMarker m = GetEdgeMarker(index);
        m.gameObject.SetActive(true);

        // 마커 좌표는 부모 사각형의 로컬 단위로 다룸 — CanvasScaler가 걸려 있으면 로컬 단위가
        // 픽셀과 다르므로(레퍼런스 해상도 기준), 픽셀로 계산하면 다른 해상도에서 위치가 어긋남.
        Rect parentRect = markerParent.rect;
        Vector2 center = parentRect.center;
        Vector2 localPos = new Vector2(
            parentRect.xMin + viewportPos.x * parentRect.width,
            parentRect.yMin + viewportPos.y * parentRect.height);

        {
            // 카메라 뒤쪽이면 투영이 반전돼서 나오므로 사각형 중심 기준으로 되뒤집음
            if (viewportPos.z < 0f)
            {
                localPos = center - (localPos - center);
            }

            Vector2 dir = localPos - center;
            if (dir.sqrMagnitude < 0.0001f)
            {
                // 정확히 중심이면 방향이 정해지지 않음 — 위쪽으로 몰아둠
                dir = Vector2.up;
            }
            dir.Normalize();

            // 부모 사각형 가장자리까지 늘렸을 때의 배율 중 작은 쪽이 실제 접점
            float maxX   = parentRect.width  * 0.5f - edgePadding;
            float maxY   = parentRect.height * 0.5f - edgePadding;
            float scaleX = dir.x != 0f ? maxX / Mathf.Abs(dir.x) : float.MaxValue;
            float scaleY = dir.y != 0f ? maxY / Mathf.Abs(dir.y) : float.MaxValue;

            m.Rect.localPosition = center + dir * Mathf.Min(scaleX, scaleY);
            m.Rect.localScale    = Vector3.one * offScreenScale;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            m.SetOffScreen(true, angle);
        }

        m.SetName(GetPlayerName(ally));
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

    private FriendlyMarker GetEdgeMarker(int index)
    {
        while (_edgeMarkers.Count <= index)
        {
            GameObject go = Instantiate(markerPrefab, markerParent);
            _edgeMarkers.Add(BuildMarker(go));
        }

        return _edgeMarkers[index];
    }

    private FriendlyMarker GetWorldMarker(int index)
    {
        while (_worldMarkers.Count <= index)
        {
            if (_worldRoot == null)
            {
                _worldRoot = new GameObject("FriendlyWorldMarkers").transform;
            }

            GameObject holder = new GameObject(
                "FriendlyWorldMarker",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(WorldBillboard));
            holder.layer = markerPrefab.layer;
            holder.transform.SetParent(_worldRoot, false);

            RectTransform holderRect = holder.GetComponent<RectTransform>();
            holderRect.sizeDelta = new Vector2(MarkerCanvasSize, MarkerCanvasSize);
            holderRect.pivot = new Vector2(0.5f, 0.5f);

            holder.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            holder.GetComponent<WorldBillboard>().Configure(
                markerSize,
                markerMaxSizeDistance,
                markerMinSizeDistance);

            GameObject go = Instantiate(markerPrefab, holder.transform);
            RectTransform goRect = go.GetComponent<RectTransform>();
            if (goRect != null)
            {
                goRect.anchorMin = new Vector2(0.5f, 0.5f);
                goRect.anchorMax = new Vector2(0.5f, 0.5f);
                goRect.pivot = new Vector2(0.5f, 0.5f);
                goRect.anchoredPosition = Vector2.zero;
            }

            _worldMarkers.Add(BuildMarker(go));
        }

        return _worldMarkers[index];
    }

    private FriendlyMarker BuildMarker(GameObject go)
    {
        FriendlyMarker marker = go.GetComponent<FriendlyMarker>();
        if (marker == null)
        {
            Debug.LogError("[FriendlyMarkerUI] markerPrefab에 FriendlyMarker 스크립트가 없습니다.");
            marker = go.AddComponent<FriendlyMarker>();
        }

        marker.SetColor(markerColor);
        return marker;
    }

    private void HideFrom(List<FriendlyMarker> list, int startIndex)
    {
        for (int i = startIndex; i < list.Count; i++)
        {
            SetActiveAt(list, i, false);
        }
    }

    private void SetActiveAt(List<FriendlyMarker> list, int index, bool active)
    {
        if (index >= list.Count)
        {
            return;
        }

        GameObject target = list == _worldMarkers
            ? list[index].transform.parent.gameObject
            : list[index].gameObject;

        if (target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private void OnDestroy()
    {
        if (_worldRoot != null)
        {
            Destroy(_worldRoot.gameObject);
        }
    }
}
