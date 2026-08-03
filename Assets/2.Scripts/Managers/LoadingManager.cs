/*
 * [LoadingManager]
 * 비동기 씬 전환 로딩 화면 관리자.
 *
 * [사용법]
 * 1. LoadingScene에 빈 오브젝트 생성 후 이 스크립트 부착
 * 2. backgroundImages에 로딩 중 순차 표시할 이미지 스프라이트 등록 (순서대로)
 * 3. 씬 전환 전 LoadingManager.NextScene = "씬이름" 지정 후 LoadScene("LoadingScene") 호출
 */

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LoadingManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image    backgroundA;   // 배경 표시용
    //[SerializeField] private Image    backgroundB;   // 크로스페이드용 이미지
    //[SerializeField] private Image    backgroundC;   // 마지막 이미지
    [SerializeField] private Image    fillBar;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text tipText;

    [Header("배경 이미지 (순서대로)")]
    [SerializeField] private Sprite[] backgroundImages;

    [Header("설정")]
    [SerializeField] private float minLoadTime   = 6f;   // 최소 로딩 시간 (이미지 3장 볼 시간)
    //[SerializeField] private float fadeDuration  = 1f;   // 이미지 전환 페이드 시간
    [SerializeField] private string[] tips;

    public static string NextScene = "GameScene";

    private void Start()
    {
        if (backgroundA != null && backgroundImages != null && backgroundImages.Length > 0)
        {
            backgroundA.sprite = backgroundImages[Random.Range(0, backgroundImages.Length)];
            backgroundA.color  = Color.white;
        }

        if (tipText != null && tips != null && tips.Length > 0)
            tipText.text = tips[Random.Range(0, tips.Length)];

        StartCoroutine(LoadAsync());
    }

    private IEnumerator LoadAsync()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(NextScene);
        op.allowSceneActivation = false;

        float elapsed = 0f;
        float displayProgress = 0f;

        while (!op.isDone)
        {
            elapsed += Time.deltaTime;

            float targetProgress  = Mathf.Clamp01(op.progress / 0.9f);
            float timeProgress    = Mathf.Clamp01(elapsed / minLoadTime);
            float finalProgress   = Mathf.Min(targetProgress, timeProgress);

            displayProgress = Mathf.Lerp(displayProgress, finalProgress, Time.deltaTime * 5f);

            if (fillBar      != null) fillBar.fillAmount = displayProgress;
            if (progressText != null) progressText.text  = $"{Mathf.RoundToInt(displayProgress * 100f)}%";

            if (op.progress >= 0.9f && elapsed >= minLoadTime)
                op.allowSceneActivation = true;

            yield return null;
        }
    }

    // A→B 크로스페이드 후 A/B 역할 교체
    //private IEnumerator CrossFade(Sprite nextSprite)
    //{
    //    backgroundB.sprite = nextSprite;

    //    float t = 0f;
    //    while (t < fadeDuration)
    //    {
    //        t += Time.deltaTime;
    //        float alpha = Mathf.Clamp01(t / fadeDuration);
    //        backgroundB.color = new Color(1f, 1f, 1f, alpha);
    //        backgroundA.color = new Color(1f, 1f, 1f, 1f - alpha);
    //        yield return null;
    //    }

    //    // 역할 교체
    //    backgroundA.sprite = nextSprite;
    //    backgroundA.color  = Color.white;
    //    backgroundB.color  = new Color(1f, 1f, 1f, 0f);
    //}
}