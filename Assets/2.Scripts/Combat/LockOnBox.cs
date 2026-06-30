using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class LockOnBox : MonoBehaviour
{
    private BoxCollider _col;

	private void Awake()
	{
		_col = GetComponent<BoxCollider>();
		gameObject.layer = LayerMask.NameToLayer("LockOnBox");
		_col.isTrigger = true;
	}
	//락온필요한 대상에게 달고 콜라이더 트리거on
}
