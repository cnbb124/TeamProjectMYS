using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//모든 키입력, 마우스입력 담당
public class InputManager : MonoBehaviour
{
	private static InputManager instance = null;
	public static InputManager Instance
	{
		get
		{
			if (instance == null)
			{ 
				instance = FindObjectOfType<InputManager>();
				if (instance == null)
				{
					Debug.LogError("씬에 InputManager 누락! 하이어라키에 인풋매니저필요 필요");
				}
			}
			return instance;
		}
	}
	public Vector3 MoveInput;
    public Vector2 LookInput;

	private void Awake()
	{
		// 싱글톤 기본 세팅 (씬이 넘어가도 파괴되지 않게 유지)
		if (instance == null)
		{
			instance = this;
			DontDestroyOnLoad(gameObject);

		}
		else if (instance != this)
		{

			Debug.LogWarning("중복된 InputManager 발견. 파괴 후 실행");
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
