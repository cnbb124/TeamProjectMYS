using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================
// ProjectHubWindow — 씬 흐름도.
//
// 게임 진행 순서를 줄별로 보여줌. SCENE_TYPE 번호 순서는 흐름과 다르므로
// (LOGIN이 5번인데 맨 처음) 흐름은 여기에 따로 적어둠.
// 씬 파일이 없는 항목은 아예 그리지 않음 — enum에만 남아 있는 이름을 흐름에 띄우면 헷갈림.
// =====================================================================
public partial class ProjectHubWindow
{
	private enum FlowKind { Scene, Branch, Group }

	private class FlowNode
	{
		public FlowKind kind;
		public SCENE_TYPE scene;        // Scene
		public string label;            // Branch / Group 제목
		public SCENE_TYPE[] scenes;     // Group

		public static FlowNode Of(SCENE_TYPE scene) =>
			new FlowNode { kind = FlowKind.Scene, scene = scene };

		public static FlowNode Branch(string label) =>
			new FlowNode { kind = FlowKind.Branch, label = label };

		public static FlowNode Group(string label, params SCENE_TYPE[] scenes) =>
			new FlowNode { kind = FlowKind.Group, label = label, scenes = scenes };
	}

	private class FlowRow
	{
		public string title;
		public FlowNode[] nodes;
	}

	private static readonly FlowRow[] SceneFlowRows =
	{
		new FlowRow
		{
			title = "공통",
			nodes = new[]
			{
				FlowNode.Of(SCENE_TYPE.LOGIN),
				FlowNode.Of(SCENE_TYPE.MAIN),
				FlowNode.Of(SCENE_TYPE.BASE_STATION),
				FlowNode.Branch("싱글 / 멀티"),
				FlowNode.Of(SCENE_TYPE.BASE_HANGAR),
			},
		},
		new FlowRow
		{
			title = "싱글",
			nodes = new[]
			{
				FlowNode.Of(SCENE_TYPE.MAP_SELECT),
				FlowNode.Group("스테이지",
					SCENE_TYPE.STAGE1, SCENE_TYPE.STAGE2, SCENE_TYPE.STAGE3, SCENE_TYPE.STAGE4),
				FlowNode.Of(SCENE_TYPE.RESULT),
			},
		},
		new FlowRow
		{
			title = "멀티",
			nodes = new[]
			{
				FlowNode.Of(SCENE_TYPE.MULTIPLAYER),
				FlowNode.Of(SCENE_TYPE.MAP_SELECT),
				FlowNode.Group("스테이지",
					SCENE_TYPE.STAGE1, SCENE_TYPE.STAGE2, SCENE_TYPE.STAGE3, SCENE_TYPE.STAGE4),
				FlowNode.Of(SCENE_TYPE.RESULT),
			},
		},
	};

	private const float FlowCardWidth = 96f;
	private const float FlowCardHeight = 40f;
	private const float FlowRowTitleWidth = 44f;

	private static readonly Dictionary<SCENE_TYPE, string> SceneDisplayNames = new Dictionary<SCENE_TYPE, string>
	{
		{ SCENE_TYPE.LOGIN, "로그인" },
		{ SCENE_TYPE.MAIN, "메인" },
		{ SCENE_TYPE.BASE_STATION, "정거장" },
		{ SCENE_TYPE.BASE_HANGAR, "격납고" },
		{ SCENE_TYPE.MAP_SELECT, "맵 선택" },
		{ SCENE_TYPE.MULTIPLAYER, "대기실" },
		{ SCENE_TYPE.RESULT, "결과" },
		{ SCENE_TYPE.LOADING_SEQUENCE, "로딩" },
	};

	private SCENE_TYPE _flowSelected = SCENE_TYPE.UNKNOWN;
	// 펼쳐둔 그룹 제목. 줄이 달라도 같은 제목이면 같이 펼쳐짐.
	private readonly HashSet<string> _flowExpandedGroups = new HashSet<string>();

	private void DrawSceneFlow()
	{
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("게임 흐름", HubStyles.SectionTitle, GUILayout.Width(70f));
		GUILayout.FlexibleSpace();
		DrawFlowLegend();
		EditorGUILayout.EndHorizontal();

		for (int r = 0; r < SceneFlowRows.Length; r++)
		{
			DrawFlowRow(SceneFlowRows[r]);
		}

		HubStyles.Separator();
	}

	private void DrawFlowLegend()
	{
		EditorGUILayout.BeginHorizontal();
		DrawLegendItem(HubStyles.Ok, "씬 있음 + 빌드 등록됨");
		DrawLegendItem(HubStyles.Warn, "빌드에 없거나 꺼짐");
		EditorGUILayout.EndHorizontal();
	}

	private static void DrawLegendItem(Color color, string text)
	{
		Rect dot = GUILayoutUtility.GetRect(8f, 8f, GUILayout.Width(8f), GUILayout.Height(8f));
		dot.y += 5f;
		HubStyles.StatusDot(dot, color);
		EditorGUILayout.LabelField(text, EditorStyles.miniLabel, GUILayout.Width(120f));
	}

