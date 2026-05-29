using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DicePanelToggle : MonoBehaviour
{
    [SerializeField] private RectTransform panelRect;  // PanelDice
    [SerializeField] private float expandedHeight = 300f;
    [SerializeField] private float animDuration = 0.3f;

    private bool _isOpen = false;
    private Coroutine _coroutine;

    public void Toggle()
    {
        if (_coroutine != null) StopCoroutine(_coroutine);
        _isOpen = !_isOpen;
        _coroutine = StartCoroutine(Animate(_isOpen ? expandedHeight : 0f));
    }

    private IEnumerator Animate(float targetHeight)
    {
        float startHeight = panelRect.sizeDelta.y;
        float elapsed = 0f;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
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