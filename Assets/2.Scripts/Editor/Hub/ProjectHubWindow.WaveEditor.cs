using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================
// ProjectHubWindow — 웨이브 표 편집기.
//
// WaveData 에셋을 인스펙터 대신 표 한 줄 = 적 한 종류로 편집함.
// 전투 스테이지에서만 씀 — 다른 종류의 씬엔 SpawnManager 자체가 없음.
//
// 씬 설정과 달리 초안([적용] 버튼)을 두지 않음.
// 웨이브는 숫자를 여러 번 만져가며 감을 잡는 작업이라 매번 버튼을 누르는 쪽이 더 번거로움.
// 대신 값 변경은 Undo에 남으므로 Ctrl+Z로 되돌릴 수 있음.
// =====================================================================
public partial class ProjectHubWindow
{
	private const string WaveAssetFolder = "Assets/8.Data/EnemyWaveData";

	// 적 종류 선택지. POOL_TYPE을 통째로 띄우면 투사체·아이템까지 나와서 적만 추림.
	// 적 종류가 늘면 여기 두 배열에 같은 자리로 추가할 것
	private static readonly int[] EnemyPoolValues =
	{
		(int)POOL_TYPE.ENEMY_GUNSHIP,
		(int)POOL_TYPE.ENEMY_DROPSHIP,
		(int)POOL_TYPE.ENEMY_MISSILESHIP,
		(int)POOL_TYPE.ENEMY_WORKER,
		(int)POOL_TYPE.ENEMY_BOSS,
		(int)POOL_TYPE.ENEMY_STATION,
	};
	private static readonly string[] EnemyPoolNames =
	{
		"건쉽", "드롭쉽", "미사일쉽", "채굴선", "보스", "정거장(구조물)",
	};
	private static GUIContent[] _enemyPoolLabels;

	private static GUIContent[] EnemyPoolLabels
	{
		get
		{
			if (_enemyPoolLabels == null)
			{
				_enemyPoolLabels = new GUIContent[EnemyPoolNames.Length];
				for (int i = 0; i < EnemyPoolNames.Length; i++)
				{
					_enemyPoolLabels[i] = new GUIContent(EnemyPoolNames[i]);
				}
			}
			return _enemyPoolLabels;
		}
	}

	private static readonly string[] SpawnPlaceLabels = { "랜덤", "지정 위치" };

	// 웨이브 하나가 적 몇 마리를 몇 초에 걸쳐 뱉는지.
	// SpawnManager 실제 진행 순서 그대로 계산함 —
	// entries를 순서대로 처리하고, 각 항목은 delay만큼 쉰 뒤 count번 스폰하며 스폰 사이에 interval을 둠
	// (마지막 개체 뒤엔 간격을 안 둠). nextWaveDelay는 적을 다 잡은 뒤의 대기라 여기 안 넣음.
	private static void GetWaveSummary(WaveData wave, out int totalCount, out float seconds)
	{
		totalCount = 0;
		seconds = 0f;

		if (wave == null || wave.entries == null)
		{
			return;
		}

		for (int i = 0; i < wave.entries.Length; i++)
		{
			WaveData.SpawnEntry entry = wave.entries[i];
			if (entry == null)
			{
				continue;
			}

			int count = Mathf.Max(0, entry.count);
			totalCount += count;
			seconds += Mathf.Max(0f, entry.delay);
			if (count > 1)
			{
				seconds += (count - 1) * Mathf.Max(0f, entry.interval);
			}
		}
	}

