using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestMoving : MonoBehaviour
{
    private Rigidbody _rb;
    private int dir = 1;
    public float baseSpeed = 50f;
    public float maxSpeed =800f;
    public float curSpeed;
    // Start is called before the first frame update
    void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        curSpeed = _rb.velocity.magnitude;
    }
    private void FixedUpdate()
    {

        _rb.AddForce(dir * baseSpeed, 0, 0, ForceMode.Acceleration);
        if (gameObject.transform.position.x > 2000f)
        {
            dir = -1;
        }
        else if (gameObject.transform.position.x < 0f)
        {
            dir = 1;
        }

        float clampX = Mathf.Clamp(_rb.velocity.x, -maxSpeed, maxSpeed);
        _rb.velocity = new Vector3(clampX, 0f, 0f);

        //if (rb.velocity.magnitude > 400f)
        //{
        //    // 순수 방향(normalized)에 최대 속력(400)을 곱해줍니다.
        //    // 왼쪽(-1)으로 가고 있었다면 -400이 됩니다.
        //    rb.velocity = rb.velocity.normalized * 400f;
        //}

    }
}
