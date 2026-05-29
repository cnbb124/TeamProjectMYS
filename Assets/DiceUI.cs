using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DiceUI : MonoBehaviour
{
    public static DiceUI Instance { get; private set; }

    [Header("DicePanel_1  - 애니메이션")]
    [SerializeField] private Image diceImage; // 주사위 이미지
    [SerializeField] private Sprite[] diceFaces; // 주사위 면 이미지 배열
    [SerializeField] private TMP_Text diceNumberText; // 스프라이트가 없을 시 숫자로 대체

    [Header("DicePanel_2 - 결과")]
    [SerializeField] private TMP_Text itemNameText; // 아이템 이름 텍스트
    [SerializeField] private TMP_Text itemDescText; // 아이템 설명 텍스트

    [Header("애니메이션 설정")]
    [SerializeField] private float rollDuration = 1.5f; // 주사위 굴리는 시간
    [SerializeField] private float startInterval = 0.05f; // 초반 빠른 속도
    [SerializeField] private float endInterval = 0.3f; // 후반 느린 속도

    private Coroutine _rollCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) {Destroy(gameObject); return;}
        Instance = this;
    }

    //DiceManager에서 호출하는 함수
    public void ShowResult(int diceResult, string itemName, string itemDesc = "")
    {
        if (_rollCoroutine != null) StopCoroutine(_rollCoroutine);
        _rollCoroutine = StartCoroutine(RollAnimation(diceResult, itemName, itemDesc));
    }

    private IEnumerator RollAnimation(int result, string itemName, string itemDesc)
    {
        // 결과 텍스트 초기화
        itemNameText.text = "";
        if (itemDescText != null) itemDescText.text = "";

        // 주사위 굴리기 애니메이션
        float elapsed = 0f;
        while (elapsed < rollDuration)
        {
            float t = elapsed / rollDuration;
            float interval = Mathf.Lerp(startInterval, endInterval, t);

            ShowDiceFace(Random.Range(1, 7));

            elapsed += interval;
            yield return new WaitForSeconds(interval);
        }

        // 최종 결과
        ShowDiceFace(result);

        // 잠깐 대기 후 아이템 표시
        yield return new WaitForSeconds(0.4f);
        itemNameText.text = itemName;
        if (itemDescText != null) itemDescText.text = itemDesc;
    }

    private void ShowDiceFace(int number)
    {
        // 스프라이트가 있으면 이미지로, 없으면 숫자 텍스트로
        if (diceFaces != null && diceFaces.Length == 6)
        {
            if (diceImage != null)
                diceImage.sprite = diceFaces[number - 1];
        }
        else
        {
            if (diceNumberText != null)
                diceNumberText.text = number.ToString();
        }
    }
}
