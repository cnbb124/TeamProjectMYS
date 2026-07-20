using UnityEngine;

public class PlayerTestCtrl: MonoBehaviour
{
    public float speed = 5f;
    public float mouseSensitivity = 100f;
    private float gravity = -9.81f;
    private float velocityY = 0f;

    private CharacterController cc;
    private float xRotation = 0f;

    public bool canControl = true;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked; // 마우스 화면 고정
    }

    void Update()
    {

        if (canControl)
        {

            if (cc.isGrounded && velocityY < 0)
            {
                velocityY = 0f;
            }

            // WASD 이동
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            // 중력 적용
            velocityY += gravity * Time.deltaTime;

            Vector3 move = transform.forward * v + transform.right * h;
            move.y = velocityY;
            cc.Move(move * speed * Time.deltaTime);

            // 마우스 좌우 → 플레이어 회전
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            transform.Rotate(Vector3.up * mouseX);

            // 마우스 상하 → 카메라만 회전
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        }
    }
}