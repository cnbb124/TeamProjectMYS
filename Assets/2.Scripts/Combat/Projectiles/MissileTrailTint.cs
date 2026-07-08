using UnityEngine;

// 미사일 트레일 불꽃 색상을 아군/적군으로 구분해서 칠하는 스크립트.
// 아군/적군 미사일이 프리팹/풀을 공유하기 때문에(프리팹 자체는 색 고정 불가),
// 발사할 때마다 Missile.Init()에서 ApplyTeamColor()를 호출해 색을 다시 세팅함.
// 방식은 쉴드(ProceduralForceFieldOverlay)와 동일하게 MaterialPropertyBlock 사용 —
// 머티리얼 파일은 그대로 공유하고 렌더러별 색상만 오버라이드(GPU 인스턴싱 유지).
public class MissileTrailTint : MonoBehaviour
{
	[Tooltip("▶ 아군(플레이어) 미사일 트레일 색상\n색약 참고 → 파란계열 권장")]
	[SerializeField] private Color _allyColor = new Color(0.10f, 0.65f, 1.00f, 1f);

	[Tooltip("▶ 적군(Enemy) 미사일 트레일 색상\n색약 참고 → 붉은계열 권장")]
	[SerializeField] private Color _enemyColor = new Color(1.00f, 0.25f, 0.10f, 1f);

	private static readonly int ColorId = Shader.PropertyToID("_Color");

	private ParticleSystemRenderer[] _fireRenderers;
	private MaterialPropertyBlock _propertyBlock;

	private void Awake()
	{
		CacheFireRenderers();
		_propertyBlock = new MaterialPropertyBlock();
	}

	// 이름이 "fire"로 시작하는 파티클만 대상으로 함 (smoke는 회색 그대로 유지).
	private void CacheFireRenderers()
	{
		ParticleSystemRenderer[] allRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
		int count = 0;
		for (int i = 0; i < allRenderers.Length; i++)
		{
			if (allRenderers[i].gameObject.name.StartsWith("fire"))
				count++;
		}

		_fireRenderers = new ParticleSystemRenderer[count];
		int index = 0;
		for (int i = 0; i < allRenderers.Length; i++)
		{
			if (allRenderers[i].gameObject.name.StartsWith("fire"))
				_fireRenderers[index++] = allRenderers[i];
		}
	}

	/// <summary>발사할 때마다 호출 — isEnemy에 따라 트레일 색을 다시 세팅.</summary>
	public void ApplyTeamColor(bool isEnemy)
	{
		if (_fireRenderers == null)
			CacheFireRenderers();
		if (_propertyBlock == null)
			_propertyBlock = new MaterialPropertyBlock();

		Color targetColor = isEnemy ? _enemyColor : _allyColor;

		for (int i = 0; i < _fireRenderers.Length; i++)
		{
			ParticleSystemRenderer renderer = _fireRenderers[i];
			if (renderer == null) continue;

			// 머티리얼 원본 알파(반투명)는 유지하고 RGB만 덮어씀.
			renderer.GetPropertyBlock(_propertyBlock);
			Color materialColor = renderer.sharedMaterial != null ? renderer.sharedMaterial.color : Color.white;
			_propertyBlock.SetColor(ColorId, new Color(targetColor.r, targetColor.g, targetColor.b, materialColor.a));
			renderer.SetPropertyBlock(_propertyBlock);
		}
	}
}