	// 웨이브 하나를 표로. 값이 바뀌면 에셋에 바로 반영됨
	private void DrawWaveTable(WaveData wave)
	{
		if (wave == null)
		{
			return;
		}

		SerializedObject so = new SerializedObject(wave);
		so.Update();

		SerializedProperty entries = so.FindProperty("entries");
		SerializedProperty nextWaveDelay = so.FindProperty("nextWaveDelay");
		if (entries == null)
		{
			EditorGUILayout.HelpBox("이 에셋에서 웨이브 목록을 찾지 못했습니다.", MessageType.Warning);
			return;
		}

		// ---- 표 머리 ----
		EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
		EditorGUILayout.LabelField("적 종류", EditorStyles.miniBoldLabel, GUILayout.Width(120f));
		EditorGUILayout.LabelField("수", EditorStyles.miniBoldLabel, GUILayout.Width(40f));
		EditorGUILayout.LabelField("간격", EditorStyles.miniBoldLabel, GUILayout.Width(46f));
		EditorGUILayout.LabelField("지연", EditorStyles.miniBoldLabel, GUILayout.Width(46f));
		EditorGUILayout.LabelField("스폰 위치", EditorStyles.miniBoldLabel);
		GUILayout.Space(26f);
		EditorGUILayout.EndHorizontal();

		int removeIndex = -1;
		for (int i = 0; i < entries.arraySize; i++)
		{
			SerializedProperty entry = entries.GetArrayElementAtIndex(i);
			SerializedProperty poolType = entry.FindPropertyRelative("poolType");
			SerializedProperty count = entry.FindPropertyRelative("count");
			SerializedProperty interval = entry.FindPropertyRelative("interval");
			SerializedProperty delay = entry.FindPropertyRelative("delay");
			SerializedProperty spawnPointIndex = entry.FindPropertyRelative("spawnPointIndex");

			EditorGUILayout.BeginHorizontal();

			// 목록에 없는 값(적이 아닌 POOL_TYPE)이 들어 있으면 IntPopup이 빈칸으로 보이므로 그대로 enum으로 띄움
			if (System.Array.IndexOf(EnemyPoolValues, poolType.intValue) >= 0)
			{
				EditorGUILayout.IntPopup(poolType, EnemyPoolLabels, EnemyPoolValues,
					GUIContent.none, GUILayout.Width(120f));
			}
			else
			{
				EditorGUILayout.PropertyField(poolType, GUIContent.none, GUILayout.Width(120f));
			}

			count.intValue = Mathf.Max(0, EditorGUILayout.IntField(count.intValue, GUILayout.Width(40f)));
			interval.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(interval.floatValue, GUILayout.Width(46f)));
			delay.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(delay.floatValue, GUILayout.Width(46f)));

			// 스폰 위치는 -1이 랜덤이라는 규칙 자체를 감춤 — 화면엔 랜덤/지정으로만 보이게
			int placeMode = spawnPointIndex.intValue >= 0 ? 1 : 0;
			int pickedMode = EditorGUILayout.Popup(placeMode, SpawnPlaceLabels, GUILayout.Width(80f));
			if (pickedMode != placeMode)
			{
				spawnPointIndex.intValue = pickedMode == 1 ? 0 : -1;
			}
			if (spawnPointIndex.intValue >= 0)
			{
				// 고정 포인트 번호도 사람이 세는 대로 1번부터 보여주고, 저장할 때만 0부터로 되돌림
				int shownNumber = Mathf.Max(1, EditorGUILayout.IntField(spawnPointIndex.intValue + 1, GUILayout.Width(40f)));
				spawnPointIndex.intValue = shownNumber - 1;
				EditorGUILayout.LabelField("번", EditorStyles.miniLabel, GUILayout.Width(18f));
			}

