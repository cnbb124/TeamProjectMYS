#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class VRHudPlacementWindow : EditorWindow
{
    private const string CockpitPrefabPath =
        "Assets/3.Prefabs/cockpit/cockpit.prefab";

    private CockpitViewSwitcher _liveSwitcher;
    private float _distance = 1.5f;
    private float _scale = 0.001f;
    private Vector3 _localOffset = Vector3.zero;
    private Vector3 _localEulerAngles = Vector3.zero;
    private Vector2 _radarOffset = Vector2.zero;
    private Vector2 _hpOffset = Vector2.zero;
    private Vector2 _fuelOffset = Vector2.zero;
    private Vector2 _weaponOffset = Vector2.zero;
    private Vector2 _boosterOffset = Vector2.zero;

    [MenuItem("Tools/VR/HUD Placement")]
    private static void Open()
    {
        GetWindow<VRHudPlacementWindow>("VR HUD Placement");
    }

    private void OnEnable()
    {
        LoadFromPrefab();
        FindLiveSwitcher();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "기존 HUD Canvas는 수정하지 않습니다. Play Mode에서 VR 배치만 " +
            "미리 본 뒤 Save를 눌러야 cockpit.prefab에 저장됩니다.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(
                "Live Switcher",
                _liveSwitcher,
                typeof(CockpitViewSwitcher),
                true);
        }

        if (GUILayout.Button("Find Live Cockpit"))
        {
            FindLiveSwitcher();
        }

        EditorGUI.BeginChangeCheck();
        _distance = EditorGUILayout.Slider("Forward Distance", _distance, 0.05f, 10f);
        _localOffset = EditorGUILayout.Vector3Field("Local Offset", _localOffset);
        _localEulerAngles = EditorGUILayout.Vector3Field(
            "Local Rotation",
            _localEulerAngles);
        _scale = EditorGUILayout.Slider("HUD Scale", _scale, 0.00005f, 0.01f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Individual HUD Elements", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "아래 값은 기존 HUD 요소 위치에 더해지는 VR 전용 Canvas 오프셋입니다.",
            MessageType.None);
        _radarOffset = EditorGUILayout.Vector2Field("Radar (PanelRadar)", _radarOffset);
        _hpOffset = EditorGUILayout.Vector2Field("HP", _hpOffset);
        _fuelOffset = EditorGUILayout.Vector2Field("Fuel", _fuelOffset);
        _weaponOffset = EditorGUILayout.Vector2Field("Weapon", _weaponOffset);
        _boosterOffset = EditorGUILayout.Vector2Field("Booster", _boosterOffset);

        if (EditorGUI.EndChangeCheck() && Application.isPlaying)
        {
            ApplyPreview();
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Apply Preview"))
            {
                ApplyPreview();
            }
        }

        if (GUILayout.Button("Save To Cockpit Prefab"))
        {
            SaveToPrefab();
        }

        if (GUILayout.Button("Reload Saved Values"))
        {
            LoadFromPrefab();
            ApplyPreview();
        }

        if (GUILayout.Button("Reset Fields To Defaults"))
        {
            _distance = 1.5f;
            _scale = 0.001f;
            _localOffset = Vector3.zero;
            _localEulerAngles = Vector3.zero;
            _radarOffset = Vector2.zero;
            _hpOffset = Vector2.zero;
            _fuelOffset = Vector2.zero;
            _weaponOffset = Vector2.zero;
            _boosterOffset = Vector2.zero;
            ApplyPreview();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Offset axes", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("X: left/right, Y: down/up, Z: back/forward");
        EditorGUILayout.LabelField("Play Mode changes are temporary until Save is pressed.");
    }

    private void FindLiveSwitcher()
    {
        CockpitViewSwitcher[] switchers =
            FindObjectsByType<CockpitViewSwitcher>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        _liveSwitcher = null;
        for (int i = 0; i < switchers.Length; i++)
        {
            LocalPlayerGuard guard =
                switchers[i].GetComponent<LocalPlayerGuard>();
            if (guard != null && guard.IsLocalPlayer)
            {
                _liveSwitcher = switchers[i];
                break;
            }
        }

        if (_liveSwitcher == null && switchers.Length > 0)
        {
            _liveSwitcher = switchers[0];
        }

        Repaint();
    }

    private void ApplyPreview()
    {
        if (_liveSwitcher == null)
        {
            FindLiveSwitcher();
        }

        if (_liveSwitcher == null)
        {
            Debug.LogWarning("[VRHudPlacementWindow] 실행 중인 콕핏을 찾지 못했습니다.");
            return;
        }

        _liveSwitcher.PreviewVrHudPlacement(
            _distance,
            _scale,
            _localOffset,
            _localEulerAngles);
        _liveSwitcher.PreviewVrHudElementOffsets(
            _radarOffset,
            _hpOffset,
            _fuelOffset,
            _weaponOffset,
            _boosterOffset);
    }

    private void LoadFromPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CockpitPrefabPath);
        CockpitViewSwitcher switcher =
            prefab != null ? prefab.GetComponent<CockpitViewSwitcher>() : null;
        if (switcher == null)
        {
            Debug.LogError(
                $"[VRHudPlacementWindow] CockpitViewSwitcher를 찾지 못했습니다: " +
                CockpitPrefabPath);
            return;
        }

        SerializedObject serialized = new SerializedObject(switcher);
        _distance = serialized.FindProperty("_vrHudDistance").floatValue;
        _scale = serialized.FindProperty("_vrHudScale").floatValue;
        _localOffset = serialized.FindProperty("_vrHudLocalOffset").vector3Value;
        _localEulerAngles =
            serialized.FindProperty("_vrHudLocalEulerAngles").vector3Value;
        _radarOffset = serialized.FindProperty("_vrRadarOffset").vector2Value;
        _hpOffset = serialized.FindProperty("_vrHpOffset").vector2Value;
        _fuelOffset = serialized.FindProperty("_vrFuelOffset").vector2Value;
        _weaponOffset = serialized.FindProperty("_vrWeaponOffset").vector2Value;
        _boosterOffset = serialized.FindProperty("_vrBoosterOffset").vector2Value;
        Repaint();
    }

    private void SaveToPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CockpitPrefabPath);
        try
        {
            CockpitViewSwitcher switcher = root.GetComponent<CockpitViewSwitcher>();
            if (switcher == null)
            {
                Debug.LogError(
                    "[VRHudPlacementWindow] cockpit.prefab에 " +
                    "CockpitViewSwitcher가 없습니다.");
                return;
            }

            SerializedObject serialized = new SerializedObject(switcher);
            serialized.FindProperty("_vrHudDistance").floatValue = _distance;
            serialized.FindProperty("_vrHudScale").floatValue = _scale;
            serialized.FindProperty("_vrHudLocalOffset").vector3Value = _localOffset;
            serialized.FindProperty("_vrHudLocalEulerAngles").vector3Value =
                _localEulerAngles;
            serialized.FindProperty("_vrRadarOffset").vector2Value = _radarOffset;
            serialized.FindProperty("_vrHpOffset").vector2Value = _hpOffset;
            serialized.FindProperty("_vrFuelOffset").vector2Value = _fuelOffset;
            serialized.FindProperty("_vrWeaponOffset").vector2Value = _weaponOffset;
            serialized.FindProperty("_vrBoosterOffset").vector2Value = _boosterOffset;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, CockpitPrefabPath);
            Debug.Log(
                "[VRHudPlacementWindow] VR HUD 배치 값을 cockpit.prefab에 저장했습니다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif