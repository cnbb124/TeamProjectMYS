using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// =====================================================================
// PerfRecorder — 성능 지표를 프레임마다 샘플링해 CSV로 남기는 개발용 계측기.
//
// 프로파일러 창은 값을 파일로 안 뽑아주기 때문에, 병목을 사람이 눈으로 옮겨적는 대신
// 수치를 그대로 저장해서 나중에 대조할 수 있게 하는 용도임.
//
// [쓰는 법]
//   1. 아무 씬 오브젝트에 이 컴포넌트를 붙임 (전투 씬 권장)
//   2. 플레이 중 Toggle Key(기본 F9)로 녹화 시작/정지
//   3. 정지하면 Output Folder에 perf_MMdd_HHmm.csv 가 떨어짐. 콘솔에 경로도 찍힘
//
// [기록 항목]
//   시간/프레임      frame, time, dtMs, fps
//   CPU             mainThreadMs
//   GC              gcAllocFrame(byte), gcReserved, gcUsed
//   렌더            drawCalls, batches, setPassCalls, triangles, vertices
//   메모리          totalReserved, totalUsed, textureMemory
//   상황            scene, enemyCount, playerPos  ← 스파이크 순간에 뭐가 있었는지 대조용
//
// ⚠ 렌더/메모리 카운터는 에디터 또는 Development Build에서만 값이 들어옴.
//   릴리즈 빌드에서는 해당 칸이 비어서 나옴(측정 자체가 꺼져 있음).
//
// ⚠ 함수별 점유율(어느 스크립트가 몇 ms)은 이 방식으로 못 뽑음 — 그건 Deep Profile 영역임.
//   특정 함수를 의심하게 되면 그 함수에 ProfilerMarker를 심어서 여기에 칸을 추가하는 식으로 봄.
// =====================================================================
public class PerfRecorder : MonoBehaviour
{
	[Header("<size=18>녹화 조작</size>")]
	[Tooltip("이 키로 녹화 시작/정지함. None으로 두면 키 입력 없이 StartRecording()/StopRecording() 직접 호출로만 동작함")]
	[SerializeField] private Key _toggleKey = Key.F9;
	[Tooltip("켜면 씬 시작과 동시에 녹화를 시작함")]
	[SerializeField] private bool _recordOnStart = false;

	[Header("<size=18>샘플링</size>")]
	[Tooltip("몇 프레임마다 한 줄을 기록할지. 1이면 매 프레임. 값을 키우면 파일이 작아지지만 짧은 스파이크를 놓칠 수 있음")]
	[SerializeField] private int _sampleEveryNFrames = 1;
	[Tooltip("최대 기록 줄 수. 넘으면 자동으로 녹화를 멈추고 저장함(메모리 무한 증가 방지)")]
	[SerializeField] private int _maxRows = 20000;

	[Header("<size=18>출력</size>")]
	[Tooltip("비워두면 프로젝트 옆 Claude/Temp 폴더에 저장함. 절대경로를 넣으면 그쪽에 저장함")]
	[SerializeField] private string _outputFolder = "";

	[Header("<size=14>==========상태 (참고용, 입력X)</size>==========")]
	[SerializeField] private bool _isRecording;
	[SerializeField] private int _rowCount;

	public bool IsRecording
	{
		get
		{
			return _isRecording;
		}
	}

	// 지표별 레코더. Valid가 false면 그 지표는 이 환경에서 측정 불가라 빈 칸으로 남김.
	private ProfilerRecorder _mainThread;
	private ProfilerRecorder _gcAllocFrame;
	private ProfilerRecorder _gcReserved;
	private ProfilerRecorder _gcUsed;
	private ProfilerRecorder _totalReserved;
	private ProfilerRecorder _totalUsed;
	private ProfilerRecorder _textureMemory;
	private ProfilerRecorder _drawCalls;
	private ProfilerRecorder _batches;
	private ProfilerRecorder _setPassCalls;
	private ProfilerRecorder _triangles;
	private ProfilerRecorder _vertices;

	// 한 줄씩 모아두고 정지 시 한 번에 씀 — 매 프레임 파일을 열면 그 자체가 병목이 되어 측정을 망침
	private readonly List<string> _rows = new List<string>();
	private int _frameCounter;
	private float _recordStartTime;

	private void OnEnable()
	{
		// 레코더는 활성화 구간에만 열어둠. 안 닫으면 네이티브 핸들이 남음
		_mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
		_gcAllocFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
		_gcReserved = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
		_gcUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
		_totalReserved = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
		_totalUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
		_textureMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory");
		_drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
		_batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
		_setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
		_triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
		_vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");

		if (_recordOnStart)
		{
			StartRecording();
		}
	}

	private void OnDisable()
	{
		// 녹화 중이었으면 여기서라도 저장해야 데이터가 날아가지 않음(씬 전환/플레이 정지 대응)
		if (_isRecording)
		{
			StopRecording();
		}

		_mainThread.Dispose();
		_gcAllocFrame.Dispose();
		_gcReserved.Dispose();
		_gcUsed.Dispose();
		_totalReserved.Dispose();
		_totalUsed.Dispose();
		_textureMemory.Dispose();
		_drawCalls.Dispose();
		_batches.Dispose();
		_setPassCalls.Dispose();
		_triangles.Dispose();
		_vertices.Dispose();
	}

