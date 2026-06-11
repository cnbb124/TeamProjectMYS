using UnityEngine;

public class SetShipColors : MonoBehaviour
{
	public Color lightColor = Color.white;

	private Renderer[] meshRen;

	public void Start ()
	{
		meshRen = gameObject.GetComponentsInChildren<Renderer>();
		foreach (Renderer r in meshRen) {
						r.material.color = lightColor;
				}

		//ParticleAnimator[] animators = GetComponentsInChildren<ParticleAnimator>(true);
		//foreach (ParticleAnimator p in animators) p.colorAnimation = thrusters;

		//Light[] lights = GetComponentsInChildren<Light>(true);
		//foreach (Light l in lights) { l.color = lightColor; l.intensity = intensity; }
	}
}