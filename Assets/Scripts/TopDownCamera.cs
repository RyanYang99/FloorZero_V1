using UnityEngine;

// 탑다운 카메라
public class TopDownCamera : MonoBehaviour
{
    [Header("추격 대상")]
    public Transform target; // Player 오브젝트

    [Header("프셋 [카메라 to 플레이어]")]
    public Vector3 offset = new Vector3(0f, 10f, -5f);

    [Header("추격 속도")]
    public float smoothSpeed = 5f;

    // LateUpdate로 모든 Update가 끝난 후 호출해서, 플레이어 이동 후 카메라를 따라가게끔 
    void LateUpdate()
    {
        if (target == null) return;

        // 목표 위치 = 플레이어 위치 + 오프셋
        Vector3 desiredPosition = target.position + offset;

        // 부드럽게 이동 - Lerp
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;
    }
}