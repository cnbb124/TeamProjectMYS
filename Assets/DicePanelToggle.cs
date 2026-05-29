using System.Collections;
using UnityEngine;

public class DicePanelToggle : MonoBehaviour
{
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private float expandedHeight = 300f;
    [SerializeField] private float animDuration = 0.3f;
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    private bool _isOpen = false;
    private Coroutine _coroutine;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();
    }

    public void Toggle()
    {
        if (_coroutine != null) StopCoroutine(_coroutine);
        _isOpen = !_isOpen;

        Time.timeScale = _isOpen ? 0f : 1f;

        _coroutine = StartCoroutine(Animate(_isOpen ? expandedHeight : 0f));
    }

    private IEnumerator Animate(float targetHeight)
    {
        float startHeight = panelRect.sizeDelta.y;
        float elapsed = 0f;

        while (elapsed < animDuration)
        {
            elapsed += Time.unscaledDeltaTime; // timeScale 0이어도 작동
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
            panelRect.sizeDelta = new Vector2(
                panelRect.sizeDelta.x,
                Mathf.Lerp(startHeight, targetHeight, t)
            );
            yield return null;
        }

        panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, targetHeight);
    }
}