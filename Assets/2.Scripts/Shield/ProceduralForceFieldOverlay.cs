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
        [Tooltip("▶ 에디터 미리보기\n" +
            "체크 ON  → 게임 실행 전에도 씬 뷰에서 쉴드가 보임 (배치/크기 확인용)\n" +
            "체크 OFF → 에디터에서는 쉴드가 안 보임\n" +
            "※ 실제 게임 실행에는 영향 없음")]
        [SerializeField] private bool _previewInEditor = true;

        [Tooltip("▶ 게임 시작 시 쉴드 표시 여부\n" +
            "체크 ON  → 게임 시작하자마자 쉴드가 보임\n" +
            "체크 OFF → 처음엔 안 보이고 맞았을 때만 나타남 (권장)\n" +
            "※ 보통 OFF로 두는 게 자연스러움")]
        [SerializeField] private bool _startVisible = false;

        [Tooltip("▶ 피격 후 자동으로 사라지게 할지 여부\n" +
            "체크 ON  → 맞은 후 일정 시간이 지나면 쉴드가 서서히 사라짐 (권장)\n" +
            "체크 OFF → 한번 나타나면 계속 보임 (끄기 전까지)")]
        [SerializeField] private bool _autoHide = true;

        [Tooltip("▶ 쉴드가 숨겨질 때 렌더러 비활성화 여부\n" +
            "체크 ON  → 안 보일 때 렌더러를 꺼서 성능 절약 (권장)\n" +
            "체크 OFF → 렌더러를 계속 켜둠 (에디터 미리보기 필요할 때 OFF)")]
        [SerializeField] private bool _disableRendererWhenHidden = true;

        [Header("Timing")]
        [Tooltip("▶ 쉴드가 보이는 시간 (초)\n" +
            "피격 후 쉴드가 화면에 유지되는 시간\n" +
            "예) 1.5 → 1.5초 동안 보였다가 사라지기 시작\n" +
            "짧을수록 순간적으로 반짝, 길수록 오래 보임\n" +
            "권장값: 1.5 ~ 2.0")]
        [SerializeField, Min(0.01f)] private float _visibleSeconds = 1.5f;

        [Tooltip("▶ 쉴드가 사라질 때 페이드 아웃 시간 (초)\n" +
            "쉴드가 서서히 투명해지는 시간\n" +
            "예) 0.8 → 0.8초에 걸쳐 천천히 사라짐\n" +
            "0.1에 가까울수록 빠르게 뚝 끊김, 클수록 부드럽게 사라짐\n" +
            "권장값: 0.5 ~ 1.0")]
        [SerializeField, Min(0.01f)] private float _fadeOutSeconds = 0.6f;

        [Header("Reveal Size")]
        [Tooltip("▶ 피격 지점에서 쉴드가 드러나는 반경 (월드 단위)\n" +
            "총알이 맞은 곳 주변으로 쉴드가 보이는 크기\n" +
            "예) 1.5 → 맞은 지점 기준 반경 1.5 범위만큼 쉴드 표시\n" +
            "작으면 좁게, 크면 넓게 보임\n" +
            "쉴드 크기에 맞게 조절 권장값: 1.0 ~ 2.0")]
        [SerializeField, Min(0.01f)] private float _localRevealRadius = 0.5f;

        [Header("실드 색상/강도 설정")]
        [Tooltip("▶ 아군(플레이어) 쉴드 색상\n" +
            "이 오브젝트가 Enemy 컴포넌트가 없으면 자동으로 이 색상 적용\n" +
            "색약 참고 → 파란계열 권장: R:0.1 G:0.65 B:1.0\n" +
            "※ 색상 변경 후 바로 적용되려면 Preview In Editor 체크 확인")]
        [SerializeField] private Color _allyColor  = new Color(0.10f, 0.65f, 1.00f, 1f);

        [Tooltip("▶ 적군(Enemy) 쉴드 색상\n" +
            "이 오브젝트 부모에 Enemy 컴포넌트가 있으면 자동으로 이 색상 적용\n" +
            "색약 참고 → 붉은계열 권장: R:1.0 G:0.25 B:0.1\n" +
            "※ 색상 변경 후 바로 적용되려면 Preview In Editor 체크 확인")]
        [SerializeField] private Color _enemyColor = new Color(1.00f, 0.25f, 0.10f, 1f);

        [Tooltip("▶ 쉴드 전체 투명도\n" +
            "0   = 완전히 투명 (안 보임)\n" +
            "0.5 = 반투명\n" +
            "1   = 완전 불투명\n" +
            "너무 높으면 쉴드가 배경을 가림, 너무 낮으면 안 보임\n" +
            "권장값: 0.5 ~ 0.8")]
        [SerializeField, Range(0f, 1f)]  private float _opacity = 0.6f;

        [Tooltip("▶ 피격 시 이펙트 밝기\n" +
            "총알이 맞았을 때 나타나는 방사형 이펙트의 밝기\n" +
            "0  = 피격 이펙트 없음\n" +
            "2  = 적당한 밝기 (권장)\n" +
            "10 이상 = 너무 밝아져 쉴드 패턴이 가려지고 단색처럼 보일 수 있음\n" +
            "권장값: 1.5 ~ 3.0")]
        [SerializeField, Range(0f, 20f)] private float _hitIntensity = 3f;

        [Header("쉴드 파괴 이펙트")]
        [Tooltip("▶ 쉴드 파괴 시 생성할 Pulsewave 프리팹\n" +
            "VFXManager.vfxConfigs에 EFFECT_TYPE.VFX_SHIELD_PULSEWAVE로 등록된 프리팹 사용\n" +
            "비워두면 파괴 이펙트 없이 사라지기만 함")]
        [SerializeField] private bool _usePulsewave = true;

        [Tooltip("▶ 쉴드 HP가 낮아질 때 깜빡임 시작 비율\n" +
            "예) 0.3 → 쉴드 HP 30% 이하일 때 깜빡이기 시작\n" +
            "권장값: 0.3 ~ 0.5")]
        [SerializeField, Range(0f, 1f)] private float _flickerThreshold = 0.3f;

        [Tooltip("▶ 깜빡임 속도\n" +
            "값이 클수록 빠르게 깜빡임\n" +
            "권장값: 8 ~ 15")]
        [SerializeField] private float _flickerSpeed = 10f;

        private Color _activeColor;
        private MaterialPropertyBlock _propertyBlock;

        private float _hideAtTime;
        private float _visibility;
        private float _fadeOutStartTime;
        private bool  _fadingOut;

        // 쉴드 HP 비율 (0~1), Unit에서 설정
        private float _shieldHpRatio = 1f;
        private bool  _isDestroyed   = false;

        private static readonly int FieldVisibilityId   = Shader.PropertyToID("_FieldVisibility");
        private static readonly int RevealMaxDistanceId  = Shader.PropertyToID("_RevealMaxDistance");
        private static readonly int DefaultVisibleId     = Shader.PropertyToID("_DefaultVisible");
        private static readonly int ActivationRevealId   = Shader.PropertyToID("_ActivationReveal");
        private static readonly int ShieldTintColorId    = Shader.PropertyToID("_ShieldTintColor");
        private static readonly int OpacityId            = Shader.PropertyToID("_Opacity");
        private static readonly int HitIntensityId       = Shader.PropertyToID("_HitIntensity");
        private static readonly int HitDurationId        = Shader.PropertyToID("_HitDuration");
        private static readonly int RevealDurationId     = Shader.PropertyToID("_RevealDuration");
        #endregion

        #region Initialization
        private void Awake()
        {
            CacheReferences();
            _propertyBlock = new MaterialPropertyBlock();
            _visibility = _startVisible ? 1.0f : 0.0f;
            _fadingOut  = false;

            if (_overlayRenderer != null)
                _overlayRenderer.enabled = _startVisible || !_disableRendererWhenHidden;

            DetectAndApplyTeamColor();
        }

        // 인스펙터 값 변경 시 에디터/플레이 중 즉시 반영
        private void OnValidate()
        {
            CacheReferences();
            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            if (_overlayRenderer != null)
                _overlayRenderer.enabled = (!Application.isPlaying && _previewInEditor) || _startVisible || !_disableRendererWhenHidden;

            DetectAndApplyTeamColor();
        }
        #endregion

        private void Update()
        {
            if (_overlayRenderer == null) return;
            if (_isDestroyed) return;

            // HP 낮을 때 깜빡임 — opacity를 sin 파형으로 흔들기
            if (_shieldHpRatio <= _flickerThreshold && _shieldHpRatio > 0f && _overlayRenderer.enabled)
            {
                float flicker = 0.5f + 0.5f * Mathf.Sin(Time.time * _flickerSpeed * (1f - _shieldHpRatio + 0.1f));
                _propertyBlock ??= new MaterialPropertyBlock();
                _overlayRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(OpacityId, _opacity * flicker);
                _overlayRenderer.SetPropertyBlock(_propertyBlock);
            }

            if (!_autoHide)
            {
                if (!_overlayRenderer.enabled)
                    _overlayRenderer.enabled = true;

                _visibility = 1.0f;
                _fadingOut  = false;
                ApplyProperties();
                return;
            }

            if (_fadingOut)
            {
                float t = Mathf.Clamp01((Time.time - _fadeOutStartTime) / Mathf.Max(_fadeOutSeconds, 0.01f));
                _visibility = 1.0f - t;

                ApplyProperties();

                if (_visibility <= 0.001f)
                {
                    _visibility = 0.0f;
                    _fadingOut  = false;
                    ApplyProperties();

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
        // Unit에서 피격 시 호출 — 쉴드 HP 비율 전달 (0~1)
        public void UpdateShieldHP(float hpRatio)
        {
            _shieldHpRatio = Mathf.Clamp01(hpRatio);
        }

        // 쉴드 파괴 시 Unit에서 호출
        public void TriggerDestroy()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;

            // 쉴드 숨기기
            _visibility = 0f;
            _fadingOut  = false;
            ApplyProperties();
            if (_overlayRenderer != null)
                _overlayRenderer.enabled = false;

            // Pulsewave 스폰 — VFXManager 풀 사용 (자동반납은 EffectAutoReturn이 처리, PoolManager에는 미등록이라 작동 안 했었음)
            if (_usePulsewave && VFXManager.Instance != null)
            {
                GameObject wave = VFXManager.Instance.PlayEffectAtUnit(
                    EFFECT_TYPE.VFX_SHIELD_PULSEWAVE, transform, transform.position, Quaternion.identity);
                if (wave != null)
                {
                    // Halo / Halo_Glow 파티클 색상을 쉴드 색상(아군/적군)에 맞춰 동기화
                    ParticleSystem[] haloParticles = wave.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (ParticleSystem ps in haloParticles)
                    {
                        if (ps.gameObject.name.StartsWith("Halo"))
                        {
                            ParticleSystem.MainModule main = ps.main;
                            main.startColor = _activeColor;
                        }
                    }

                    // F3DPulsewave는 OnSpawned()(private) 호출해야 초기화됨
                    wave.BroadcastMessage("OnSpawned", SendMessageOptions.DontRequireReceiver);
                }
            }
        }

        // 쉴드 재생성 시 Unit에서 호출
        public void TriggerReset()
        {
            _isDestroyed   = false;
            _shieldHpRatio = 1f;
            _visibility    = 0f;
        }

        public void Trigger(Vector3 hitWorldPosition)
        {
            CacheReferences();

            if (_forceFieldHit == null || _overlayRenderer == null) return;

            if (!_overlayRenderer.enabled)
                _overlayRenderer.enabled = true;

            _visibility = 1.0f;
            _fadingOut  = false;

            ApplyProperties();

            _hideAtTime = Time.time + _visibleSeconds;

            _forceFieldHit.TriggerHit(hitWorldPosition);
        }

        // 부모에서 Enemy 여부 자동 감지 후 색상 적용
        private void DetectAndApplyTeamColor()
        {
            bool isEnemy = GetComponentInParent<Enemy>() != null;
            _activeColor = isEnemy ? _enemyColor : _allyColor;
            ApplyProperties();
        }

        private void CacheReferences()
        {
            if (_forceFieldHit == null)
                _forceFieldHit = GetComponentInChildren<ProceduralForceFieldHit>(true);

            if (_overlayRenderer == null && _forceFieldHit != null)
                _overlayRenderer = _forceFieldHit.GetComponent<Renderer>();
        }

        private void ApplyProperties()
        {
            if (_overlayRenderer == null) return;

            bool editorPreview = !Application.isPlaying && _previewInEditor;

            _overlayRenderer.GetPropertyBlock(_propertyBlock);

            _propertyBlock.SetFloat(FieldVisibilityId,   editorPreview ? 1.0f : 0.0f);
            _propertyBlock.SetFloat(DefaultVisibleId,    editorPreview ? 1.0f : 0.0f);
            _propertyBlock.SetFloat(ActivationRevealId,  1.0f);
            _propertyBlock.SetFloat(RevealMaxDistanceId, _localRevealRadius);
            _propertyBlock.SetColor(ShieldTintColorId,   _activeColor);
            _propertyBlock.SetFloat(OpacityId,           _opacity);
            _propertyBlock.SetFloat(HitIntensityId,      _hitIntensity);
            _propertyBlock.SetFloat(HitDurationId,       _visibleSeconds);
            _propertyBlock.SetFloat(RevealDurationId,    _visibleSeconds);

            _overlayRenderer.SetPropertyBlock(_propertyBlock);
        }
        #endregion
    }
}
