using UnityEngine;

// 플레이어 이동 스크립트
// (카메라뷰 고정 이동 , 회전 보정 , 애니메이션 상태 전환)
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("방향별 이동 속도")]
    public float forwardSpeed = 3f;
    public float backSpeed = 2f;
    public float strafeSpeed = 2.2f;
    public float gravity = -9.81f;

    [Header("방향 판정 각도")]
    public float forwardAngle = 60f;
    public float backAngle = 120f;

    [Header("상태 전환 안정화")]
    public float switchDelay = 0.12f;
    public float hysteresis = 8f;

    [Header("조준 자세")]
    public float aimStanceHoldTime = 6f;

    [Header("캐릭터 회전")]
    public float characterRotationSpeed = 10f;

    [Header("회전 보정")] // 모델 방향 틀어짐
    public float rotationOffset = 45f;

    [Header("애니메이션 판정 보정")]
    public float animRotationOffset = -10f;

    [Header("디버그")]
    public bool drawDebug = true;
    public bool showDebugUI = true;

    private CharacterController controller;
    private Animator animator;
    private Vector3 verticalVelocity;

    private int currentState;
    private int rawState;
    private float pendingTime;
    private float signedAngle;
    private Vector3 lastWorldMoveDir;
    private float lastActiveTime = -999f;
    private bool isAiming;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        if (animator != null)
            animator.applyRootMotion = false;
    }

    private void Update()
    {
        // 키보드 입력
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector2 input = new Vector2(h, v);
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        // 활동 입력 감지 - 이동 시
        bool activeInput = input.sqrMagnitude > 0.01f;
        if (activeInput)
            lastActiveTime = Time.time;

        isAiming = (Time.time - lastActiveTime) <= aimStanceHoldTime;

        // 고정 이동 방향 - 월드 좌표계 기준
        Vector3 worldMoveDir = new Vector3(h, 0f, v);

        if (worldMoveDir.sqrMagnitude > 1f)
            worldMoveDir.Normalize();

        lastWorldMoveDir = worldMoveDir;

        // 캐릭터 회전 - 우클릭 시 마우스 방향, 평소 이동 방향 + 45도
        Camera mainCam = Camera.main;
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        Ray mouseRay = mainCam.ScreenPointToRay(Input.mousePosition);

        // 마우스 위치를 기준으로 캐릭터 회전 방향 결정
        if (groundPlane.Raycast(mouseRay, out float distance))
        {
            Vector3 mouseWorldPos = mouseRay.GetPoint(distance);

            // 에임중이면 -> 크로스헤어 방향을 바라봄
            if (Input.GetMouseButton(1))
            {
                Vector3 directionToMouse = mouseWorldPos - transform.position;
                directionToMouse.y = 0f;

                if (directionToMouse.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToMouse);
                    targetRotation *= Quaternion.Euler(0f, rotationOffset, 0f); // ★ 45도 추가

                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        characterRotationSpeed * Time.deltaTime
                    );
                }
            }
            // 우클릭을 하지 않을 때 -> 이동 방향을 바라봄
            else if (worldMoveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(worldMoveDir);
                targetRotation *= Quaternion.Euler(0f, rotationOffset, 0f); // ★ 45도 추가

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    characterRotationSpeed * Time.deltaTime
                );
            }
        }

        // 애니메이션 판정
        if (worldMoveDir.sqrMagnitude < 0.001f)
        {
            rawState = 0;
            signedAngle = 0f;
        }
        else
        {
            // 캐릭터 정면에 애니메이션 보정각도 적용
            Vector3 animForward = Quaternion.Euler(0f, animRotationOffset, 0f) * transform.forward;
            signedAngle = Vector3.SignedAngle(animForward, worldMoveDir, Vector3.up);
            rawState = RawStateFromAngle(signedAngle);
        }

        // 상태 전환
        if (rawState != currentState)
        {
            pendingTime += Time.deltaTime;
            bool immediate = (rawState == 0 || currentState == 0);

            if (immediate || pendingTime >= switchDelay)
            {
                currentState = rawState;
                pendingTime = 0f;
            }
        }
        else
        {
            pendingTime = 0f;
        }

        // Animator 파라미터
        if (animator != null)
        {
            animator.SetInteger("MoveState", currentState);
            animator.SetBool("IsAiming", isAiming);
        }

        // 속도 적용
        float speed = 0f;
        switch (currentState)
        {
            case 1: speed = forwardSpeed; break;
            case 2: speed = backSpeed; break;
            case 3:
            case 4: speed = strafeSpeed; break;
        }

        // 중력 처리
        if (controller.isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f;

        verticalVelocity.y += gravity * Time.deltaTime;

        // 이동 적용
        Vector3 velocity = worldMoveDir * speed + verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        // 위치를 기준으로 캐릭터 회전 - 디버깅용
        if (drawDebug && worldMoveDir.sqrMagnitude > 0.01f)
        {
            Debug.DrawLine(transform.position, transform.position + worldMoveDir * 2f, Color.green);
            Debug.DrawLine(transform.position, transform.position + transform.forward * 2f, Color.blue);
        }
    }

    // 각도에 따른 상태 판정
    private int RawStateFromAngle(float angle)
    {
        float abs = Mathf.Abs(angle);
        float fwdBound = forwardAngle;
        float backBound = backAngle;

        if (currentState == 1) fwdBound += hysteresis;
        else if (currentState == 2) backBound -= hysteresis;
        else if (currentState == 3 || currentState == 4)
        {
            fwdBound -= hysteresis;
            backBound += hysteresis;
        }

        if (abs <= fwdBound) return 1;
        if (abs >= backBound) return 2;
        return angle > 0f ? 4 : 3;
    }

    //// 디버그 UI
    //private void OnGUI()
    //{
    //    if (!showDebugUI) return;

    //    string text =
    //        "State: " + currentState + " (raw: " + rawState + ")\n" +
    //        "IsAiming: " + isAiming + "\n" +
    //        "SignedAngle: " + signedAngle.ToString("F1");

    //    GUI.Label(new Rect(10, 10, 420, 120), text);
    //}
}