using System.Collections.Generic;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

// =====================================================================
// ProjectHubWindow — 프리팹 검사 탭.
//
// 지금까지 나온 버그 대부분이 "코드는 맞는데 프리팹 배선이 빠졌다"였음.
// 눈으로 하나씩 확인하던 항목을 규칙으로 박아두고 한 번에 훑음.
//
// 검사 항목은 CheckPrefab()에 모여 있음. 새 규칙이 생기면 거기에 한 줄씩 추가할 것.
// 검사는 읽기만 함 — 프리팹을 고치지 않음(잘못된 자동수정이 더 위험하므로).
// =====================================================================
public partial class ProjectHubWindow
{
	private static readonly string[] PrefabSearchRoots =
	{
		"Assets/3.Prefabs",
	};

	private struct PrefabIssue
	{
		public CheckLevel level;
		public string message;
	}

	private class PrefabReport
	{
		public GameObject prefab;
		public string path;
		public readonly List<PrefabIssue> issues = new List<PrefabIssue>();

		public int FailCount;
		public int WarnCount;
	}

	private string _prefabFilter = "";
	private bool _prefabOnlyProblems = true;
	private Vector2 _prefabScroll;
	private readonly List<PrefabReport> _prefabReports = new List<PrefabReport>();
	private readonly HashSet<string> _prefabExpanded = new HashSet<string>();

	private int _hitBoxLayer = -1;
	private int _lockOnBoxLayer = -1;

	private void PrefabTabOnEnable()
	{
		_hitBoxLayer = LayerMask.NameToLayer("HitBox");
		_lockOnBoxLayer = LayerMask.NameToLayer("LockOnBox");
	}

	private void DrawPrefabTab()
	{
		EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
		if (GUILayout.Button("전체 검사", EditorStyles.toolbarButton, GUILayout.Width(72f)))
		{
			RunPrefabScan();
		}
		_prefabOnlyProblems = GUILayout.Toggle(_prefabOnlyProblems, "문제만 보기", EditorStyles.toolbarButton, GUILayout.Width(90f));
		_prefabFilter = EditorGUILayout.TextField(_prefabFilter, EditorStyles.toolbarSearchField);
		EditorGUILayout.EndHorizontal();

		if (_prefabReports.Count == 0)
		{
			EditorGUILayout.HelpBox("[전체 검사]를 누르면 " + string.Join(", ", PrefabSearchRoots) + " 아래 프리팹을 훑음.\n" +
									"검사는 읽기 전용임 — 프리팹을 고치지 않음.", MessageType.Info);
			return;
		}

		int fail = 0;
		int warn = 0;
		for (int i = 0; i < _prefabReports.Count; i++)
		{
			fail += _prefabReports[i].FailCount;
			warn += _prefabReports[i].WarnCount;
		}
		EditorGUILayout.LabelField($"프리팹 {_prefabReports.Count}개 검사 — 실패 {fail}건 / 주의 {warn}건", EditorStyles.boldLabel);

		_prefabScroll = EditorGUILayout.BeginScrollView(_prefabScroll);
		for (int i = 0; i < _prefabReports.Count; i++)
		{
			DrawPrefabReport(_prefabReports[i]);
		}
		EditorGUILayout.EndScrollView();
	}

	private void DrawPrefabReport(PrefabReport report)
	{
		if (report.prefab == null)
		{
			return;
		}
		if (!MatchesFilter(report.prefab.name, _prefabFilter))
		{
			return;
		}
		bool hasProblem = report.FailCount > 0 || report.WarnCount > 0;
		if (_prefabOnlyProblems && !hasProblem)
		{
			return;
		}

		EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		EditorGUILayout.BeginHorizontal();

		bool expanded = _prefabExpanded.Contains(report.path);
		string summary = hasProblem ? $"실패 {report.FailCount} / 주의 {report.WarnCount}" : "이상 없음";
		bool newExpanded = EditorGUILayout.Foldout(expanded, $"{report.prefab.name}   ({summary})", true);
		if (newExpanded != expanded)
		{
			if (newExpanded)
			{
				_prefabExpanded.Add(report.path);
			}
			else
			{
				_prefabExpanded.Remove(report.path);
			}
		}

		if (GUILayout.Button("선택", GUILayout.Width(48f)))
		{
			Selection.activeObject = report.prefab;
			EditorGUIUtility.PingObject(report.prefab);
		}
		EditorGUILayout.EndHorizontal();

		if (newExpanded)
		{
			EditorGUILayout.LabelField(report.path, EditorStyles.miniLabel);
			for (int i = 0; i < report.issues.Count; i++)
			{
				PrefabIssue issue = report.issues[i];
				if (_prefabOnlyProblems && issue.level == CheckLevel.Pass)
				{
					continue;
				}
				ResultLine(issue.level, issue.message);
			}
		}

		EditorGUILayout.EndVertical();
	}

