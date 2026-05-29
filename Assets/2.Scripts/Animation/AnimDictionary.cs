using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AnimTypeClip
{
	public ANIM_TYPE animType;
	public AnimationClip animClip;

	/*
	사용 예시 (인스펙터용 데이터):

	[SerializeField]
	AnimTypeClip[] animList;

	→ 인스펙터에서
	type = IDLE, clip = idleClip
	type = SHOT, clip = shotClip
	이렇게 넣어두는 용도
	*/
}




public class AnimDictionary
{
    private Dictionary<ANIM_TYPE, AnimationClip> dict 
        = new Dictionary<ANIM_TYPE, AnimationClip>();

    //Add(해당하는 애니메이션타입, 클립)
    public void Add(ANIM_TYPE type, AnimationClip clip)
    {
        if (clip == null)
        {
            return;
        }
       
         dict[type] = clip;
		// 덮어쓰기 허용

		/*
	   사용 예시:

	   animDict.Add(ANIM_TYPE.IDLE, idleClip);
	   animDict.Add(ANIM_TYPE.SHOT, shotClip);

	   → 해당 타입에 맞는 클립을 등록
	   */
	}

    //프로퍼티(겟터,재생용)
	public AnimationClip Get(ANIM_TYPE type)
    {
        dict.TryGetValue(type, out var clip);
        return clip;

		/*
        사용 예시:

        AnimationClip shot = animDict.Get(ANIM_TYPE.SHOT);

        if (shot != null)
        {
            _anim.CrossFade(shot.name);        // 레거시
            // animator.Play(shot.name);      // Animator
        }

        → 타입으로 클립을 꺼내서 재생
        */
	}

    //사전초기화
	public void Clear()
    {
        dict.Clear();
    }
}
