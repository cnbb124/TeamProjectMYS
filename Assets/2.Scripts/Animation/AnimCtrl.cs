using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimCtrl : MonoBehaviour
{
	[Header("애니메이션 타입,해당클립")]
	public AnimTypeClip[] animTypeClips;
	
	private Animator _animator;

	private AnimDictionary _animDic = new AnimDictionary();

	private void Awake()
	{
		_animator = GetComponent<Animator>();
	}
	// Start is called before the first frame update
	void Start()
	{
		//인스펙터에있는 타입,클립을 딕셔너리에 실제등록.
		//차후 애님클립=딕.Get으로 갖고와서 할것
		foreach (var entry in animTypeClips)
		{
			_animDic.Add(entry.animType, entry.animClip);
		}
	}


	public void Play(ANIM_TYPE type, float duration = 0.1f)
	{
		AnimationClip clip = _animDic.Get(type);
		if (clip == null)
		{
			return;
		}
		_animator.CrossFade(clip.name, duration);


	}
}