	private void Update()
	{
		if (_toggleKey != Key.None && Keyboard.current != null && Keyboard.current[_toggleKey].wasPressedThisFrame)
		{
			if (_isRecording)
			{
				StopRecording();
			}
			else
			{
				StartRecording();
			}
		}

		if (!_isRecording)
		{
			return;
		}

		_frameCounter++;
		int interval = _sampleEveryNFrames > 0 ? _sampleEveryNFrames : 1;
		if (_frameCounter < interval)
		{
			return;
		}
		_frameCounter = 0;

		AppendRow();

		if (_rows.Count >= _maxRows)
		{
			Debug.LogWarning($"[PerfRecorder] 최대 줄 수({_maxRows}) 도달 — 자동 저장하고 녹화를 멈춤.");
			StopRecording();
		}
	}

	/// <summary>녹화 시작. 이미 녹화 중이면 무시함.</summary>
	public void StartRecording()
	{
		if (_isRecording)
		{
			return;
		}
		_rows.Clear();
		_rowCount = 0;
		_frameCounter = 0;
		_recordStartTime = Time.unscaledTime;
		_isRecording = true;
		Debug.Log("[PerfRecorder] 녹화 시작");
	}

	/// <summary>녹화 정지 후 CSV 저장. 기록이 없으면 파일을 만들지 않음.</summary>
	public void StopRecording()
	{
		if (!_isRecording)
		{
			return;
		}
		_isRecording = false;

		if (_rows.Count == 0)
		{
			Debug.Log("[PerfRecorder] 기록된 줄이 없어 저장하지 않음.");
			return;
		}

		string path = BuildOutputPath();
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("frame,time,dtMs,fps,mainThreadMs,gcAllocFrame,gcReserved,gcUsed," +
					  "totalReserved,totalUsed,textureMemory,drawCalls,batches,setPassCalls," +
					  "triangles,vertices,scene,enemyCount,playerX,playerY,playerZ");
		for (int i = 0; i < _rows.Count; i++)
		{
			sb.AppendLine(_rows[i]);
		}

		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
			Debug.Log($"[PerfRecorder] 저장 완료 ({_rows.Count}줄, {Time.unscaledTime - _recordStartTime:F1}초): {path}");
		}
		catch (IOException e)
		{
			Debug.LogError($"[PerfRecorder] 저장 실패: {e.Message}");
		}
	}

	private void AppendRow()
	{
		float dt = Time.unscaledDeltaTime;
		float fps = dt > 0f ? 1f / dt : 0f;

		// Main Thread는 나노초로 옴 — ms로 환산해야 프로파일러 창 값과 같은 단위가 됨
		string mainThreadMs = _mainThread.Valid ? (_mainThread.LastValue * 1e-6).ToString("F3") : "";

		string scene = SceneManager.GetActiveScene().name;
		int enemyCount = UnitManager.Instance != null ? UnitManager.Instance.GetAllEnemies().Count : -1;

		Vector3 playerPos = Vector3.zero;
		if (GameManager.Instance != null && GameManager.Instance.playerRef != null)
		{
			playerPos = GameManager.Instance.playerRef.transform.position;
		}

		_rows.Add(string.Join(",",
			Time.frameCount.ToString(),
			(Time.unscaledTime - _recordStartTime).ToString("F3"),
			(dt * 1000f).ToString("F3"),
			fps.ToString("F1"),
			mainThreadMs,
			Read(_gcAllocFrame),
			Read(_gcReserved),
			Read(_gcUsed),
			Read(_totalReserved),
			Read(_totalUsed),
			Read(_textureMemory),
			Read(_drawCalls),
			Read(_batches),
			Read(_setPassCalls),
			Read(_triangles),
			Read(_vertices),
			scene,
			enemyCount.ToString(),
			playerPos.x.ToString("F1"),
			playerPos.y.ToString("F1"),
			playerPos.z.ToString("F1")));

		_rowCount = _rows.Count;
	}

	// 측정 불가(Valid=false)면 빈 칸으로 남김 — 0으로 채우면 "값이 0"과 구분이 안 됨
	private string Read(ProfilerRecorder recorder)
	{
		if (!recorder.Valid)
		{
			return "";
		}
		return recorder.LastValue.ToString();
	}

	// 지정 폴더가 없으면 프로젝트 폴더 옆의 Claude/Temp를 씀.
	// Application.dataPath는 <프로젝트>/Assets 이므로 두 단계 올라가면 프로젝트의 부모 폴더가 됨.
	private string BuildOutputPath()
	{
		string folder = _outputFolder;
		if (string.IsNullOrWhiteSpace(folder))
		{
			folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Claude", "Temp"));
		}
		string fileName = $"perf_{System.DateTime.Now:MMdd_HHmm}.csv";
		return Path.Combine(folder, fileName);
	}
}
