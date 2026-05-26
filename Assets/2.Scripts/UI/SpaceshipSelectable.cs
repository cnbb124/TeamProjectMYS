using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceshipSelectable : MonoBehaviour
{
    public PlaneNodeData shipData;

    void OnMouseDown()
    {
        HangarUIManager.Instance.OpenHangar(shipData);
    }
}
