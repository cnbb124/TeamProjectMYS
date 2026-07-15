using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class BulletPatternEditor : EditorWindow
{
    // ── 현재 편집 중인 데이터 ─────────────────────────────
    private BulletPatternData _target;
    private int _selectedWaveIndex = 0;

    // ── 캔버스 설정 ───────────────────────────────────────
    private Vector2 _canvasCenter;
    private float   _canvasRadius = 150f;   // 캔버스 원 반지름 (픽셀)
    private float   _worldRadius  = 30f;    // 실제 게임 단위 반경 (Gizmo 연동)

    // ── 드래그 상태 ───────────────────────────────────────
    private bool    _isDragging   = false;
    private Vector2 _dragStart;

    // ── 스타일 ────────────────────────────────────────────
    private Color _bgColor      = new Color(0.1f, 0.1f, 0.15f);
    private Color _circleColor  = new Color(0f, 1f, 0.8f, 0.3f);
    private Color _pointColor   = new Color(1f, 0.4f, 0.1f);
    private Color _lineColor    = new Color(1f, 0.8f, 0.2f, 0.8f);
    private Color _aimColor     = new Color(0.4f, 0.8f, 1f);

    // ─────────────────────────────────────────────────────
    [MenuItem("Barrage/Bullet Pattern Editor")]
    public static void OpenWindow()
    {
        var win = GetWindow<BulletPatternEditor>("Bullet Pattern Editor");
        win.minSize = new Vector2(600f, 500f);
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (_target == null)
        {
            EditorGUILayout.HelpBox("상단에서 BulletPatternData를 선택하세요.", MessageType.Info);
            return;
        }

        DrawWaveList();
        DrawCanvas();
        DrawPointList();
    }

    // ── 상단 툴바 ─────────────────────────────────────────

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        _target = (BulletPatternData)EditorGUILayout.ObjectField(
            "Pattern Data", _target, typeof(BulletPatternData), false);

        _worldRadius = EditorGUILayout.FloatField("World Radius", _worldRadius, GUILayout.Width(150));

        if (GUILayout.Button("New Pattern", EditorStyles.toolbarButton, GUILayout.Width(100)))
            CreateNewPattern();

        if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60)))
            SavePattern();

        EditorGUILayout.EndHorizontal();
    }

    // ── 왼쪽: 웨이브 목록 ────────────────────────────────

    private void DrawWaveList()
    {
        GUI.BeginGroup(new Rect(5, 30, 150, position.height - 30));

        GUILayout.Label("Waves", EditorStyles.boldLabel);

        if (_target.waves == null)
            _target.waves = new List<PatternWave>();

        for (int i = 0; i < _target.waves.Count; i++)
        {
            bool isSelected = (i == _selectedWaveIndex);
            GUI.backgroundColor = isSelected ? Color.cyan : Color.white;

            if (GUILayout.Button($"[{i}] {_target.waves[i].waveName}", GUILayout.Width(140)))
                _selectedWaveIndex = i;
        }

        GUI.backgroundColor = Color.white;
        GUILayout.Space(5);

        if (GUILayout.Button("+ Add Wave", GUILayout.Width(140)))
        {
            Undo.RecordObject(_target, "Add Wave");
            _target.waves.Add(new PatternWave { waveName = $"Wave {_target.waves.Count}" });
            _selectedWaveIndex = _target.waves.Count - 1;
            EditorUtility.SetDirty(_target);
        }

        if (_target.waves.Count > 0 && GUILayout.Button("- Remove Wave", GUILayout.Width(140)))
        {
            Undo.RecordObject(_target, "Remove Wave");
            _target.waves.RemoveAt(_selectedWaveIndex);
            _selectedWaveIndex = Mathf.Clamp(_selectedWaveIndex, 0, _target.waves.Count - 1);
            EditorUtility.SetDirty(_target);
        }

        if (_target.waves.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.Label("Wave 설정", EditorStyles.boldLabel);
            PatternWave wave = _target.waves[_selectedWaveIndex];

            GUILayout.Label("이름");
            wave.waveName = GUILayout.TextField(wave.waveName, GUILayout.Width(140));

            GUILayout.Label("Delay(초)");
            float.TryParse(GUILayout.TextField(wave.delay.ToString("F2"), GUILayout.Width(140)), out wave.delay);
        }

        GUI.EndGroup();
    }

    // ── 가운데: 탄막 캔버스 ──────────────────────────────

    private void DrawCanvas()
    {
        // 캔버스 고정 위치 (왼쪽 패널 160px, 오른쪽 패널 165px 제외)
        float canvasSize = Mathf.Min(position.width - 325f, position.height - 40f);
        canvasSize = Mathf.Max(canvasSize, 200f);
        float startX = 160f;
        float startY = 35f;
        Rect canvasRect = new Rect(startX, startY, canvasSize, canvasSize);
        _canvasRadius = canvasSize * 0.45f;

        _canvasCenter = new Vector2(canvasRect.x + canvasRect.width * 0.5f,
                                    canvasRect.y + canvasRect.height * 0.5f);

        // 배경
        EditorGUI.DrawRect(canvasRect, _bgColor);

        // 원 그리기 (보스 탐지 범위 시각화)
        Handles.color = _circleColor;
        Handles.DrawWireDisc(_canvasCenter, Vector3.forward, _canvasRadius);

        // 십자선
        Handles.color = new Color(1f, 1f, 1f, 0.15f);
        Handles.DrawLine(new Vector3(canvasRect.x, _canvasCenter.y),
                         new Vector3(canvasRect.xMax, _canvasCenter.y));
        Handles.DrawLine(new Vector3(_canvasCenter.x, canvasRect.y),
                         new Vector3(_canvasCenter.x, canvasRect.yMax));

        // 보스 위치 (중앙 점)
        Handles.color = Color.white;
        Handles.DrawSolidDisc(_canvasCenter, Vector3.forward, 5f);

        // 현재 웨이브 포인트 그리기
        if (_target.waves != null && _target.waves.Count > 0)
            DrawWavePoints(_target.waves[_selectedWaveIndex]);

        // 마우스 이벤트 처리
        HandleCanvasInput(canvasRect);

        // 안내 텍스트
        GUI.Label(new Rect(canvasRect.x + 5, canvasRect.yMax - 20, 300, 20),
            "클릭: 점 추가 | 드래그: 선(탄 배열) | Shift+클릭: 플레이어 조준 탄",
            EditorStyles.miniLabel);
    }

    private void DrawWavePoints(PatternWave wave)
    {
        if (wave.points == null) return;

        foreach (PatternPoint p in wave.points)
        {
            Vector2 screenPos = LocalDirToScreen(p.localDir);

            // 보스 → 포인트 방향선
            Handles.color = p.aimAtPlayer ? _aimColor : _lineColor;
            Handles.DrawLine(_canvasCenter, screenPos);

            // 포인트 원
            Handles.color = p.aimAtPlayer ? _aimColor : _pointColor;
            Handles.DrawSolidDisc(screenPos, Vector3.forward, 5f);
        }
    }

    private void HandleCanvasInput(Rect canvasRect)
    {
        Event e = Event.current;
        if (!canvasRect.Contains(e.mousePosition)) return;

        // 마우스 클릭 → 점 추가
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            _isDragging  = true;
            _dragStart   = e.mousePosition;
            e.Use();
        }

        // 드래그 끝 → 선 처리 (점 여러 개 배열)
        if (e.type == EventType.MouseUp && e.button == 0 && _isDragging)
        {
            _isDragging = false;
            Vector2 dragEnd = e.mousePosition;
            bool aimAtPlayer = e.shift;

            float dragDist = Vector2.Distance(_dragStart, dragEnd);

            if (dragDist < 5f)
            {
                // 클릭 (드래그 없음) → 점 1개 추가
                AddPoint(_dragStart, aimAtPlayer);
            }
            else
            {
                // 드래그 → 선 위에 일정 간격으로 점 배열
                int count = Mathf.Max(2, Mathf.RoundToInt(dragDist / 20f));
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / (count - 1);
                    Vector2 pos = Vector2.Lerp(_dragStart, dragEnd, t);
                    AddPoint(pos, aimAtPlayer);
                }
            }

            e.Use();
            Repaint();
        }

        // 우클릭 → 가장 가까운 점 제거
        if (e.type == EventType.MouseDown && e.button == 1)
        {
            RemoveNearestPoint(e.mousePosition);
            e.Use();
            Repaint();
        }
    }

    // ── 오른쪽: 포인트 목록 ──────────────────────────────

    private Vector2 _pointListScroll;

    private void DrawPointList()
    {
        float panelWidth = 160f;
        float startX = position.width - panelWidth - 5f;
        GUI.BeginGroup(new Rect(startX, 30, panelWidth, position.height - 30));

        GUILayout.Label("Points", EditorStyles.boldLabel);

        if (_target.waves == null || _target.waves.Count == 0)
        {
            GUI.EndGroup();
            return;
        }

        PatternWave wave = _target.waves[_selectedWaveIndex];
        if (wave.points == null) wave.points = new List<PatternPoint>();

        _pointListScroll = GUILayout.BeginScrollView(_pointListScroll, GUILayout.Width(panelWidth));

        for (int i = 0; i < wave.points.Count; i++)
        {
            PatternPoint p = wave.points[i];
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label($"Point {i}", EditorStyles.miniLabel);
            p.localDir    = EditorGUILayout.Vector2Field("Dir", p.localDir);
            p.speed       = EditorGUILayout.FloatField("Speed", p.speed);
            p.aimAtPlayer = EditorGUILayout.Toggle("Aim Player", p.aimAtPlayer);

            if (GUILayout.Button("삭제", GUILayout.Height(18)))
            {
                Undo.RecordObject(_target, "Remove Point");
                wave.points.RemoveAt(i);
                EditorUtility.SetDirty(_target);
                break;
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }

        GUILayout.EndScrollView();

        if (GUILayout.Button("전체 삭제", GUILayout.Width(panelWidth)))
        {
            Undo.RecordObject(_target, "Clear Points");
            wave.points.Clear();
            EditorUtility.SetDirty(_target);
        }

        GUI.EndGroup();
    }

    // ── 헬퍼 ─────────────────────────────────────────────

    private void AddPoint(Vector2 screenPos, bool aimAtPlayer)
    {
        if (_target.waves == null || _target.waves.Count == 0) return;

        Vector2 localDir = ScreenToLocalDir(screenPos);

        Undo.RecordObject(_target, "Add Point");
        _target.waves[_selectedWaveIndex].points.Add(new PatternPoint
        {
            localDir    = localDir,
            speed       = 10f,
            aimAtPlayer = aimAtPlayer
        });
        EditorUtility.SetDirty(_target);
    }

    private void RemoveNearestPoint(Vector2 mousePos)
    {
        if (_target.waves == null || _target.waves.Count == 0) return;

        PatternWave wave = _target.waves[_selectedWaveIndex];
        if (wave.points == null || wave.points.Count == 0) return;

        int   nearest = -1;
        float minDist = 15f; // 이 거리 안에 있는 점만 삭제

        for (int i = 0; i < wave.points.Count; i++)
        {
            float dist = Vector2.Distance(LocalDirToScreen(wave.points[i].localDir), mousePos);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = i;
            }
        }

        if (nearest >= 0)
        {
            Undo.RecordObject(_target, "Remove Point");
            wave.points.RemoveAt(nearest);
            EditorUtility.SetDirty(_target);
        }
    }

    // 스크린 좌표 → localDir (-1~1 정규화)
    private Vector2 ScreenToLocalDir(Vector2 screenPos)
    {
        Vector2 offset = screenPos - _canvasCenter;
        offset.y = -offset.y; // Y축 반전 (Unity UI Y와 월드 Y 방향 맞춤)
        return offset / _canvasRadius;
    }

    // localDir → 스크린 좌표
    private Vector2 LocalDirToScreen(Vector2 localDir)
    {
        Vector2 offset = localDir * _canvasRadius;
        offset.y = -offset.y;
        return _canvasCenter + offset;
    }

    private void CreateNewPattern()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Bullet Pattern", "NewBulletPattern", "asset", "패턴 저장 위치 선택");
        if (string.IsNullOrEmpty(path)) return;

        var asset = CreateInstance<BulletPatternData>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        _target = asset;
    }

    private void SavePattern()
    {
        if (_target == null) return;
        EditorUtility.SetDirty(_target);
        AssetDatabase.SaveAssets();
        Debug.Log("[BulletPatternEditor] 저장 완료!");
    }
}