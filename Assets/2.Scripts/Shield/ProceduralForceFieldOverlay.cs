using UnityEngine;

namespace ProceduralForceField
{
    [DisallowMultipleComponent]
    public sealed class ProceduralForceFieldOverlay : MonoBehaviour
    {
        #region Properties
        [SerializeField] private ProceduralForceFieldHit _forceFieldHit;
        [SerializeField] private Renderer _overlayRenderer;

        [Header("Startup")]
        [SerializeField] private bool _startVisible = false;
        [SerializeField] private bool _autoHide = true;
        [SerializeField] private bool _disableRendererWhenHidden = true;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float _visibleSeconds = 2.5f;
        [SerializeField, Min(0.01f)] private float _fadeOutSeconds = 0.6f;

        [Header("Reveal Size")]

        [SerializeField, Min(0.01f)] private float _localRevealRadius = 0.5f;
        

        [Header("Sound Settings")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _impactClip;

        private MaterialPropertyBlock _propertyBlock;

        private float _hideAtTime;
        private float _visibility;
        private float _fadeOutStartTime;
        private bool _fadingOut;

        private static readonly int FieldVisibilityId  = Shader.PropertyToID("_FieldVisibility");
        private static readonly int RevealMaxDistanceId = Shader.PropertyToID("_RevealMaxDistance");
        private static readonly int DefaultVisibleId    = Shader.PropertyToID("_DefaultVisible");
        private static readonly int ActivationRevealId  = Shader.PropertyToID("_ActivationReveal");
        #endregion

        #region Initialization
        private void Awake()
        {
            CacheReferences();

            _propertyBlock = new MaterialPropertyBlock();

            _visibility = _startVisible ? 1.0f : 0.0f;
            _fadingOut  = false;

            if (_overlayRenderer != null)
            {
                _overlayRenderer.enabled = _startVisible || !_disableRendererWhenHidden;
            }

            ApplyProperties(_visibility);
        }
        #endregion

        private void Update()
        {
            if (_overlayRenderer == null) return;

            if (!_autoHide)
            {
                if (!_overlayRenderer.enabled)
                    _overlayRenderer.enabled = true;

                _visibility = 1.0f;
                _fadingOut  = false;
                ApplyProperties(_visibility);
                return;
            }

            if (_fadingOut)
            {
                float t = Mathf.Clamp01((Time.time - _fadeOutStartTime) / Mathf.Max(_fadeOutSeconds, 0.01f));
                _visibility = 1.0f - t;

                ApplyProperties(_visibility);

                if (_visibility <= 0.001f)
                {
                    _visibility = 0.0f;
                    _fadingOut  = false;

                    ApplyProperties(_visibility);

                    if (_disableRendererWhenHidden)
                        _overlayRenderer.enabled = false;
                }

                return;
            }

            if (_overlayRenderer.enabled && Time.time >= _hideAtTime)
            {
                _fadingOut        = true;
                _fadeOutStartTime = Time.time;
            }
        }

        #region Methods
        public void Trigger(Vector3 hitWorldPosition)
        {
            Debug.Log("Trigger 호출됨!");
            CacheReferences();

            if (_forceFieldHit == null || _overlayRenderer == null) return;

            // 사운드
            if (_audioSource != null && _impactClip != null)
                _audioSource.PlayOneShot(_impactClip);

            if (!_overlayRenderer.enabled)
                _overlayRenderer.enabled = true;

            _visibility = 1.0f;
            _fadingOut  = false;

            ApplyProperties(_visibility);

            _hideAtTime = Time.time + _visibleSeconds;

            _forceFieldHit.TriggerHit(hitWorldPosition);
        }

        private void CacheReferences()
        {
            if (_forceFieldHit == null)
                _forceFieldHit = GetComponentInChildren<ProceduralForceFieldHit>(true);

            if (_overlayRenderer == null && _forceFieldHit != null)
                _overlayRenderer = _forceFieldHit.GetComponent<Renderer>();
        }

        private void ApplyProperties(float visibility)
        {
            if (_overlayRenderer == null) return;

            _overlayRenderer.GetPropertyBlock(_propertyBlock);

            _propertyBlock.SetFloat(FieldVisibilityId, 0.0f);
            
            _propertyBlock.SetFloat(DefaultVisibleId,   0.0f);
            _propertyBlock.SetFloat(ActivationRevealId, 1.0f);

            
            _propertyBlock.SetFloat(RevealMaxDistanceId, _localRevealRadius);

            _overlayRenderer.SetPropertyBlock(_propertyBlock);
        }
        #endregion
    }
}