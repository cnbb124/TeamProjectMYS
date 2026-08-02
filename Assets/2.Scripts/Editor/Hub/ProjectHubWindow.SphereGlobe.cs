using UnityEditor;
using UnityEngine;

// =====================================================================
// ProjectHubWindow — 구 배치판.
//
// 바깥 원   = 맵 경계(WorldBoundary.boundaryRadius). 플레이어가 갈 수 있는 끝.
// 안쪽 구   = 지금 설정한 배치 거리. 실제로 물건이 놓이는 껍질이라 색을 달리 칠함.
// 안쪽 구를 클릭하면 그 방향으로 배치 거리만큼 떨어진 자리에 놓임.
//
// 실제 배치는 PlaceOnSphere를 그대로 부름 — 부모/랜덤 반경/회전/크기 규칙이
// 씬 뷰 배치와 어긋나지 않게 하기 위함.
//
// 원근 없는 정사영임. 앞면 반구만 찍을 수 있고, 뒤를 찍으려면 돌려서 앞으로 가져와야 함.
// =====================================================================
public partial class ProjectHubWindow
{
	// 화면에 그리는 지름(픽셀). 실제 거리와 무관하게 '크게 보기'용
	private const float GlobeSizeMin = 160f;
	private const float GlobeSizeMax = 560f;
	private float _globeViewSize = 260f;

	// 드래그로 회전한 각도. x=위아래(피치), y=좌우(요)
	private Vector2 _globeAngles = new Vector2(-20f, 0f);
	private bool _globeDragged;
	private float _globeDragDistance;

	private Vector3 _globeHoverDir;
	private bool _globeHoverValid;

	// 거리를 어느 쪽 기준으로 적을지. false=맵 중심에서, true=경계에서 안쪽으로
	private bool _globeFromEdge;

	// 선 하나 그릴 때마다 배열을 새로 만들면 GC가 계속 돌아서 재사용함
	private readonly Vector3[] _globeSegment = new Vector3[2];

	private Quaternion GlobeRotation => Quaternion.Euler(_globeAngles.x, _globeAngles.y, 0f);

	// 이 씬의 맵 경계 반경. 없으면 false —
	// 없는 경계를 임의값으로 지어내면 화면의 비율 표시가 전부 거짓말이 되므로 반드시 갈라서 처리할 것
	private bool TryGetBoundaryRadius(out float radius)
	{
		radius = 0f;
		WorldBoundary boundary = Object.FindObjectOfType<WorldBoundary>(true);
		if (boundary == null || boundary.boundaryRadius <= 0f)
		{
			return false;
		}
		radius = boundary.boundaryRadius;
		return true;
	}

	// 그림에서 안쪽 구가 차지하는 비율(0~1).
	// 경계가 없으면 견줄 대상이 없으므로 배치 구를 화면 가득 그림
	private float GetInnerRatio()
	{
		if (!TryGetBoundaryRadius(out float boundaryRadius))
		{
			return 1f;
		}
		return Mathf.Clamp01(_sphereRadius / boundaryRadius);
	}

