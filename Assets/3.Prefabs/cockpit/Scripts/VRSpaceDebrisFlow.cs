using Photon.Pun;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Reuses a small, fixed set of debris meshes around the local VR cockpit.
/// Debris is moved opposite to the ship velocity and wrapped inside a local
/// volume, so no Instantiate/Destroy calls occur while flying.
/// </summary>
[DisallowMultipleComponent]
public sealed class VRSpaceDebrisFlow : MonoBehaviour
{
    private sealed class DebrisItem
    {
        public Transform transform;
        public MeshRenderer renderer;
        public Vector3 angularVelocity;
    }

    [Header("Source Assets")]
    [SerializeField] private Mesh[] _meshes;
    [SerializeField] private Material[] _materials;

    [Header("Appearance")]
    [Tooltip("Cool blue-gray tint that remains readable against a dark space background.")]
    [SerializeField] private Color _debrisTint =
        new Color(0.24f, 0.32f, 0.46f, 0.2f);

    [Header("Pool")]
    [SerializeField, Min(1)] private int _itemCount = 24;
    [SerializeField] private Vector2 _scaleRange = new Vector2(0.12f, 0.35f);
    [SerializeField] private Vector2 _rotationSpeedRange = new Vector2(4f, 18f);

    [Header("Flow Volume")]
    [SerializeField] private Vector3 _volumeSize = new Vector3(56f, 34f, 90f);
    [SerializeField] private Vector3 _volumeCenter = new Vector3(0f, 0f, 34f);
    [SerializeField, Min(0f)] private float _flowMultiplier = 1f;
    [SerializeField, Min(0f)] private float _minimumVisibleSpeed = 1.5f;
    [SerializeField, Min(0f)] private float _fullDensitySpeed = 25f;

    [Header("VR Comfort")]
    [Tooltip("Keeps debris away from the center of the cockpit and the player's face.")]
    [SerializeField] private Vector3 _clearZone = new Vector3(5f, 3.5f, 14f);
    [SerializeField, Range(0f, 1f)] private float _minimumDensity = 0.2f;

    [Header("Desktop Preview")]
    [Tooltip("Shows the effect in cockpit view even when no XR device is connected.")]
    [SerializeField] private bool _previewInCockpitWithoutXR = true;

    private DebrisItem[] _items;
    private Material[] _runtimeMaterials;
    private Player _player;
    private Rigidbody _playerBody;
    private CockpitViewSwitcher _viewSwitcher;
    private bool _isLocalPlayer = true;
    private bool _poolBuilt;
    private bool _lastVisible;

    private void Awake()
    {
        PhotonView owner = GetComponentInParent<PhotonView>();
        _isLocalPlayer = owner == null || !PhotonNetwork.InRoom || owner.IsMine;
        if (!_isLocalPlayer)
        {
            enabled = false;
            return;
        }

        _viewSwitcher = GetComponent<CockpitViewSwitcher>();
        CachePlayer();
    }

    private void LateUpdate()
    {
        if (_player == null)
        {
            CachePlayer();
        }

        bool shouldShow =
            (XRRuntimeManager.IsRunning || _previewInCockpitWithoutXR) &&
            (_viewSwitcher == null || _viewSwitcher.IsCockpitView) &&
            _playerBody != null;

        if (!shouldShow)
        {
            if (_lastVisible)
            {
                SetRenderersVisible(false);
            }

            return;
        }

        if (!_poolBuilt)
        {
            BuildPool();
            if (!enabled || _items == null || _items.Length == 0)
            {
                return;
            }
        }

        if (!_lastVisible)
        {
            SetRenderersVisible(true);
        }

        Vector3 localVelocity =
            transform.InverseTransformDirection(_playerBody.velocity);
        float speed = localVelocity.magnitude;
        float density = Mathf.InverseLerp(
            _minimumVisibleSpeed,
            Mathf.Max(_minimumVisibleSpeed + 0.01f, _fullDensitySpeed),
            speed);
        density = Mathf.Lerp(_minimumDensity, 1f, density);

        Vector3 localDelta = -localVelocity * (_flowMultiplier * Time.deltaTime);
        for (int i = 0; i < _items.Length; i++)
        {
            DebrisItem item = _items[i];
            bool itemVisible =
                speed >= _minimumVisibleSpeed &&
                i < Mathf.CeilToInt(_items.Length * density);
            item.renderer.enabled = itemVisible;
            if (!itemVisible)
            {
                continue;
            }

            item.transform.localPosition =
                WrapPosition(item.transform.localPosition + localDelta);
            item.transform.Rotate(
                item.angularVelocity * Time.deltaTime,
                Space.Self);
        }
    }

    private void CachePlayer()
    {
        _player = GetComponentInParent<Player>();
        if (_player == null && GameManager.Instance != null)
        {
            _player = GameManager.Instance.playerRef;
        }

        _playerBody = _player != null
            ? _player.GetComponent<Rigidbody>()
            : null;
    }

