using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class LobbyButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image sliderFill;   // 채워지는 Image (Filled)
    [SerializeField] private float fillSpeed = 4f;

    private Coroutine _coroutine;

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetFill(1f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetFill(0f);
    }

    private void SetFill(float target)
    {
        if (_coroutine != null) StopCoroutine(_coroutine);
        _coroutine = StartCoroutine(AnimateFill(target));
    }

    private IEnumerator AnimateFill(float target)
    {
        while (!Mathf.Approximately(sliderFill.fillAmount, target))
        {
            sliderFill.fillAmount = Mathf.MoveTowards(
                sliderFill.fillAmount, target, fillSpeed * Time.deltaTime);
            yield return null;
        }
        sliderFill.fillAmount = target;
    }
}