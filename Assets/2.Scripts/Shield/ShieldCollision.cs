using UnityEngine;
using ProceduralForceField;

public class ShieldCollision : MonoBehaviour
{
    [SerializeField] private ProceduralForceFieldOverlay _forceField;

    void OnCollisionEnter(Collision coll)
    {
        _forceField.Trigger(coll.contacts[0].point);
    }
}