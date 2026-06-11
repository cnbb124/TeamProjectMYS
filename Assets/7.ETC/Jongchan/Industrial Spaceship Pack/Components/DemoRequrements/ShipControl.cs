using UnityEngine;
using System.Collections;

public class ShipControl : MonoBehaviour {

	private Vector3 thrust;
	private Rigidbody rb;

	public float power = 100;

	// Use this for initialization
	void Start () {
		rb = GetComponent<Rigidbody>();
		Cursor.visible = false;
	}
	
	// Update is called once per frame
	void Update () {
		thrust = new Vector3(Input.GetAxis ("Horizontal")*Time.deltaTime * power,Input.GetAxis ("Mouse ScrollWheel")*Time.deltaTime *power*10 ,Input.GetAxis ("Vertical")*Time.deltaTime * power);
		rb.AddRelativeForce (thrust);
		if (Input.GetButton ("Fire1")) 
		{
			rb.velocity=new Vector3(0,0,0);
		}
	}
}
