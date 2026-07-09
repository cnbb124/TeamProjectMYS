using UnityEngine;

// =====================================================================
// [NPCWander]
// 단순 배회형 NPC. 정면 방향으로 계속 이동하다가, 뭔가에 부딪히면
// (벽/장애물/다른 오브젝트 구분 없이) 방향을 반사시켜 계속 이동.
//
// ▶ Collider는 Is Trigger 꺼진 일반 Collider여야 함 (OnCollisionEnter 사용)
// ▶ Rigidbody 필요 (Is Kinematic 체크 — 스크립트로 직접 이동 제어)
// ▶ Animator에 "Idle", "Walking" State가 등록되어 있어야 함
// =====================================================================
[RequireComponent(typeof(Rigidbody))]
public class NPCWander : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private Animator animator;
    [SerializeField] private float moveDuration = 20f;
    [SerializeField] private float idleDuration = 5f;

    private readonly string IDLE = "Idle";
    private readonly string WALKING = "Walking";

    private Vector3 moveDirection;
    private Rigidbody rb;
    private bool isMoving = true;
    private float stateTimer = 0f;

    private float lastReflectTime = -999f;
    private const float reflectCooldown = 0.2f;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        moveDirection = transform.forward;
        animator.CrossFade(WALKING, 0.25f);
        stateTimer = moveDuration;
    }

    private void Update()
    {
        // 이동/정지 상태 타이머
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            isMoving = !isMoving;

            if (isMoving)
            {
                stateTimer = moveDuration;
                animator.CrossFade(WALKING, 0.25f);

                
                moveDirection = GetRandomDirection();
                transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            }
            else
            {
                stateTimer = idleDuration;
                animator.CrossFade(IDLE, 0.25f);
            }
        }
    }

    private void FixedUpdate()
    {
        if (!isMoving) return; // 정지상태는 이동 안함
        Vector3 newPosition = rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(newPosition);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryReflect(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryReflect(collision);
    }

    private void TryReflect(Collision collision)
    {
        if (!isMoving) return;
        if (Time.time - lastReflectTime < reflectCooldown) return;

        Vector3 normal = collision.contacts[0].normal;

        // 바닥/천장은 무시
        if (Mathf.Abs(normal.y) > 0.7f) return;

        ChangeDirection(normal);
        lastReflectTime = Time.time;
    }

    private void ChangeDirection(Vector3 normal)
    {
        normal.y = 0f;
        normal.Normalize();

        // normal을 기준으로 -90 ~ +90도 사이 랜덤 각도로 회전
        float randomAngle = Random.Range(-90f, 90f);
        Vector3 newDirection = Quaternion.Euler(0f, randomAngle, 0f) * normal;

        newDirection.y = 0f;
        moveDirection = newDirection.normalized;
        transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
    }

    private Vector3 GetRandomDirection()
    {
        float angle = Random.Range(0f, 360f);
        return Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
    }
}