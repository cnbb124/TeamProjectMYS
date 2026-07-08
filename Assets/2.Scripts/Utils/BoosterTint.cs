	using UnityEngine;

// 부스터 불꽃 색상을 아군/적군으로 구분해서 칠하는 스크립트.
// 부스터는 기체에 계속 붙어있으므로 미사일(MissileTrailTint)과 달리 매번 다시 칠할 필요 없이,
// 쉴드(ProceduralForceFieldOverlay)와 같은 방식으로 시작 시 부모에 Enemy 컴포넌트가 있는지 보고 1회 적용.
// 색칠 방식도 쉴드와 동일한 MaterialPropertyBlock — 머티리얼 원본은 공유 유지(복사본 없음, GPU 인스턴싱 유지).
// 부스터 프리팹(Main_boost, Thruster_Main, Sub-Boost, boost, Thruster_Rev 등) 루트에 붙이면 됨.
public class BoosterTint : MonoBehaviour
{
	[Tooltip("▶ 아군(플레이어) 부스터 불꽃 색상\n기본값은 원래 부스터에 박혀있던 하늘색(기존 모습 그대로)\n색약 참고 → 파란계열 권장")]
	[SerializeField] private Color _allyColor = new Color(0.745f, 0.941f, 1.00f, 1f);

	[Tooltip("▶ 적군(Enemy) 부스터 불꽃 색상\n부모에 Enemy 컴포넌트가 있으면 자동으로 이 색상 적용\n색약 참고 → 붉은계열 권장")]
	[SerializeField] private Color _enemyColor = new Color(1.00f, 0.25f, 0.10f, 1f);

	private static readonly string[] ColorNames = { "_Color", "_TintColor", "_BaseColor" };
	private MaterialPropertyBlock _propertyBlock;

	private void Awake()
	{
		ApplyTeamColor();
	}

	// 인스펙터에서 색 바꾸면 에디터에서도 즉시 반영 (쉴드의 OnValidate와 같은 역할)
	private void OnValidate()
	{
		ApplyTeamColor();
	}

	private void ApplyTeamColor()
	{
		bool isEnemy = GetComponentInParent<Enemy>() != null;
		Color targetColor = isEnemy ? _enemyColor : _allyColor;

		if (_propertyBlock == null)
			_propertyBlock = new MaterialPropertyBlock();

		// 이름에 "fire"가 들어간 파티클만 칠함 (fire_*, R_fire_*, D_fire_* 전부 포함).
		// smoke는 이름에 fire가 없어 자동 제외 — 연기는 회색 유지.
		ParticleSystemRenderer[] renderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
		for (int i = 0; i < renderers.Length; i++)
		{
			ParticleSystemRenderer renderer = renderers[i];
			if (renderer == null || !renderer.gameObject.name.Contains("fire")) continue;

			// 머티리얼 원본 알파(반투명)는 유지하고 RGB만 덮어씀.
			Material mat = renderer.sharedMaterial;
			if (mat == null) continue;

			// 이 머티리얼이 실제로 가진 색 슬롯 이름을 찾음 (없으면 건너뜀 → 에러 방지)
			string slot = null;
			for (int n = 0; n < ColorNames.Length; n++)
			{
				if (mat.HasProperty(ColorNames[n])) { slot = ColorNames[n]; break; }
			}
			if (slot == null) continue;
			// 머티리얼 원본 알파(반투명)는 유지하고 RGB만 덮어씀.
			renderer.GetPropertyBlock(_propertyBlock);
			float alpha = mat.GetColor(slot).a;
			_propertyBlock.SetColor(slot, new Color(targetColor.r, targetColor.g, targetColor.b, alpha));
			renderer.SetPropertyBlock(_propertyBlock);
		}
	}
}
