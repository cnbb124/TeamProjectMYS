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
    }

    void OnTriggerExit(Collider other)
    {
        isClosing = true;
    }
}