	private void RunPrefabScan()
	{
		_prefabReports.Clear();
		_hitBoxLayer = LayerMask.NameToLayer("HitBox");
		_lockOnBoxLayer = LayerMask.NameToLayer("LockOnBox");

		List<string> roots = new List<string>();
		for (int i = 0; i < PrefabSearchRoots.Length; i++)
		{
			if (AssetDatabase.IsValidFolder(PrefabSearchRoots[i]))
			{
				roots.Add(PrefabSearchRoots[i]);
			}
		}
		if (roots.Count == 0)
		{
			return;
		}

		string[] guids = AssetDatabase.FindAssets("t:GameObject", roots.ToArray());
		try
		{
			for (int i = 0; i < guids.Length; i++)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[i]);
				if (!path.EndsWith(".prefab"))
				{
					continue;
				}
				if (EditorUtility.DisplayCancelableProgressBar("프리팹 검사", path, (float)i / guids.Length))
				{
					break;
				}

				GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				if (prefab == null)
				{
					continue;
				}

				PrefabReport report = CheckPrefab(prefab, path);
				// 검사 규칙에 걸리는 요소가 하나도 없는 프리팹(장식용 메시 등)은 목록에서 뺌
				if (report.issues.Count > 0)
				{
					_prefabReports.Add(report);
				}
			}
		}
		finally
		{
			EditorUtility.ClearProgressBar();
		}

		_prefabReports.Sort(ComparePrefabReport);
	}

	private static int ComparePrefabReport(PrefabReport a, PrefabReport b)
	{
		// 실패 많은 것 → 주의 많은 것 → 이름순
		if (a.FailCount != b.FailCount)
		{
			return b.FailCount.CompareTo(a.FailCount);
		}
		if (a.WarnCount != b.WarnCount)
		{
			return b.WarnCount.CompareTo(a.WarnCount);
		}
		return string.Compare(a.path, b.path, System.StringComparison.OrdinalIgnoreCase);
	}

	// =================================================================
	// 검사 규칙 — 새 규칙은 여기에 추가
	// =================================================================
	private PrefabReport CheckPrefab(GameObject prefab, string path)
	{
		PrefabReport report = new PrefabReport();
		report.prefab = prefab;
		report.path = path;

		CheckEnvironmentHit(prefab, report);
		CheckLockOnBoxLayer(prefab, report);
		CheckUnitWiring(prefab, report);
		CheckPhotonView(prefab, report);

		for (int i = 0; i < report.issues.Count; i++)
		{
			if (report.issues[i].level == CheckLevel.Fail)
			{
				report.FailCount++;
			}
			else if (report.issues[i].level == CheckLevel.Warn)
			{
				report.WarnCount++;
			}
		}
		return report;
	}

	// 환경 오브젝트: 투사체는 HitBox 레이어만 감지하므로, 그 레이어 콜라이더가 없으면
	// 총알이 통과하고 락온 차폐도 안 걸림.
	private void CheckEnvironmentHit(GameObject prefab, PrefabReport report)
	{
		MapEnvironmentHit[] hits = prefab.GetComponentsInChildren<MapEnvironmentHit>(true);
		if (hits.Length == 0)
		{
			return;
		}

		bool hasHitBoxCollider = HasColliderOnLayer(prefab, _hitBoxLayer);
		if (hasHitBoxCollider)
		{
			Add(report, CheckLevel.Pass, "MapEnvironmentHit + HitBox 레이어 콜라이더 있음 (피격/차폐 정상)");
		}
		else
		{
			Add(report, CheckLevel.Fail, "MapEnvironmentHit이 있는데 HitBox 레이어 콜라이더가 없음 " +
										 "→ 총알이 통과하고 락온 차폐도 안 걸림");
		}
	}

	// LockOnBox는 락온 탐색 마스크(LockOnBox 레이어)에 걸려야 후보가 됨. 레이어가 다르면 조용히 안 잡힘.
	private void CheckLockOnBoxLayer(GameObject prefab, PrefabReport report)
	{
		LockOnBox[] boxes = prefab.GetComponentsInChildren<LockOnBox>(true);
		if (boxes.Length == 0)
		{
			return;
		}

		int wrong = 0;
		for (int i = 0; i < boxes.Length; i++)
		{
			if (boxes[i].gameObject.layer != _lockOnBoxLayer)
			{
				wrong++;
			}
		}

		if (wrong == 0)
		{
			Add(report, CheckLevel.Pass, $"LockOnBox {boxes.Length}개 전부 LockOnBox 레이어");
		}
		else
		{
			Add(report, CheckLevel.Fail, $"LockOnBox {wrong}개가 LockOnBox 레이어가 아님 → 락온 후보로 안 잡힘");
		}
	}

	// 유닛 배선: 사망 VFX 미지정 / 본체 히트박스 누락은 둘 다 '조용히 아무 일도 안 일어나는' 부류라 눈에 안 띔.
	private void CheckUnitWiring(GameObject prefab, PrefabReport report)
	{
		Unit[] units = prefab.GetComponentsInChildren<Unit>(true);
		if (units.Length == 0)
		{
			return;
		}

		for (int i = 0; i < units.Length; i++)
		{
			Unit unit = units[i];
			string who = units.Length > 1 ? $"[{unit.gameObject.name}] " : "";

			SerializedObject so = new SerializedObject(unit);

			SerializedProperty deathVfx = so.FindProperty("_deathVfxType");
			if (deathVfx != null)
			{
				if (deathVfx.intValue == (int)EFFECT_TYPE.VFX_NONE)
				{
					Add(report, CheckLevel.Warn, who + "_deathVfxType이 VFX_NONE → 죽을 때 폭발 VFX가 안 나옴");
				}
				else
				{
					Add(report, CheckLevel.Pass, who + "_deathVfxType 지정됨");
				}
			}

			// 총알이 실제로 맞는 조건은 '계층에 HitBox 레이어 콜라이더가 있는지'뿐임 —
			// Projectile은 레이어만 보고 GetComponentInParent<IHittable>로 유닛을 찾으므로
			// bodyHitboxRoot 배선과는 무관함.
			if (!HasColliderOnLayer(unit.gameObject, _hitBoxLayer))
			{
				Add(report, CheckLevel.Fail, who + "계층에 HitBox 레이어 콜라이더가 없음 → 총알이 통과함");
			}
			else
			{
				Add(report, CheckLevel.Pass, who + "HitBox 레이어 콜라이더 있음");
			}

			// bodyHitboxRoot는 '실드/무적일 때 본체 히트박스를 끄기 위한' 토글 대상 참조임(필수 아님).
			// 비어 있으면 토글만 안 되고 콜라이더는 항상 켜진 상태로 남음.
			// 무적은 ApplyHitDamage에서 코드로도 막으므로 피격 판정 자체엔 영향 없음.
			if (unit.bodyHitboxRoot == null)
			{
				if (unit.shield != null)
				{
					Add(report, CheckLevel.Warn, who + "실드는 연결됐는데 bodyHitboxRoot가 비어 있음 " +
												"→ 실드가 켜져도 본체 히트박스가 안 꺼짐");
				}
				// 실드도 없으면 토글할 대상이 없는 정상 구성 — 아무것도 보고하지 않음
			}
			else if (!HasColliderOnLayer(unit.bodyHitboxRoot.gameObject, _hitBoxLayer))
			{
				Add(report, CheckLevel.Warn, who + "bodyHitboxRoot 하위에 HitBox 레이어 콜라이더가 없음 → 토글 대상이 없음");
			}
		}
	}

	// PhotonView의 ObservedComponents는 '스트림 동기화(OnPhotonSerializeView)' 대상 목록임.
	// RPC는 이 목록과 무관하게 ViewID만으로 동작하므로, RPC 전용 오브젝트(SpawnManager 등)는
	// 목록이 비어 있는 게 정상임. 그래서 '비었음' 자체가 아니라
	// '동기화 대상 컴포넌트가 붙어 있는데 등록만 안 됐는지'를 봄.
	private void CheckPhotonView(GameObject prefab, PrefabReport report)
	{
		PhotonView[] views = prefab.GetComponentsInChildren<PhotonView>(true);
		if (views.Length == 0)
		{
			return;
		}

		for (int i = 0; i < views.Length; i++)
		{
			PhotonView view = views[i];
			string who = views.Length > 1 ? $"[{view.gameObject.name}] " : "";

			// 같은 오브젝트에 붙은 스트림 동기화 후보(IPunObservable 구현체).
			// PhotonTransformView/RigidbodyView/AnimatorView와 Unit이 모두 여기 해당됨.
			List<Component> candidates = new List<Component>();
			Component[] components = view.GetComponents<Component>();
			for (int j = 0; j < components.Length; j++)
			{
				if (components[j] == null)
				{
					continue;
				}
				if (components[j] is IPunObservable)
				{
					candidates.Add(components[j]);
				}
			}

			bool hasNull = false;
			bool hasTransformView = false;
			int observed = 0;
			if (view.ObservedComponents != null)
			{
				observed = view.ObservedComponents.Count;
				for (int j = 0; j < view.ObservedComponents.Count; j++)
				{
					Component c = view.ObservedComponents[j];
					if (c == null)
					{
						hasNull = true;
						continue;
					}
					if (c is PhotonTransformView || c is PhotonTransformViewClassic || c is PhotonRigidbodyView)
					{
						hasTransformView = true;
					}
				}
			}

			if (hasNull)
			{
				Add(report, CheckLevel.Fail, who + "Observed Components에 비어 있는 칸이 있음 → 참조가 끊긴 상태");
			}

			// 동기화 후보가 없으면 RPC 전용 뷰 — 정상이므로 아무것도 보고하지 않음
			if (candidates.Count == 0)
			{
				continue;
			}

			// 후보가 있는데 목록에 안 들어간 것들 = 배선 누락
			List<string> missing = new List<string>();
			for (int j = 0; j < candidates.Count; j++)
			{
				if (view.ObservedComponents == null || !view.ObservedComponents.Contains(candidates[j]))
				{
					missing.Add(candidates[j].GetType().Name);
				}
			}

			if (missing.Count > 0)
			{
				Add(report, CheckLevel.Fail, who + $"동기화 대상이 Observed에 없음: {string.Join(", ", missing)} " +
											"→ 해당 컴포넌트가 아무것도 보내지 않음");
			}

			// 유닛인데 위치 동기화가 없으면 남 화면에서 제자리에 멈춰 있음
			bool isUnit = view.GetComponent<Unit>() != null;
			if (isUnit && !hasTransformView)
			{
				Add(report, CheckLevel.Warn, who + "위치 동기화 뷰(PhotonTransformView 등)가 Observed에 없음 " +
											"→ 남 화면에서 이 유닛이 움직이지 않음");
			}
			else if (!hasNull && missing.Count == 0)
			{
				Add(report, CheckLevel.Pass, who + $"PhotonView Observed {observed}개 등록됨");
			}
		}
	}

	// =================================================================
	// 헬퍼
	// =================================================================

	// 지정 레이어의 콜라이더가 하위(자기 포함)에 하나라도 있는지. 비활성 오브젝트도 포함해서 봄
	private static bool HasColliderOnLayer(GameObject root, int layer)
	{
		if (root == null || layer < 0)
		{
			return false;
		}
		Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
		for (int i = 0; i < colliders.Length; i++)
		{
			if (colliders[i].gameObject.layer == layer)
			{
				return true;
			}
		}
		return false;
	}

	private static void Add(PrefabReport report, CheckLevel level, string message)
	{
		PrefabIssue issue = new PrefabIssue();
		issue.level = level;
		issue.message = message;
		report.issues.Add(issue);
	}
}
