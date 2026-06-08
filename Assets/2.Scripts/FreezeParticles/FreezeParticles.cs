using UnityEngine;
using System.Collections;

public class FreezeParticles : MonoBehaviour {

	private ParticleSystem part;

	// Use this for initialization
	void Start () {
		part = GetComponent<ParticleSystem> ();
		part.Pause() ;
	}
	
}
