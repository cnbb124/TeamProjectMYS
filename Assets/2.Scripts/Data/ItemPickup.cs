using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public ResourceData resourceData;
    public int amount = 1;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            InventoryManager.Instance.AddItem(resourceData, amount);
            Debug.Log($"ÀÚ¿ø È¹µæ! ÇöÀç º¸À¯: {InventoryManager.Instance.GetCount(resourceData)}");
            PoolManager.Instance.Return(gameObject);
        }
    }
}