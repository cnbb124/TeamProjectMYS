using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AnimTypeClip
{
	public ANIM_TYPE animType;
	public AnimationClip animClip;

	/*
	사용 예시 (인스펙터 배열):

	[SerializeField]
	AnimTypeClip[] animList;

	인스펙터에서
	type = IDLE,         clip = idleClip
	type = SHOOT_BULLET, clip = shootBulletClip
	type = DODGE,        clip = dodgeClip
	이렇게 채워넣는 용도
	*/
}


public class AnimDictionary
{
    private Dictionary<ANIM_TYPE, AnimationClip> _dict = new Dictionary<ANIM_TYPE, AnimationClip>();

    // Add(해당하는 애니메이션타입, 클립)
    public void Add(ANIM_TYPE type, AnimationClip clip)
    {
        if (clip == null)
        {
            return;
        }

        _dict[type] = clip;

		/*
	    사용 예시:

	    animDict.Add(ANIM_TYPE.IDLE,         idleClip);
	    animDict.Add(ANIM_TYPE.SHOOT_BULLET, shootBulletClip);

	    해당 타입에 맞는 클립을 등록
	    */
	}

    /// <summary>
    /// 겟터. 애니메이션클립에 접근. 사용 시 null 체크 필수
    /// </summary>
    public AnimationClip Get(ANIM_TYPE type)
    {
        _dict.TryGetValue(type, out var clip);
        return clip; 

		/*
        사용 예시 (AnimCtrl.Play 내부):

        AnimationClip clip = animDict.Get(ANIM_TYPE.SHOOT_BULLET);

        if (clip != null)
        {
            animator.CrossFade(clip.name, 0.1f);  // Animator 컴포넌트 (모던)
        }

        딕셔너리에서 클립 꺼내서 재생
        */
	}

    // 전체 초기화
	public void Clear()
    {
        _dict.Clear();
    }
}
