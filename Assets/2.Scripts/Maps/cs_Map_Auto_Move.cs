using System.Collections;
using UnityEngine;

public class cs_Map_Auto_Move : MonoBehaviour
{
    [Header("문 오른쪽/왼쪽")]
    public Transform[] door;
    [Header("움직일 유닛")]
    public Transform unit;
    [Header("얼마나 이동할지")]
    public float openDistance = 20f;
    public float moveDistance = 150f;
    [Header("움직임 속도")]
    public float doorSpeed = 3f;
    public float moveSpeed = 10f;

    private Vector3 openPos_L;
    private Vector3 openPos_R;
    private Vector3 closedPos_L;
    private Vector3 closedPos_R;
    private Vector3 startUnitPos;
    private bool isClosing = false;

    [Header("이동할 씬 관련")]
    public string nextSceneName;  // Inspector에서 씬 이름 입력
    private bool isNextScene = false;

    void Start()
    {
        closedPos_L = door[0].localPosition;
        closedPos_R = door[1].localPosition;
        openPos_L = door[0].localPosition + new Vector3(-openDistance, 0, 0);
        openPos_R = door[1].localPosition + new Vector3(openDistance, 0, 0);
        startUnitPos = unit.localPosition + new Vector3(0, -7.0f, moveDistance);  // 유닛 움직일 좌표 수정은 여기서
    }

    void Update()
    {
        if (isClosing)
        {
            door[0].localPosition = Vector3.MoveTowards(door[0].localPosition, closedPos_L, doorSpeed * Time.deltaTime);
            door[1].localPosition = Vector3.MoveTowards(door[1].localPosition, closedPos_R, doorSpeed * Time.deltaTime);
        }
        else
        {
            door[0].localPosition = Vector3.MoveTowards(door[0].localPosition, openPos_L, doorSpeed * Time.deltaTime);
            door[1].localPosition = Vector3.MoveTowards(door[1].localPosition, openPos_R, doorSpeed * Time.deltaTime);
        }

        unit.localPosition = Vector3.MoveTowards(unit.localPosition, startUnitPos, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(unit.localPosition, startUnitPos) < 1f && !isNextScene)
        {
            isNextScene = true;
            StartCoroutine(nextScene());
        }
    }

    void OnTriggerExit(Collider other)
    {
        isClosing = true;
    }

    IEnumerator nextScene()
    {
        yield return new WaitForSeconds(1.0f);
        // 씬 전환은 반드시 GameManager 경유 — 전환 직전 정리(풀/사운드/이펙트 회수)가 실행되어야
        // DontDestroyOnLoad 매니저가 파괴된 유닛 참조를 들고 가는 문제가 안 생김.
        // (GameManager 없는 테스트 씬 대비 폴백만 직접 로드)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadScene(nextSceneName);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
        }
    }
}
