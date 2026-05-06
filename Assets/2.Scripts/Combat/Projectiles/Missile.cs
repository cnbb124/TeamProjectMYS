using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Missile : Projectile
{
	protected override void Awake()
	{
		base.Awake();
		dmgType = DAMAGE_TYPE.EXPLOSION;
	}
	// Start is called before the first frame update
	protected override void Start()
   {
        
   }

	// Update is called once per frame
	protected override void Update()
    {
        
    }
}
