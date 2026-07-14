/*
 * [ChatInputIMEFix]
 * 한글 IME 조합창이 입력 필드 캐럿 위치를 따라오도록 보정.
 * (백스페이스 등으로 조합이 리셋될 때 조합창이 화면 가운데로 튀는 Unity/TMP 버그 우회)
 *
 * [부착] 채팅 입력창(TMP_InputField)이 붙은 오브젝트에 함께 부착. 연결할 것 없음.
 *
 * [원리]
 * 매 프레임 캐럿의 스크린 좌표를 Input.compositionCursorPos에 전달 →
 * OS IME가 조합 중인 글자를 캐럿 위치에 그리게 됨.
 * Canvas가 Overlay든 Camera든 좌표 변환을 알아서 처리.
 */

using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_InputField))]
public class ChatInputIMEFix : MonoBehaviour
{
    private TMP_InputField _input;
    private Canvas _canvas;

    private void Awake()
    {
        _input  = GetComponent<TMP_InputField>();
        _canvas = GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        if (_input == null || !_input.isFocused) return;

        Input.compositionCursorPos = GetCaretScreenPos();
    }

    // 캐럿의 스크린 좌표 계산
    private Vector2 GetCaretScreenPos()
    {
        TMP_Text textComp = _input.textComponent;

        // 기본값: 텍스트 컴포넌트 위치 (텍스트가 비었을 때 등)
        Vector3 worldPos = textComp.transform.position;

        // 캐럿 앞 글자의 오른쪽 끝 위치를 캐럿 위치로 사용
        TMP_TextInfo info = textComp.textInfo;
        int caret = Mathf.Clamp(_input.caretPosition, 0, _input.text.Length);

        if (info != null && info.characterCount > 0 && caret > 0)
        {
            int charIndex = Mathf.Clamp(caret - 1, 0, info.characterCount - 1);
            TMP_CharacterInfo ch = info.characterInfo[charIndex];
            Vector3 local = new Vector3(ch.xAdvance, (ch.ascender + ch.descender) * 0.5f, 0f);
            worldPos = textComp.transform.TransformPoint(local);
        }

        // 캔버스 모드별 스크린 좌표 변환
        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay && _canvas.worldCamera != null)
            return _canvas.worldCamera.WorldToScreenPoint(worldPos);

        // Overlay: 월드 좌표 = 스크린 좌표
        return worldPos;
    }
}