	private void DrawGlobeSection()
	{
		// 마우스 이동 이벤트(구 위 좌표 표시용)는 OnGUI에서 탭에 따라 켜고 끔
		EditorGUILayout.BeginVertical(HubStyles.Card);

		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("어디에 놓을지 고르기", HubStyles.SectionTitle);
		GUILayout.FlexibleSpace();
		if (GUILayout.Button("정면", GUILayout.Width(48f)))
		{
			_globeAngles = new Vector2(-20f, 0f);
		}
		if (GUILayout.Button("위에서", GUILayout.Width(52f)))
		{
			_globeAngles = new Vector2(-89f, 0f);
		}
		EditorGUILayout.EndHorizontal();

		HubStyles.ColoredLabel("끌면 돌아가고, 안쪽 구를 클릭하면 그 자리에 놓입니다. 앞으로 보이는 면만 찍힙니다.\n" +
							   "구 위에서 휠을 굴리면 크게/작게 볼 수 있습니다.",
			HubStyles.Muted, EditorStyles.wordWrappedMiniLabel);

		// ---- 크게 보기 ----
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("크게 보기", EditorStyles.miniLabel, GUILayout.Width(56f));
		_globeViewSize = GUILayout.HorizontalSlider(_globeViewSize, GlobeSizeMin, GlobeSizeMax);
		if (GUILayout.Button("기본", EditorStyles.miniButton, GUILayout.Width(40f)))
		{
			_globeViewSize = 260f;
		}
		EditorGUILayout.EndHorizontal();

		// 구는 가운데로
		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		Rect rect = GUILayoutUtility.GetRect(_globeViewSize, _globeViewSize, GUILayout.ExpandWidth(false));
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();

		HandleGlobeInput(rect);

		if (Event.current.type == EventType.Repaint)
		{
			DrawBoundaryRing(rect);
			DrawInnerSphere(rect);
			DrawGlobeGrid(rect);
			DrawPlacedMarkers(rect);
			DrawGlobeHover(rect);
		}

		// ---- 가리키는 좌표 ----
		if (_globeHoverValid)
		{
			GetLatLon(_globeHoverDir, out float lat, out float lon);
			HubStyles.ColoredLabel($"가리키는 곳 — 위도 {lat:0.#}° · 경도 {lon:0.#}°",
				HubStyles.Ok, EditorStyles.miniBoldLabel);
		}
		else
		{
			HubStyles.ColoredLabel("구 위에 마우스를 올리면 좌표가 여기 나옵니다.",
				HubStyles.Muted, EditorStyles.miniLabel);
		}

		HubStyles.Separator(2f);
		DrawDistanceField();

		EditorGUILayout.EndVertical();
	}

	// 배치 거리 입력. 중심 기준과 경계 기준 중 익숙한 쪽으로 적게 함(값은 하나임)
	private void DrawDistanceField()
	{
		bool hasBoundary = TryGetBoundaryRadius(out float boundaryRadius);

		// 경계가 없으면 '경계에서 안쪽으로'는 셀 기준이 없어 아예 못 고르게 함
		if (!hasBoundary)
		{
			_globeFromEdge = false;
		}

		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("거리 기준", EditorStyles.miniLabel, GUILayout.Width(56f));
		EditorGUI.BeginDisabledGroup(!hasBoundary);
		int picked = GUILayout.Toolbar(_globeFromEdge ? 1 : 0,
			new[] { "맵 중심으로부터", "경계에서 안쪽으로" }, GUILayout.Height(18f));
		_globeFromEdge = picked == 1 && hasBoundary;
		EditorGUI.EndDisabledGroup();
		EditorGUILayout.EndHorizontal();

		if (_globeFromEdge)
		{
			float fromEdge = Mathf.Max(0f, boundaryRadius - _sphereRadius);
			float editedFromEdge = EditorGUILayout.FloatField("경계에서 안쪽으로", fromEdge);
			_sphereRadius = Mathf.Clamp(boundaryRadius - editedFromEdge, 1f, boundaryRadius);
		}
		else
		{
			_sphereRadius = Mathf.Max(1f, EditorGUILayout.FloatField("맵 중심으로부터의 거리", _sphereRadius));
		}

		if (!hasBoundary)
		{
			HubStyles.ColoredLabel("이 씬엔 맵 경계(WorldBoundary)가 없어 경계 대비 위치를 표시할 수 없습니다.\n" +
								   "중심은 원점(0,0,0) 기준입니다.",
				HubStyles.Warn, EditorStyles.wordWrappedMiniLabel);
			return;
		}

		float ratio = _sphereRadius / boundaryRadius;
		if (ratio > 1f)
		{
			HubStyles.ColoredLabel($"맵 경계({boundaryRadius:0}) 밖입니다 — 플레이어가 닿을 수 없는 자리입니다.",
				HubStyles.Error, EditorStyles.miniLabel);
		}
		else
		{
			HubStyles.ColoredLabel($"맵 경계 {boundaryRadius:0} 기준 {ratio * 100f:0}% 지점 " +
								   $"(경계에서 안쪽으로 {boundaryRadius - _sphereRadius:0})",
				HubStyles.Muted, EditorStyles.miniLabel);
		}
	}