			GUILayout.FlexibleSpace();
			if (GUILayout.Button("✕", GUILayout.Width(24f)))
			{
				removeIndex = i;
			}
			EditorGUILayout.EndHorizontal();
		}

		if (entries.arraySize == 0)
		{
			HubStyles.ColoredLabel("아직 적이 없습니다. 아래 [+ 적 추가]로 채워 주십시오.",
				HubStyles.Muted, EditorStyles.miniLabel);
		}

		if (removeIndex >= 0)
		{
			entries.DeleteArrayElementAtIndex(removeIndex);
		}

		// ---- 추가 / 요약 ----
		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("+ 적 추가", GUILayout.Width(90f)))
		{
			AppendSpawnEntry(entries);
		}

		GUILayout.FlexibleSpace();
		EditorGUILayout.LabelField("다음 웨이브까지", EditorStyles.miniLabel, GUILayout.Width(88f));
		nextWaveDelay.floatValue = Mathf.Max(0f,
			EditorGUILayout.FloatField(nextWaveDelay.floatValue, GUILayout.Width(46f)));
		EditorGUILayout.LabelField("초", EditorStyles.miniLabel, GUILayout.Width(18f));
		EditorGUILayout.EndHorizontal();

		so.ApplyModifiedProperties();

		GetWaveSummary(wave, out int totalCount, out float seconds);
		HubStyles.ColoredLabel($"총 {totalCount}마리 · 다 나오는 데 약 {seconds:0.#}초",
			HubStyles.Muted, EditorStyles.miniLabel);
	}

	// 새 줄은 마지막 줄을 복제하는 Unity 기본 동작 대신 기본값으로 채움 —
	// 앞 줄 값이 딸려오면 고친 줄 알고 지나치기 쉬움
	private static void AppendSpawnEntry(SerializedProperty entries)
	{
		entries.InsertArrayElementAtIndex(entries.arraySize);
		SerializedProperty added = entries.GetArrayElementAtIndex(entries.arraySize - 1);

		added.FindPropertyRelative("poolType").intValue = (int)POOL_TYPE.ENEMY_GUNSHIP;
		added.FindPropertyRelative("count").intValue = 1;
		added.FindPropertyRelative("interval").floatValue = 0.5f;
		added.FindPropertyRelative("delay").floatValue = 0f;
		added.FindPropertyRelative("spawnPointIndex").intValue = -1;
	}

	// 웨이브 목록 전체. 순서가 곧 등장 순서라 위/아래로 옮길 수 있게 둠.
	// ownerName은 새 에셋의 기본 이름에만 씀(어느 씬 것인지 파일명에 남기려고)
	private void DrawWaveListEditor(List<WaveData> waves, ref WaveData bossWave, string ownerName)
	{
		// 보스 에셋 생성은 GUI가 끝난 뒤에 돌아서 그때는 ref를 못 씀 — 다음 그리기에서 받아 붙임
		if (_pendingBossWave != null)
		{
			bossWave = _pendingBossWave;
			_pendingBossWave = null;
			GUI.changed = true;
		}

		int removeIndex = -1;
		int moveIndex = -1;
		int moveDir = 0;

		for (int i = 0; i < waves.Count; i++)
		{
			EditorGUILayout.BeginVertical(HubStyles.Card);

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField($"웨이브 {i + 1}", EditorStyles.boldLabel, GUILayout.Width(80f));
			waves[i] = (WaveData)EditorGUILayout.ObjectField(waves[i], typeof(WaveData), false);

			EditorGUI.BeginDisabledGroup(i == 0);
			if (GUILayout.Button("▲", GUILayout.Width(24f)))
			{
				moveIndex = i;
				moveDir = -1;
			}
			EditorGUI.EndDisabledGroup();
			EditorGUI.BeginDisabledGroup(i == waves.Count - 1);
			if (GUILayout.Button("▼", GUILayout.Width(24f)))
			{
				moveIndex = i;
				moveDir = 1;
			}
			EditorGUI.EndDisabledGroup();
			if (GUILayout.Button("✕", GUILayout.Width(24f)))
			{
				removeIndex = i;
			}
			EditorGUILayout.EndHorizontal();

			if (waves[i] != null)
			{
				DrawWaveTable(waves[i]);
			}
			else
			{
				HubStyles.ColoredLabel("빈 칸입니다. 웨이브 에셋을 끌어다 놓거나 아래 [+ 새 웨이브 만들기]를 쓰십시오.",
					HubStyles.Warn, EditorStyles.miniLabel);
			}

			EditorGUILayout.EndVertical();
		}

		if (removeIndex >= 0)
		{
			waves.RemoveAt(removeIndex);
		}
		if (moveIndex >= 0)
		{
			int target = moveIndex + moveDir;
			if (target >= 0 && target < waves.Count)
			{
				(waves[moveIndex], waves[target]) = (waves[target], waves[moveIndex]);
			}
		}

		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("+ 새 웨이브 만들기", GUILayout.Height(22f), GUILayout.Width(140f)))
		{
			// 에셋 생성은 저장 대화상자(모달)를 띄우므로 GUI가 끝난 뒤에 실행
			List<WaveData> target = waves;
			string suggested = SuggestWaveName(ownerName, waves.Count + 1, false);
			EditorApplication.delayCall += () =>
			{
				WaveData created = CreateWaveAsset(suggested);
				if (created != null)
				{
					target.Add(created);
					Repaint();
				}
			};
		}
		if (GUILayout.Button("빈 칸 추가", GUILayout.Height(22f), GUILayout.Width(90f)))
		{
			waves.Add(null);
		}
		EditorGUILayout.EndHorizontal();

		// ---- 보스 웨이브 ----
		GUILayout.Space(6f);
		EditorGUILayout.BeginHorizontal();
		bossWave = (WaveData)EditorGUILayout.ObjectField("보스 웨이브", bossWave, typeof(WaveData), false);
		if (bossWave == null && GUILayout.Button("새로 만들기", GUILayout.Width(90f)))
		{
			string suggested = SuggestWaveName(ownerName, 0, true);
			EditorApplication.delayCall += () =>
			{
				WaveData created = CreateWaveAsset(suggested);
				if (created != null)
				{
					_pendingBossWave = created;
					Repaint();
				}
			};
		}
		EditorGUILayout.EndHorizontal();

		if (bossWave != null)
		{
			HubStyles.ColoredLabel("보스 웨이브는 마지막에 워프 연출과 함께 나옵니다. 비워두면 보스 없이 끝납니다.",
				HubStyles.Muted, EditorStyles.miniLabel);
			DrawWaveTable(bossWave);
		}
	}

	// delayCall 안에서는 ref 인자를 못 건드려서, 만들어진 에셋을 여기 담아 다음 그리기에서 붙임
	private WaveData _pendingBossWave;

	// 이미 만들어진 씬의 웨이브 편집.
	// 웨이브는 SpawnManager라는 '씬 안 오브젝트'가 들고 있어서, 그 씬이 열려 있어야만 고칠 수 있음.
	private void DrawSceneWaveSection(SCENE_TYPE sceneType)
	{
		string activeName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
		if (!string.Equals(activeName, sceneType.ToString(), System.StringComparison.OrdinalIgnoreCase))
		{
			EditorGUILayout.HelpBox("웨이브는 그 씬 안에 들어 있어서, 씬을 열어야 고칠 수 있습니다.", MessageType.Info);

			SceneRow row = FindRow(sceneType);
			if (row != null && !string.IsNullOrEmpty(row.assetPath))
			{
				if (GUILayout.Button("이 씬 열고 편집하기", GUILayout.Height(22f)))
				{
					string path = row.assetPath;
					EditorApplication.delayCall += () => OpenScene(path);
				}
			}
			return;
		}

		SpawnManager spawner = Object.FindObjectOfType<SpawnManager>(true);
		if (spawner == null)
		{
			EditorGUILayout.HelpBox("이 씬에 SpawnManager가 없어 웨이브를 넣을 수 없습니다.\n" +
									"SpawnManager 프리팹을 씬에 배치한 뒤 다시 여십시오.", MessageType.Warning);
			return;
		}

		List<WaveData> waves = new List<WaveData>();
		if (spawner.waves != null)
		{
			waves.AddRange(spawner.waves);
		}
		WaveData boss = spawner.bossWave;

		DrawWaveListEditor(waves, ref boss, sceneType.ToString());

		// 목록/보스가 실제로 달라졌을 때만 씬을 더럽힘 — 매 그리기마다 쓰면 씬이 계속 '수정됨'이 됨
		if (!SameWaveList(spawner.waves, waves) || spawner.bossWave != boss)
		{
			Undo.RecordObject(spawner, "웨이브 편집");
			spawner.waves = waves.ToArray();
			spawner.bossWave = boss;
			EditorUtility.SetDirty(spawner);
			UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
		}

		HubStyles.ColoredLabel("웨이브 순서와 보스는 이 씬에 저장됩니다 — Ctrl+S로 씬을 저장해야 남습니다.",
			HubStyles.Warn, EditorStyles.miniLabel);
	}

	private static bool SameWaveList(WaveData[] current, List<WaveData> edited)
	{
		int currentLength = current != null ? current.Length : 0;
		if (currentLength != edited.Count)
		{
			return false;
		}
		for (int i = 0; i < currentLength; i++)
		{
			if (current[i] != edited[i])
			{
				return false;
			}
		}
		return true;
	}

	// 이름은 사람이 직접 정하게 함 — 자동 번호(WaveData1/2…)를 붙이면
	// 어느 스테이지 것인지 파일 이름만 봐선 알 수가 없어서 나중에 헷갈림.
	// 기본 이름만 씬 이름 기준으로 채워주고 바꿀 수 있게 둠.
	private static WaveData CreateWaveAsset(string suggestedName)
	{
		if (string.IsNullOrWhiteSpace(suggestedName))
		{
			suggestedName = "Wave";
		}

		string path = EditorUtility.SaveFilePanelInProject("웨이브 저장", suggestedName, "asset",
			"이 웨이브를 어떤 이름으로 저장할지 정할 것", WaveAssetFolder);
		if (string.IsNullOrEmpty(path))
		{
			return null;
		}

		WaveData asset = ScriptableObject.CreateInstance<WaveData>();
		asset.entries = new WaveData.SpawnEntry[0];
		asset.nextWaveDelay = 3f;

		AssetDatabase.CreateAsset(asset, path);
		AssetDatabase.SaveAssets();
		EditorGUIUtility.PingObject(asset);
		Debug.Log($"[Hub] 웨이브 에셋 생성: {path}");
		return asset;
	}

	// 새 웨이브의 기본 이름. 어느 씬의 몇 번째인지가 파일명에 남게 함
	private static string SuggestWaveName(string ownerName, int order, bool isBoss)
	{
		string owner = string.IsNullOrWhiteSpace(ownerName) ? "Wave" : $"Wave_{ownerName}";
		return isBoss ? $"{owner}_Boss" : $"{owner}_{order}";
	}
}