    private void BuildPool()
    {
        _poolBuilt = true;
        if (_meshes == null || _meshes.Length == 0 ||
            _materials == null || _materials.Length == 0)
        {
            Debug.LogWarning(
                "[VRSpaceDebrisFlow] Debris mesh/material is not assigned.",
                this);
            _items = new DebrisItem[0];
            enabled = false;
            return;
        }

        _items = new DebrisItem[Mathf.Max(1, _itemCount)];
        _runtimeMaterials = CreateRuntimeMaterials();
        for (int i = 0; i < _items.Length; i++)
        {
            GameObject itemObject = new GameObject($"VR Debris {i:00}");
            Transform itemTransform = itemObject.transform;
            itemTransform.SetParent(transform, false);

            MeshFilter filter = itemObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _meshes[i % _meshes.Length];

            MeshRenderer meshRenderer = itemObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial =
                _runtimeMaterials[i % _runtimeMaterials.Length];
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;

            float scale = Random.Range(
                Mathf.Min(_scaleRange.x, _scaleRange.y),
                Mathf.Max(_scaleRange.x, _scaleRange.y));
            itemTransform.localScale = Vector3.one * scale;
            itemTransform.localRotation = Random.rotation;
            itemTransform.localPosition = RandomPosition();

            float rotationSpeed = Random.Range(
                Mathf.Min(_rotationSpeedRange.x, _rotationSpeedRange.y),
                Mathf.Max(_rotationSpeedRange.x, _rotationSpeedRange.y));
            _items[i] = new DebrisItem
            {
                transform = itemTransform,
                renderer = meshRenderer,
                angularVelocity = Random.onUnitSphere * rotationSpeed
            };
        }
    }

    private Material[] CreateRuntimeMaterials()
    {
        Material[] runtimeMaterials = new Material[_materials.Length];
        float visibleDistance =
            PositiveVolumeHalfSize().magnitude + _volumeCenter.magnitude;
        Shader debrisShader = Shader.Find("FORGE3D/Debris New");

        for (int i = 0; i < _materials.Length; i++)
        {
            Material runtimeMaterial = new Material(_materials[i])
            {
                name = $"{_materials[i].name} (VR Flow Runtime)"
            };

            // The original demo materials still point to the older shader,
            // which does not output their saved albedo texture. The package's
            // newer shader uses the same saved properties and is visible in
            // both the desktop preview and stereo rendering.
            if (debrisShader != null)
            {
                runtimeMaterial.shader = debrisShader;
            }

            if (runtimeMaterial.HasProperty("_FadeInAInBOutAOutB"))
            {
                runtimeMaterial.SetVector(
                    "_FadeInAInBOutAOutB",
                    new Vector4(
                        1f,
                        3f,
                        visibleDistance * 0.85f,
                        visibleDistance));
            }

            if (runtimeMaterial.HasProperty("_TintRGBAmbientAMult"))
            {
                runtimeMaterial.SetColor(
                    "_TintRGBAmbientAMult",
                    _debrisTint);
            }

            runtimeMaterial.enableInstancing = true;
            runtimeMaterials[i] = runtimeMaterial;
        }

        return runtimeMaterials;
    }

    private void OnDestroy()
    {
        if (_runtimeMaterials == null)
        {
            return;
        }

        for (int i = 0; i < _runtimeMaterials.Length; i++)
        {
            if (_runtimeMaterials[i] != null)
            {
                Destroy(_runtimeMaterials[i]);
            }
        }
    }

    private Vector3 RandomPosition()
    {
        Vector3 half = PositiveVolumeHalfSize();
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 position = _volumeCenter + new Vector3(
                Random.Range(-half.x, half.x),
                Random.Range(-half.y, half.y),
                Random.Range(-half.z, half.z));
            if (!IsInsideClearZone(position))
            {
                return position;
            }
        }

        return _volumeCenter + new Vector3(half.x, half.y, half.z);
    }

    private Vector3 WrapPosition(Vector3 position)
    {
        Vector3 half = PositiveVolumeHalfSize();
        Vector3 min = _volumeCenter - half;
        Vector3 size = half * 2f;

        position.x = WrapAxis(position.x, min.x, size.x);
        position.y = WrapAxis(position.y, min.y, size.y);
        position.z = WrapAxis(position.z, min.z, size.z);

        if (IsInsideClearZone(position))
        {
            position = RandomPosition();
        }

        return position;
    }

    private Vector3 PositiveVolumeHalfSize()
    {
        return new Vector3(
            Mathf.Max(1f, Mathf.Abs(_volumeSize.x)) * 0.5f,
            Mathf.Max(1f, Mathf.Abs(_volumeSize.y)) * 0.5f,
            Mathf.Max(1f, Mathf.Abs(_volumeSize.z)) * 0.5f);
    }

    private bool IsInsideClearZone(Vector3 position)
    {
        return Mathf.Abs(position.x) < Mathf.Abs(_clearZone.x) &&
               Mathf.Abs(position.y) < Mathf.Abs(_clearZone.y) &&
               position.z > -1f &&
               position.z < Mathf.Abs(_clearZone.z);
    }

    private static float WrapAxis(float value, float minimum, float size)
    {
        return minimum + Mathf.Repeat(value - minimum, size);
    }

    private void SetRenderersVisible(bool visible)
    {
        _lastVisible = visible;
        if (_items == null)
        {
            return;
        }

        for (int i = 0; i < _items.Length; i++)
        {
            _items[i].renderer.enabled = visible;
        }
    }
}