	private void HandleGlobeInput(Rect rect)
	{
		Event e = Event.current;
		int id = GUIUtility.GetControlID(FocusType.Passive);

		switch (e.GetTypeForControl(id))
		{
			case EventType.MouseMove:
				_globeHoverValid = TryGlobePointToDirection(rect, e.mousePosition, out _globeHoverDir);
				Repaint();
				break;

			// 구 위에서만 확대/축소로 씀. 바깥에서는 그냥 두어야 패널 스크롤이 정상 동작함
			case EventType.ScrollWheel:
				if (rect.Contains(e.mousePosition))
				{
					_globeViewSize = Mathf.Clamp(_globeViewSize - e.delta.y * 8f, GlobeSizeMin, GlobeSizeMax);
					e.Use();
					Repaint();
				}
				break;

			case EventType.MouseDown:
				if (rect.Contains(e.mousePosition) && (e.button == 0 || e.button == 1))
				{
					GUIUtility.hotControl = id;
					_globeDragged = false;
					_globeDragDistance = 0f;
					e.Use();
				}
				break;

			case EventType.MouseDrag:
				if (GUIUtility.hotControl == id)
				{
					// 마우스를 끄는 쪽으로 표면이 따라오게 — 좌우도 위아래와 같은 감각이어야 함
					_globeAngles.y -= e.delta.x * 0.5f;
					_globeAngles.x = Mathf.Clamp(_globeAngles.x - e.delta.y * 0.5f, -89f, 89f);
					_globeDragDistance += e.delta.magnitude;
					// 살짝 흔들린 것까지 드래그로 치면 클릭 배치가 안 먹음
					if (_globeDragDistance > 4f)
					{
						_globeDragged = true;
					}
					_globeHoverValid = TryGlobePointToDirection(rect, e.mousePosition, out _globeHoverDir);
					e.Use();
					Repaint();
				}
				break;

			case EventType.MouseUp:
				if (GUIUtility.hotControl == id)
				{
					GUIUtility.hotControl = 0;
					if (e.button == 0 && !_globeDragged)
					{
						PlaceFromGlobe(rect, e.mousePosition);
					}
					e.Use();
					Repaint();
				}
				break;
		}
	}

	private void PlaceFromGlobe(Rect rect, Vector2 mousePosition)
	{
		if (_paletteSelected == null)
		{
			Debug.LogWarning("[Hub] 놓을 것을 먼저 고를 것.");
			return;
		}
		if (!TryGlobePointToDirection(rect, mousePosition, out Vector3 dir))
		{
			return;
		}

		PlaceOnSphere(_sphereCenter + dir * _sphereRadius);
	}

	// 창 안의 점 → 안쪽 구 표면 방향.
	// 안쪽 구보다 바깥을 찍으면 그 방향의 가장자리로 쳐줌 — 작게 그려졌을 때 못 찍는 걸 막기 위함.
	private bool TryGlobePointToDirection(Rect rect, Vector2 position, out Vector3 dir)
	{
		dir = Vector3.forward;

		float outerRadius = rect.width * 0.5f;
		if (outerRadius <= 0f)
		{
			return false;
		}

		float nx = (position.x - rect.center.x) / outerRadius;
		// GUI는 아래로 갈수록 y가 커져서 뒤집어야 3D와 방향이 맞음
		float ny = -(position.y - rect.center.y) / outerRadius;
		if (nx * nx + ny * ny > 1f)
		{
			return false;
		}

		float inner = Mathf.Max(GetInnerRatio(), 0.001f);
		float sx = nx / inner;
		float sy = ny / inner;
		float flat = sx * sx + sy * sy;

		Vector3 view;
		if (flat <= 1f)
		{
			view = new Vector3(sx, sy, Mathf.Sqrt(1f - flat));
		}
		else
		{
			// 안쪽 구 실루엣 바깥 — 방향만 취해 가장자리에 붙임
			float length = Mathf.Sqrt(flat);
			view = new Vector3(sx / length, sy / length, 0f);
		}

		dir = (GlobeRotation * view).normalized;
		return true;
	}

