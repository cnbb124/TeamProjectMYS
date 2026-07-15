/*
 * [StartSceneManager]
 * 게임 시작 로고 스플래시 화면 (TestStartScene).
 * 로고들을 순서대로 페이드 인 → 유지 → 페이드 아웃 하고, 끝나면 다음 씬으로 전환.
 * 아무 키/클릭으로 현재 로고 스킵 가능.
 *
 * [씬 구성 예시]
 * TestStartScene
 * └ Canvas
 *    ├ Logo_Team (Image + CanvasGroup, 알파 0으로 시작)   ← logos[0]
 *    └ Logo_Game (Image + CanvasGroup, 알파 0으로 시작)   ← logos[1]
 * └ StartSceneManager (빈 오브젝트 + 이 스크립트)
 *
 * [인스펙터 연결]
 * - logos         : 순서대로 보여줄 로고들의 CanvasGroup (각 로고 오브젝트에 CanvasGroup 추가)
 * - fadeDuration  : 페이드 인/아웃 시간
 * - holdDuration  : 로고 완전히 보인 상태 유지 시간
 * - nextSceneName : 스플래시 끝나면 갈 씬 (로비/타이틀). 비우면 전환 안 함(테스트용)
 * - useLoading    : 체크 시 LoadingManager(로딩 씬) 경유해서 전환
 * - allowSkip     : 아무 키/클릭으로 현재 로고 넘기기 허용
 */

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using WebSocketSharp;

public class StartSceneManager : MonoBehaviour
{
    [Header("로고 (순서대로 재생)")]
    [SerializeField] private CanvasGroup[] logos;

    [Header("타이밍")]
    [SerializeField] private float fadeDuration = 1f;   // 페이드 인/아웃 시간
    [SerializeField] private float holdDuration = 1.5f; // 로고 유지 시간

    [Header("다음 씬")]
    [SerializeField] private SCENE_TYPE nextSceneType ; // 비우면 전환 안 함 (테스트용)
    [SerializeField] private bool   useLoading    = false; // 로딩 씬 경유 여부

    [Header("스킵")]
    [SerializeField] private bool allowSkip = true;

    private bool _skipRequested;

    private void Start()
    {
        // 시작 시 전부 투명하게
        if (logos != null)
            foreach (CanvasGroup cg in logos)
                if (cg != null) cg.alpha = 0f;

        StartCoroutine(PlaySplash());
    }

    private void Update()
    {
        // 아무 키/클릭 = 현재 로고 스킵 요청
        if (allowSkip && Input.anyKeyDown)
            _skipRequested = true;
    }

    private IEnumerator PlaySplash()
    {
        if (logos != null)
        {
            foreach (CanvasGroup logo in logos)
            {
                if (logo == null) continue;
                yield return ShowLogo(logo);
            }
        }

        GoNextScene();
    }

    // 로고 하나: 페이드 인 → 유지 → 페이드 아웃 (스킵 시 즉시 다음 단계)
    private IEnumerator ShowLogo(CanvasGroup logo)
    {
        _skipRequested = false;

        // 페이드 인
        yield return Fade(logo, 0f, 1f);

        // 유지
        float elapsed = 0f;
        while (elapsed < holdDuration && !_skipRequested)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 페이드 아웃
        yield return Fade(logo, logo.alpha, 0f);
    }

    private IEnumerator Fade(CanvasGroup cg, float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            if (_skipRequested && to > from)   // 페이드 인 중 스킵 → 즉시 완전 표시
            {
                cg.alpha = to;
                yield break;
            }

            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }
        cg.alpha = to;
    }

    private void GoNextScene()
    {
        if (string.IsNullOrEmpty(nextSceneType.ToString()))
        {
            Debug.Log("[StartScene] nextSceneName 비어있음 — 씬 전환 생략 (테스트 모드)");
            return;
        }

        if (useLoading)
        {
            // 로딩 씬 경유 (LoadingManager 방식)
            LoadingManager.NextScene = nextSceneType;
            GameManager.Instance.LoadScene("LoadingScene");
        }
        else
        {
            GameManager.Instance.LoadScene(nextSceneType);
        }
    }
}
