using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShieldController : MonoBehaviour
{
    public float shieldHp = 100f;

    public void TakeDamage(float damage)
    {
        shieldHp -= damage;
        
        if (shieldHp <= 0)
        {
            // 쉴드 비활성화
            gameObject.SetActive(false);
        }
    }
}