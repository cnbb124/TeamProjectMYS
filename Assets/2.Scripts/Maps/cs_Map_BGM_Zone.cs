using System.Collections;
using UnityEngine;

public class cs_Map_BGM_Zone : MonoBehaviour
{
    public SOUND_TYPE bgmType; // 사운드 연결 추가되면 살릴것
    public float fadeTime = 2f;
    public AudioClip temp_Audio;
    public AudioSource tempAudioSource;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            StartCoroutine(CrossFade());
            // StartCoroutine(Temp_CrossFade());
        }
    }

    IEnumerator CrossFade()
    {
        float startVolume = SoundManager.Instance.bgmVolume;

        // 페이드 아웃
        for (float t = 0; t < fadeTime; t += Time.deltaTime)
        {
            SoundManager.Instance.SetBGMVolume(Mathf.Lerp(startVolume, 0, t / fadeTime));
            yield return null;
        }
        SoundManager.Instance.SetBGMVolume(0f);

        SoundManager.Instance.PlayBGM(bgmType); // 완전히 꺼진 다음에 교체

        // 페이드 인
        for (float t = 0; t < fadeTime; t += Time.deltaTime)
        {
            SoundManager.Instance.SetBGMVolume(Mathf.Lerp(0, startVolume, t / fadeTime));
            yield return null;
        }
        SoundManager.Instance.SetBGMVolume(startVolume);
    }

    //    IEnumerator Temp_CrossFade()
    //{
    //    float startVolume = 1f;
    //    // 페이드 아웃
    
    //    for (float t = 0; t < fadeTime; t += Time.deltaTime)
    //    {
    //        tempAudioSource.volume = Mathf.Lerp(startVolume, 0, t / fadeTime);
    //        yield return null;
    //    }

    //    tempAudioSource.clip = temp_Audio;
    //    tempAudioSource.Play();

    //    // 페이드 인
    //    for (float t = 0; t < fadeTime; t += Time.deltaTime)
    //    {
    //        tempAudioSource.volume = Mathf.Lerp(0, startVolume, t / fadeTime);
    //        yield return null;
    //    }
    //}
}