	private void DrawFlowRow(FlowRow row)
	{
		// 이 줄에서 실제로 그릴 게 하나도 없으면 줄 자체를 접음.
		if (!RowHasAnything(row))
		{
			return;
		}

		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField(row.title, EditorStyles.miniBoldLabel,
			GUILayout.Width(FlowRowTitleWidth));

		bool firstDrawn = true;
		for (int i = 0; i < row.nodes.Length; i++)
		{
			FlowNode node = row.nodes[i];
			if (!NodeHasAnything(node))
			{
				continue;
			}

			if (!firstDrawn)
			{
				EditorGUILayout.LabelField("›", HubStyles.Arrow,
					GUILayout.Width(12f), GUILayout.Height(FlowCardHeight));
			}
			firstDrawn = false;

			DrawFlowNode(node);
		}

		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
		GUILayout.Space(2f);
	}

	private bool RowHasAnything(FlowRow row)
	{
		for (int i = 0; i < row.nodes.Length; i++)
		{
			if (NodeHasAnything(row.nodes[i]))
			{
				return true;
			}
		}
		return false;
	}

	private bool NodeHasAnything(FlowNode node)
	{
		switch (node.kind)
		{
			case FlowKind.Branch:
				return true;
			case FlowKind.Scene:
				return HasSceneFile(node.scene);
			case FlowKind.Group:
				return CountExisting(node.scenes) > 0;
			default:
				return false;
		}
	}

	private void DrawFlowNode(FlowNode node)
	{
		switch (node.kind)
		{
			case FlowKind.Branch:
				DrawBranchCard(node.label);
				break;
			case FlowKind.Scene:
				EditorGUILayout.BeginVertical(GUILayout.Width(FlowCardWidth));
				DrawFlowCard(node.scene);
				EditorGUILayout.EndVertical();
				break;
			case FlowKind.Group:
				DrawGroupCard(node);
				break;
		}
	}

	// 씬이 아닌 갈림길 표시. 클릭 대상 아님.
	private static void DrawBranchCard(string label)
	{
		EditorGUILayout.BeginVertical(GUILayout.Width(FlowCardWidth));
		EditorGUILayout.BeginVertical(HubStyles.Card,
			GUILayout.Width(FlowCardWidth), GUILayout.Height(FlowCardHeight));
		HubStyles.ColoredLabel(label, HubStyles.Muted, HubStyles.CardTitle);
		EditorGUILayout.LabelField("선택", HubStyles.CardSub);
		EditorGUILayout.EndVertical();
		EditorGUILayout.EndVertical();
	}

	// 스테이지처럼 여러 개인 묶음. 접으면 개수만, 펼치면 카드가 세로로 쌓임.
	private void DrawGroupCard(FlowNode node)
	{
		int count = CountExisting(node.scenes);
		bool expanded = _flowExpandedGroups.Contains(node.label);

		EditorGUILayout.BeginVertical(GUILayout.Width(FlowCardWidth));

		Rect header = EditorGUILayout.BeginVertical(HubStyles.Card,
			GUILayout.Width(FlowCardWidth), GUILayout.Height(FlowCardHeight));
		EditorGUILayout.LabelField($"{(expanded ? "▾" : "▸")} {node.label}", HubStyles.CardTitle);
		EditorGUILayout.LabelField($"{count}개", HubStyles.CardSub);
		EditorGUILayout.EndVertical();

		if (Event.current.type == EventType.MouseDown && header.Contains(Event.current.mousePosition))
		{
			if (expanded)
			{
				_flowExpandedGroups.Remove(node.label);
			}
			else
			{
				_flowExpandedGroups.Add(node.label);
			}
			Event.current.Use();
			Repaint();
		}

		if (expanded)
		{
			for (int i = 0; i < node.scenes.Length; i++)
			{
				if (HasSceneFile(node.scenes[i]))
				{
					DrawFlowCard(node.scenes[i]);
				}
			}
		}

		EditorGUILayout.EndVertical();
	}

	private void DrawFlowCard(SCENE_TYPE type)
	{
		SceneRow row = FindRow(type);
		Color status = row != null && row.inBuildSettings && row.buildEnabled
			? HubStyles.Ok
			: HubStyles.Warn;

		bool selected = _flowSelected == type;
		GUIStyle style = selected ? HubStyles.CardSelected : HubStyles.Card;

		Rect card = EditorGUILayout.BeginVertical(style,
			GUILayout.Width(FlowCardWidth), GUILayout.Height(FlowCardHeight));

		HubStyles.StatusDot(new Rect(card.x + 6f, card.y + 6f, 6f, 6f), status);

		string label = SceneDisplayNames.TryGetValue(type, out string display) ? display : type.ToString();
		EditorGUILayout.LabelField(label, HubStyles.CardTitle);
		EditorGUILayout.LabelField(type.ToString(), HubStyles.CardSub);

		EditorGUILayout.EndVertical();

		if (Event.current.type == EventType.MouseDown && card.Contains(Event.current.mousePosition))
		{
			_flowSelected = selected ? SCENE_TYPE.UNKNOWN : type;
			GUI.FocusControl(null);
			Event.current.Use();
			Repaint();
		}
	}

	private int CountExisting(SCENE_TYPE[] scenes)
	{
		int n = 0;
		for (int i = 0; i < scenes.Length; i++)
		{
			if (HasSceneFile(scenes[i]))
			{
				n++;
			}
		}
		return n;
	}

	private bool HasSceneFile(SCENE_TYPE type)
	{
		SceneRow row = FindRow(type);
		return row != null && !string.IsNullOrEmpty(row.assetPath);
	}

	private SceneRow FindRow(SCENE_TYPE type)
	{
		for (int i = 0; i < _sceneRows.Count; i++)
		{
			if (_sceneRows[i].sceneType == type)
			{
				return _sceneRows[i];
			}
		}
		return null;
	}
}
