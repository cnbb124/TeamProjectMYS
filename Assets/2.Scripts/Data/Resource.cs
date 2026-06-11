using UnityEngine;

public class Resource : MonoBehaviour
{
    public ResourceData resourceData;
    public int amount = 1;

    public void Collect()
    {
        InventoryManager.Instance.AddItem(resourceData, amount);
        Destroy(gameObject);
    }
}