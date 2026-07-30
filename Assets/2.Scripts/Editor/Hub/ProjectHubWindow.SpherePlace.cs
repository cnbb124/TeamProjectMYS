using UnityEditor;
using UnityEngine;

// =====================================================================
// ProjectHubWindow — 구면 배치 (맵 배치 탭).
//
// 우주 맵은 바닥이 없어서 씬 뷰 드래그로 놓으면 깊이가 제멋대로가 됨.
// 원점 기준 구(반경 = 배치 거리)를 기준면으로 두고 그 표면에 스냅해서 놓음.
// 배치 거리가 일정하므로 높이를 따로 만질 필요가 없음.
//
// 씬 뷰에 와이어 구와 프리뷰를 그리고, 클릭 지점과 구의 교점에 배치함.
// 시점 버튼(위/정면/측면)은 SceneView 카메라를 그 각도로 스냅함.
// =====================================================================
public partial class ProjectHubWindow
{
	private bool _sphereMode;
	private float _sphereRadius = 1200f;
	private float _sphereRadiusMin = 1000f;
	private float _sphereRadiusMax = 1500f;
	private bool _sphereRandomRadius;
	// 경도/위도를 이 각도 단위로 맞춤. 0이면 자유 배치
	private float _sphereSnapAngle;
	private bool _sphereContinuous = true;
	private Vector3 _sphereCenter = Vector3.zero;

	// 마우스가 가리키는 구 표면 지점. 유효할 때만 프리뷰를 그림
	private Vector3 _spherePreviewPoint;
	private bool _spherePreviewValid;

	private void SpherePlaceOnEnable()
	{
		SceneView.duringSceneGui -= OnSphereSceneGui;
		SceneView.duringSceneGui += OnSphereSceneGui;
		PullBoundaryRadius();
	}

	private void SpherePlaceOnDisable()
	{
		SceneView.duringSceneGui -= OnSphereSceneGui;
		_sphereMode = false;
	}

	// WorldBoundary가 있으면 그 경계 안쪽을 기본 배치 범위로 제안함
	private void PullBoundaryRadius()
	{
		WorldBoundary boundary = Object.FindObjectOfType<WorldBoundary>(true);
		if (boundary == null)
		{
			return;
		}
		SerializedObject so = new SerializedObject(boundary);
		SerializedProperty radiusProp = so.FindProperty("boundaryRadius");
		if (radiusProp == null)
		{
			return;
		}
		float boundaryRadius = radiusProp.floatValue;
		_sphereCenter = boundary.transform.position;
		_sphereRadiusMax = boundaryRadius * 0.9f;
		_sphereRadiusMin = boundaryRadius * 0.5f;
		_sphereRadius = Mathf.Clamp(_sphereRadius, _sphereRadiusMin, _sphereRadiusMax);
	}

	private void DrawSpherePlaceSection()
	{
		SectionHeader("구면 배치");

		EditorGUILayout.BeginHorizontal();
		bool newMode = GUILayout.Toggle(_sphereMode, _sphereMode ? "배치 모드 켜짐 (Esc로 해제)" : "배치 모드 켜기",
			EditorStyles.miniButton, GUILayout.Height(22f));
		if (newMode != _sphereMode)
		{
			_sphereMode = newMode;
			_spherePreviewValid = false;
			SceneView.RepaintAll();
		}
		if (GUILayout.Button("경계에서 범위 가져오기", GUILayout.Width(160f), GUILayout.Height(22f)))
		{
			PullBoundaryRadius();
		}
		EditorGUILayout.EndHorizontal();

		if (_sphereMode && _paletteSelected == null)
		{
			EditorGUILayout.HelpBox("팔레트에서 프리팹을 먼저 고를 것.", MessageType.Warning);
		}

		_sphereCenter = EditorGUILayout.Vector3Field("구 중심", _sphereCenter);

		EditorGUILayout.BeginHorizontal();
		_sphereRadius = EditorGUILayout.Slider("배치 거리", _sphereRadius, _sphereRadiusMin, _sphereRadiusMax);
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.BeginHorizontal();
		_sphereRadiusMin = EditorGUILayout.FloatField("범위 최소", _sphereRadiusMin);
		_sphereRadiusMax = EditorGUILayout.FloatField("최대", _sphereRadiusMax);
		EditorGUILayout.EndHorizontal();

		_sphereRandomRadius = EditorGUILayout.Toggle("거리를 범위 내 랜덤", _sphereRandomRadius);
		_sphereSnapAngle = EditorGUILayout.Slider("각도 스냅(0=자유)", _sphereSnapAngle, 0f, 45f);
		_sphereContinuous = EditorGUILayout.Toggle("연속 배치(클릭마다)", _sphereContinuous);

		// 시점 스냅 — 같은 높이 버튼이라 한 줄에 둠
		SectionHeader("시점");
		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("위에서", GUILayout.Height(22f)))
		{
			SnapSceneView(Quaternion.Euler(90f, 0f, 0f), true);
		}
		if (GUILayout.Button("정면", GUILayout.Height(22f)))
		{
			SnapSceneView(Quaternion.Euler(0f, 0f, 0f), true);
		}
		if (GUILayout.Button("측면", GUILayout.Height(22f)))
		{
			SnapSceneView(Quaternion.Euler(0f, 90f, 0f), true);
		}
		if (GUILayout.Button("자유", GUILayout.Height(22f)))
		{
			SnapSceneView(Quaternion.Euler(35f, 45f, 0f), false);
		}
		if (GUILayout.Button("전체 보기", GUILayout.Width(80f), GUILayout.Height(22f)))
		{
			FitSceneViewToSphere();
		}
		EditorGUILayout.EndHorizontal();