	// 구 위 방향 → 창 안의 점. distanceRatio는 중심에서 얼마나 떨어졌는지(1 = 맵 경계)
	private Vector2 GlobeProject(Rect rect, Vector3 worldDir, float distanceRatio, out bool front)
	{
		Vector3 view = Quaternion.Inverse(GlobeRotation) * worldDir;
		front = view.z >= 0f;

		float outerRadius = rect.width * 0.5f * distanceRatio;
		return new Vector2(rect.center.x + view.x * outerRadius, rect.center.y - view.y * outerRadius);
	}

	private static void GetLatLon(Vector3 dir, out float latitude, out float longitude)
	{
		latitude = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
		longitude = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
	}

	// 맵 경계 = 바깥 테두리. 여기까지가 플레이어가 갈 수 있는 범위임.
	// 경계가 없는 씬에서는 테두리를 그리지 않음 — 없는 경계를 그리면 그림이 거짓말이 됨
	private void DrawBoundaryRing(Rect rect)
	{
		Color prev = Handles.color;
		Vector3 center = new Vector3(rect.center.x, rect.center.y, 0f);
		float outerRadius = rect.width * 0.5f;

		Handles.color = EditorGUIUtility.isProSkin
			? new Color(0.13f, 0.14f, 0.17f)
			: new Color(0.88f, 0.89f, 0.92f);
		Handles.DrawSolidDisc(center, Vector3.forward, outerRadius);

		if (TryGetBoundaryRadius(out float _))
		{
			Handles.color = new Color(HubStyles.Warn.r, HubStyles.Warn.g, HubStyles.Warn.b, 0.55f);
			Handles.DrawWireDisc(center, Vector3.forward, outerRadius);
		}

		Handles.color = prev;
	}

	// 실제로 물건이 놓이는 껍질. 경계와 구분되게 다른 색으로 칠함
	private void DrawInnerSphere(Rect rect)
	{
		Color prev = Handles.color;
		Vector3 center = new Vector3(rect.center.x, rect.center.y, 0f);
		float innerRadius = rect.width * 0.5f * GetInnerRatio();

		Handles.color = EditorGUIUtility.isProSkin
			? new Color(0.20f, 0.30f, 0.42f, 0.9f)
			: new Color(0.70f, 0.80f, 0.92f, 0.9f);
		Handles.DrawSolidDisc(center, Vector3.forward, innerRadius);

		Handles.color = new Color(0.45f, 0.68f, 0.95f, 0.9f);
		Handles.DrawWireDisc(center, Vector3.forward, innerRadius);

		Handles.color = prev;
	}

	// 위도선(가로) + 경도선(세로). 뒤쪽 반구는 흐리게 그려서 앞뒤가 구분되게 함
	private void DrawGlobeGrid(Rect rect)
	{
		Color prev = Handles.color;
		float ratio = GetInnerRatio();
		Color frontColor = new Color(1f, 1f, 1f, 0.35f);
		Color backColor = new Color(1f, 1f, 1f, 0.12f);

		const int steps = 24;

		// 위도선 — 적도(0°)는 진하게
		for (int lat = -60; lat <= 60; lat += 30)
		{
			float latRad = lat * Mathf.Deg2Rad;
			float y = Mathf.Sin(latRad);
			float ring = Mathf.Cos(latRad);
			float width = lat == 0 ? 2f : 1f;

			for (int i = 0; i < steps; i++)
			{
				float a0 = (360f / steps) * i * Mathf.Deg2Rad;
				float a1 = (360f / steps) * (i + 1) * Mathf.Deg2Rad;
				Vector3 p0 = new Vector3(Mathf.Sin(a0) * ring, y, Mathf.Cos(a0) * ring);
				Vector3 p1 = new Vector3(Mathf.Sin(a1) * ring, y, Mathf.Cos(a1) * ring);
				DrawGlobeArc(rect, p0, p1, ratio, frontColor, backColor, width);
			}
		}

		// 경도선
		for (int lon = 0; lon < 180; lon += 30)
		{
			float lonRad = lon * Mathf.Deg2Rad;
			for (int i = 0; i < steps; i++)
			{
				float a0 = (360f / steps) * i * Mathf.Deg2Rad;
				float a1 = (360f / steps) * (i + 1) * Mathf.Deg2Rad;
				Vector3 p0 = new Vector3(Mathf.Cos(a0) * Mathf.Sin(lonRad), Mathf.Sin(a0), Mathf.Cos(a0) * Mathf.Cos(lonRad));
				Vector3 p1 = new Vector3(Mathf.Cos(a1) * Mathf.Sin(lonRad), Mathf.Sin(a1), Mathf.Cos(a1) * Mathf.Cos(lonRad));
				DrawGlobeArc(rect, p0, p1, ratio, frontColor, backColor, 1f);
			}
		}

		Handles.color = prev;
	}

