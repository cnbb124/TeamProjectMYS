using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class UnitAnimCtrl : MonoBehaviour
{
	[Header("애니메이션 타입,해당클립")]
	public AnimTypeClip[] animTypeClips;
	
	private Animator animator;

	private AnimDictionary animDic = new AnimDictionary();

	private void Awake()
	{
		animator = GetComponent<Animator>();
	}
	// Start is called before the first frame update
	void Start()
	{
		//인스펙터에있는 타입,클립을 딕셔너리에 실제등록.
		//차후 애님클립=딕.Get으로 갖고와서 할것
		foreach (var entry in animTypeClips)
		{
			animDic.Add(entry.animType, entry.animClip);
		}
	}


	public void Play(ANIM_TYPE type)
	{
		AnimationClip clip = animDic.Get(type);
		if (clip == null)
		{
			return;
		}
		animator.CrossFade(clip.name, 0.1f);


	}
}

