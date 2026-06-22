using UnityEngine;

namespace ProceduralForceField
{
    [DisallowMultipleComponent]
    public sealed class ProceduralForceFieldHit : MonoBehaviour
    {
        #region Properties
        [SerializeField] private Renderer _targetRenderer;

        [Header("Hit Settings")]
        [Tooltip("▶ 피격 이펙트 전체 배율\n" +
            "피격 시 나타나는 방사형 이펙트와 쉴드 표면 찌그러짐 두 가지를 동시에 조절\n" +
            "0   = 피격 이펙트 완전히 없음\n" +
            "1   = 기본값 (권장)\n" +
            "2   = 두 배 강도\n" +
            "※ 이펙트 밝기만 따로 조절하고 싶으면\n" +
            "  같은 오브젝트의 Overlay 컴포넌트 → Hit Intensity 값을 사용할 것\n" +
            "※ 이 값과 Hit Intensity가 곱해지므로 둘 다 높이면 너무 밝아질 수 있음")]
        [SerializeField, Min(0f)] private float _hitStrength = 1.0f;

        private MaterialPropertyBlock _propertyBlock;

        // 최대 3개 피격 동시 표시
        private const int MAX_HITS = 12;
        private Vector3[] _hitPositions = new Vector3[MAX_HITS];
        private float[]   _hitTimes     = new float[MAX_HITS];
        private int       _hitWriteIdx  = 0;

        private static readonly int HitPositionId   = Shader.PropertyToID("_HitPosition");
        private static readonly int HitTimeId       = Shader.PropertyToID("_HitTime");
        private static readonly int HitStrengthId   = Shader.PropertyToID("_HitStrength");
        private static readonly int BoundsRadiusWsId = Shader.PropertyToID("_BoundsRadiusWS");
        private static readonly int HitPositionOsId = Shader.PropertyToID("_HitPositionOS");
        private static readonly int BoundsExtentsOsId = Shader.PropertyToID("_BoundsExtentsOS");

        private static readonly int HitPosition2Id  = Shader.PropertyToID("_HitPosition2");
        private static readonly int HitTime2Id      = Shader.PropertyToID("_HitTime2");
        private static readonly int HitPositionOs2Id = Shader.PropertyToID("_HitPositionOS2");

        private static readonly int HitPosition3Id  = Shader.PropertyToID("_HitPosition3");
        private static readonly int HitTime3Id      = Shader.PropertyToID("_HitTime3");
        private static readonly int HitPositionOs3Id = Shader.PropertyToID("_HitPositionOS3");
        #endregion

        #region Initialization
        private void Awake()
        {
            if (_targetRenderer == null)
                _targetRenderer = GetComponent<Renderer>();

            _propertyBlock = new MaterialPropertyBlock();

            for (int i = 0; i < MAX_HITS; i++)
                _hitTimes[i] = -9999f;
        }
        #endregion

        #region Methods
        public void TriggerHit(Vector3 worldPosition)
        {
            if (_targetRenderer == null) return;

            // 히트 위치를 타원체 실드 메쉬 표면으로 투영
            Vector3 hitOs    = _targetRenderer.transform.InverseTransformPoint(worldPosition);
            Vector3 extentsOs = _targetRenderer.localBounds.extents;
            extentsOs.x = Mathf.Max(0.0001f, extentsOs.x);
            extentsOs.y = Mathf.Max(0.0001f, extentsOs.y);
            extentsOs.z = Mathf.Max(0.0001f, extentsOs.z);

            Vector3 unitVec = new Vector3(hitOs.x / extentsOs.x, hitOs.y / extentsOs.y, hitOs.z / extentsOs.z);
            if (unitVec == Vector3.zero) unitVec = Vector3.up;
            unitVec = unitVec.normalized;

            Vector3 surfaceOs = new Vector3(unitVec.x * extentsOs.x, unitVec.y * extentsOs.y, unitVec.z * extentsOs.z);
            Vector3 projectedWS = _targetRenderer.transform.TransformPoint(surfaceOs);

            _hitPositions[_hitWriteIdx] = projectedWS;
            _hitTimes[_hitWriteIdx]     = Time.timeSinceLevelLoad;
            _hitWriteIdx = (_hitWriteIdx + 1) % MAX_HITS;

            ApplyAllHits();
        }

        private void ApplyAllHits()
        {
            if (_targetRenderer == null) return;

            Bounds localBounds = _targetRenderer.localBounds;
            Vector3 extentsOs  = localBounds.extents;
            extentsOs.x = Mathf.Max(0.0001f, extentsOs.x);
            extentsOs.y = Mathf.Max(0.0001f, extentsOs.y);
            extentsOs.z = Mathf.Max(0.0001f, extentsOs.z);
            float boundsRadiusWs = Mathf.Max(0.0001f, _targetRenderer.bounds.extents.magnitude);

            _targetRenderer.GetPropertyBlock(_propertyBlock);

            _propertyBlock.SetFloat(BoundsRadiusWsId, boundsRadiusWs);
            _propertyBlock.SetVector(BoundsExtentsOsId, new Vector4(extentsOs.x, extentsOs.y, extentsOs.z, 0f));
            _propertyBlock.SetFloat(HitStrengthId, _hitStrength);

            SetHitSlot(0, HitPositionId,  HitTimeId,  HitPositionOsId,  extentsOs);
            SetHitSlot(1, HitPosition2Id, HitTime2Id, HitPositionOs2Id, extentsOs);
            SetHitSlot(2, HitPosition3Id, HitTime3Id, HitPositionOs3Id, extentsOs);

            _targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void SetHitSlot(int slot, int posId, int timeId, int posOsId, Vector3 extentsOs)
        {
            int idx = ((_hitWriteIdx - 1 - slot) % MAX_HITS + MAX_HITS) % MAX_HITS;
            Vector3 wp   = _hitPositions[idx];
            float   time = _hitTimes[idx];
            Vector3 hitOs = _targetRenderer.transform.InverseTransformPoint(wp);

            _propertyBlock.SetVector(posId,   new Vector4(wp.x, wp.y, wp.z, 1f));
            _propertyBlock.SetFloat(timeId,   time);
            _propertyBlock.SetVector(posOsId, new Vector4(hitOs.x, hitOs.y, hitOs.z, 1f));
        }

        public void SetHitStrength(float strength)
        {
            _hitStrength = Mathf.Max(0.0f, strength);
        }
        #endregion
    }
}
