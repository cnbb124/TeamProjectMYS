using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AffectionManager : MonoBehaviour
{
	private static AffectionManager instance = null;

	public static AffectionManager Instance
	{
		get
		{
			if (instance == null)
			{
				instance = FindObjectOfType<AffectionManager>();
				if (instance == null)
				{
					Debug.LogError("[AffectionManager] 씬에 AffectionManager 없음! 하이어라키에 추가 필요");
				}
				else
				{
					DontDestroyOnLoad(instance.gameObject);
				}
			}
			return instance;
		}

	}


	private void Awake()
	{
		if (instance == null)
		{
			instance = this;
			DontDestroyOnLoad(gameObject);
		}
		else
		{
			Debug.LogWarning("[AffectionManager] 중복 감지. 파괴 후 기존 유지");
			Destroy(gameObject);
		}
	}



	// Start is called before the first frame update
	void Start()
	{

	}

	// Update is called once per frame
	void Update()
	{

	}
}
