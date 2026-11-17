
using UnityEngine;

public class NPCQueueAgent : MonoBehaviour
{
    public float moveSpeed = 2.5f;
    public float rotateSpeed = 8f;

    private Vector3 targetPosition;

    public void SetTarget(Vector3 position)
    {
        targetPosition = position;
    }

    private void Update()
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        // เดินไปยังตำแหน่งที่กำหนด
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        // หันหน้าไปตามทิศทางการเดิน
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotateSpeed * Time.deltaTime
            );
        }
    }
}
