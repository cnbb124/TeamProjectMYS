#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
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
    private Vector3 _radarRotation = Vector3.zero;
    private Vector3 _hpRotation = Vector3.zero;
    private Vector3 _fuelRotation = Vector3.zero;
    private Vector3 _weaponRotation = Vector3.zero;
    private Vector3 _boosterRotation = Vector3.zero;

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

        if (Application.isPlaying && _liveSwitcher != null)
        {
            EditorGUILayout.HelpBox(
                _liveSwitcher.GetVrHudElementStatus(),
                MessageType.None);
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
        _hpOffset = EditorGUILayout.Vector2Field("HP (PanelHUD)", _hpOffset);
        _fuelOffset = EditorGUILayout.Vector2Field("Fuel (FuelGageIndicator)", _fuelOffset);
        _weaponOffset = EditorGUILayout.Vector2Field("Weapon (AmmoUI)", _weaponOffset);
        _boosterOffset = EditorGUILayout.Vector2Field("Booster", _boosterOffset);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Individual Rotations (degrees)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Z rotates within the HUD plane. X and Y tilt the panel in 3D.",
            MessageType.None);
        _radarRotation = EditorGUILayout.Vector3Field("Radar Rotation", _radarRotation);
        _hpRotation = EditorGUILayout.Vector3Field("HP Rotation", _hpRotation);
        _fuelRotation = EditorGUILayout.Vector3Field("Fuel Rotation", _fuelRotation);
        _weaponRotation = EditorGUILayout.Vector3Field("Weapon Rotation", _weaponRotation);
        _boosterRotation = EditorGUILayout.Vector3Field("Booster Rotation", _boosterRotation);

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
            _radarRotation = Vector3.zero;
            _hpRotation = Vector3.zero;
            _fuelRotation = Vector3.zero;
            _weaponRotation = Vector3.zero;
            _boosterRotation = Vector3.zero;
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
        _liveSwitcher.PreviewVrHudElementRotations(
            _radarRotation,
            _hpRotation,
            _fuelRotation,
            _weaponRotation,
            _boosterRotation);

        if (EditorApplication.isPaused)
        {
            // A paused player does not reliably run the normal Canvas/layout and
            // Game-view repaint cycle. Rebuild the layout first, then reapply the
            // element overrides because a layout component may have replaced them.
            // QueuePlayerLoopUpdate refreshes the view without stepping gameplay.
            Canvas.ForceUpdateCanvases();
            _liveSwitcher.PreviewVrHudElementOffsets(
                _radarOffset,
                _hpOffset,
                _fuelOffset,
                _weaponOffset,
                _boosterOffset);
            _liveSwitcher.PreviewVrHudElementRotations(
                _radarRotation,
                _hpRotation,
                _fuelRotation,
                _weaponRotation,
                _boosterRotation);
            EditorApplication.QueuePlayerLoopUpdate();
            InternalEditorUtility.RepaintAllViews();
        }
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
        _radarRotation = serialized.FindProperty("_vrRadarRotation").vector3Value;
        _hpRotation = serialized.FindProperty("_vrHpRotation").vector3Value;
        _fuelRotation = serialized.FindProperty("_vrFuelRotation").vector3Value;
        _weaponRotation = serialized.FindProperty("_vrWeaponRotation").vector3Value;
        _boosterRotation = serialized.FindProperty("_vrBoosterRotation").vector3Value;
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
            serialized.FindProperty("_vrRadarRotation").vector3Value = _radarRotation;
            serialized.FindProperty("_vrHpRotation").vector3Value = _hpRotation;
            serialized.FindProperty("_vrFuelRotation").vector3Value = _fuelRotation;
            serialized.FindProperty("_vrWeaponRotation").vector3Value = _weaponRotation;
            serialized.FindProperty("_vrBoosterRotation").vector3Value = _boosterRotation;
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