		if (_sphereMode)
		{
			EditorGUILayout.HelpBox("씬 뷰에서 구 표면을 클릭하면 그 자리에 배치됨.\n" +
									"씬 뷰를 돌리면 구도 같이 돌아 보임 — 지구본처럼 원하는 면을 보고 놓을 것.",
									MessageType.Info);
		}
	}

	// 씬 뷰 카메라를 지정 각도로 스냅. ortho면 직교 투영으로 바꿔 위/정면 뷰가 왜곡 없이 보임
	private void SnapSceneView(Quaternion rotation, bool ortho)
	{
		SceneView view = SceneView.lastActiveSceneView;
		if (view == null)
		{
			return;
		}
		view.orthographic = ortho;
		view.LookAt(_sphereCenter, rotation, _sphereRadius * 1.6f);
		view.Repaint();
	}

	private void FitSceneViewToSphere()
	{
		SceneView view = SceneView.lastActiveSceneView;
		if (view == null)
		{
			return;
		}
		view.LookAt(_sphereCenter, view.rotation, _sphereRadius * 2.2f);
		view.Repaint();
	}

	// 씬 뷰에 구/프리뷰를 그리고 클릭을 받음. 배치 모드가 꺼져 있으면 아무것도 하지 않음.
	private void OnSphereSceneGui(SceneView view)
	{
		if (!_sphereMode)
		{
			return;
		}

		DrawSphereGizmo();

		Event e = Event.current;

		if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
		{
			_sphereMode = false;
			_spherePreviewValid = false;
			Repaint();
			view.Repaint();
			e.Use();
			return;
		}

		// 마우스 위치 → 구 표면 교점
		if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.Repaint)
		{
			Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
			_spherePreviewValid = TryGetSpherePoint(ray, out _spherePreviewPoint);
			if (e.type != EventType.Repaint)
			{
				view.Repaint();
			}
		}

		if (_spherePreviewValid)
		{
			DrawPreview();
		}

		// 좌클릭 배치 — Alt(카메라 조작)와 겹치지 않게 제외
		if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && _spherePreviewValid)
		{
			if (_paletteSelected == null)
			{
				Debug.LogWarning("[Hub] 팔레트에서 프리팹을 먼저 고를 것.");
				return;
			}
			PlaceOnSphere(_spherePreviewPoint);
			e.Use();
			if (!_sphereContinuous)
			{
				_sphereMode = false;
				Repaint();
			}
		}
	}

	// 카메라에서 쏜 Ray와 구의 교점. 구 안에서 쏘면 바깥쪽 교점, 밖에서 쏘면 가까운 쪽 교점을 씀.
	private bool TryGetSpherePoint(Ray ray, out Vector3 point)
	{
		point = Vector3.zero;

		Vector3 toCenter = ray.origin - _sphereCenter;
		float b = Vector3.Dot(ray.direction, toCenter);
		float c = Vector3.Dot(toCenter, toCenter) - _sphereRadius * _sphereRadius;
		float discriminant = b * b - c;
		if (discriminant < 0f)
		{
			return false;
		}

		float sqrt = Mathf.Sqrt(discriminant);
		float near = -b - sqrt;
		float far = -b + sqrt;
		float t = near >= 0f ? near : far;
		if (t < 0f)
		{
			return false;
		}

		point = ray.origin + ray.direction * t;

		if (_sphereSnapAngle > 0.01f)
		{
			point = SnapToAngleGrid(point);
		}
		return true;
	}

	// 경도/위도를 스냅 각도 단위로 맞춤. 규칙적인 배치가 필요할 때 씀.
	private Vector3 SnapToAngleGrid(Vector3 worldPoint)
	{
		Vector3 dir = (worldPoint - _sphereCenter).normalized;

		float lat = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
		float lon = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;

		lat = Mathf.Round(lat / _sphereSnapAngle) * _sphereSnapAngle;
		lon = Mathf.Round(lon / _sphereSnapAngle) * _sphereSnapAngle;
		lat = Mathf.Clamp(lat, -89.9f, 89.9f);

		float latRad = lat * Mathf.Deg2Rad;
		float lonRad = lon * Mathf.Deg2Rad;
		float cosLat = Mathf.Cos(latRad);
		Vector3 snapped = new Vector3(cosLat * Mathf.Cos(lonRad), Mathf.Sin(latRad), cosLat * Mathf.Sin(lonRad));
		return _sphereCenter + snapped * _sphereRadius;
	}

	private void DrawSphereGizmo()
	{
		Color prev = Handles.color;

		// 적도 + 자오선 두 개로 구를 표현 — 솔리드 구는 시야를 가려서 와이어만 그림
		Handles.color = new Color(0.4f, 0.8f, 1f, 0.35f);
		Handles.DrawWireDisc(_sphereCenter, Vector3.up, _sphereRadius);
		Handles.color = new Color(0.4f, 0.8f, 1f, 0.18f);
		Handles.DrawWireDisc(_sphereCenter, Vector3.right, _sphereRadius);
		Handles.DrawWireDisc(_sphereCenter, Vector3.forward, _sphereRadius);

		Handles.color = prev;
	}

	private void DrawPreview()
	{
		Color prev = Handles.color;

		Handles.color = new Color(1f, 0.9f, 0.3f, 0.9f);
		float handleSize = HandleUtility.GetHandleSize(_spherePreviewPoint) * 0.5f;
		Handles.SphereHandleCap(0, _spherePreviewPoint, Quaternion.identity, handleSize, EventType.Repaint);

		// 중심에서 배치 지점까지 선을 그어 어느 방향/거리인지 보이게 함
		Handles.color = new Color(1f, 0.9f, 0.3f, 0.35f);
		Handles.DrawLine(_sphereCenter, _spherePreviewPoint);

		Vector3 dir = (_spherePreviewPoint - _sphereCenter).normalized;
		float lat = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
		float lon = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
		string label = _paletteSelected != null ? _paletteSelected.name : "(프리팹 미선택)";
		Handles.Label(_spherePreviewPoint,
			$" {label}\n 거리 {_sphereRadius:F0} / 위도 {lat:F0}° / 경도 {lon:F0}°");

		Handles.color = prev;
	}

	// 구 표면 지점에 프리팹을 배치. 회전은 구 바깥을 등지도록(중심을 바라보게) 맞춤.
	private void PlaceOnSphere(Vector3 point)
	{
		Transform parent = ResolveContainer();

		float radius = _sphereRadius;
		if (_sphereRandomRadius)
		{
			radius = Random.Range(_sphereRadiusMin, _sphereRadiusMax);
			point = _sphereCenter + (point - _sphereCenter).normalized * radius;
		}

		GameObject instance = PrefabUtility.InstantiatePrefab(_paletteSelected) as GameObject;
		if (instance == null)
		{
			Debug.LogWarning($"[Hub] 프리팹 인스턴스 생성 실패: {_paletteSelected.name}");
			return;
		}

		Undo.RegisterCreatedObjectUndo(instance, "Hub 구면 배치");
		if (parent != null)
		{
			instance.transform.SetParent(parent, true);
		}
		instance.transform.position = point;

		// 중심을 바라보게 세움 — 구조물이 안쪽을 향해 자연스럽게 놓임
		Vector3 toCenter = _sphereCenter - point;
		if (toCenter.sqrMagnitude > 0.001f)
		{
			instance.transform.rotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up);
		}

		if (_randomFullRotation)
		{
			instance.transform.rotation = Random.rotation;
		}
		else if (_randomYaw)
		{
			instance.transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.Self);
		}

		if (!Mathf.Approximately(_randomScaleRange.x, 1f) || !Mathf.Approximately(_randomScaleRange.y, 1f))
		{
			float scale = Random.Range(_randomScaleRange.x, _randomScaleRange.y);
			instance.transform.localScale = Vector3.one * scale;
		}

		Selection.activeGameObject = instance;
	}
}