	private void DrawGlobeArc(Rect rect, Vector3 from, Vector3 to, float ratio,
		Color frontColor, Color backColor, float width)
	{
		Vector2 a = GlobeProject(rect, from, ratio, out bool frontA);
		Vector2 b = GlobeProject(rect, to, ratio, out bool frontB);

		Handles.color = (frontA && frontB) ? frontColor : backColor;
		_globeSegment[0] = new Vector3(a.x, a.y, 0f);
		_globeSegment[1] = new Vector3(b.x, b.y, 0f);
		Handles.DrawAAPolyLine(width, _globeSegment);
	}

	// 이미 씬에 놓인 것들. 프리팹 종류마다 색을 달리해서 뭐가 어디 있는지 구분되게 함.
	// 거리도 실제 거리 비율로 찍으므로 안쪽/바깥쪽에 놓인 것이 그대로 보임.
	// 컨테이너를 만들지 않고 찾기만 함 — 그리기만 하는데 오브젝트가 생기면 안 됨
	private void DrawPlacedMarkers(Rect rect)
	{
		if (string.IsNullOrWhiteSpace(_containerName))
		{
			return;
		}

		GameObject container = GameObject.Find(_containerName);
		if (container == null)
		{
			return;
		}

		Color prev = Handles.color;
		Transform root = container.transform;
		// 경계가 없으면 배치 구 자체가 화면 가득이므로 그걸 기준으로 견줌
		if (!TryGetBoundaryRadius(out float referenceRadius))
		{
			referenceRadius = Mathf.Max(_sphereRadius, 1f);
		}

		for (int i = 0; i < root.childCount; i++)
		{
			Transform child = root.GetChild(i);
			Vector3 offset = child.position - _sphereCenter;
			if (offset.sqrMagnitude < 0.001f)
			{
				continue;
			}

			float ratio = Mathf.Clamp01(offset.magnitude / referenceRadius);
			Vector2 point = GlobeProject(rect, offset.normalized, ratio, out bool front);

			GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject);
			Color color = PaletteColor(source != null ? source.name : child.name);

			Handles.color = front ? color : new Color(color.r, color.g, color.b, 0.28f);
			Handles.DrawSolidDisc(new Vector3(point.x, point.y, 0f), Vector3.forward, front ? 4f : 2.5f);
		}

		Handles.color = prev;
	}

	private void DrawGlobeHover(Rect rect)
	{
		if (!_globeHoverValid)
		{
			return;
		}

		Vector2 point = GlobeProject(rect, _globeHoverDir, GetInnerRatio(), out bool front);
		if (!front)
		{
			return;
		}

		Color prev = Handles.color;
		Handles.color = _paletteSelected != null ? PaletteColor(_paletteSelected.name) : HubStyles.Warn;
		Handles.DrawWireDisc(new Vector3(point.x, point.y, 0f), Vector3.forward, 7f);
		Handles.color = prev;
	}

	// 프리팹 이름에서 색을 뽑음. 표에 색을 따로 등록하지 않아도 종류마다 항상 같은 색이 나옴
	private static Color PaletteColor(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return HubStyles.Muted;
		}

		// GetHashCode는 실행마다 값이 달라질 수 있어 직접 굴림 — 색이 세션마다 바뀌면 못 외움
		int hash = 17;
		for (int i = 0; i < name.Length; i++)
		{
			hash = hash * 31 + name[i];
		}
		float hue = Mathf.Abs(hash % 997) / 997f;
		return Color.HSVToRGB(hue, 0.62f, 1f);
	}
}
