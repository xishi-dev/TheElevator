
using UnityEngine;

public class NPCQueueAgent : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2.5f;
    public float rotateSpeed = 8f;

    private Vector3 targetPosition;

    private bool hasTarget = false;
    private bool isGrabbed = false;
    private bool isInQueue = true;

    // กำหนดตำแหน่งที่ต้องเดินไป
    public void SetTarget(Vector3 position)
    {
        if (isGrabbed || !isInQueue)
            return;

        targetPosition = position;
        hasTarget = true;
    }

    // หยุดหรือเปิดการควบคุมจากระบบจับ
    public void SetGrabbed(bool grabbed)
    {
        isGrabbed = grabbed;
    }

    // ออกจากระบบต่อแถว
    public void LeaveQueue()
    {
        isInQueue = false;
        hasTarget = false;
    }

    private void Update()
    {
        // ไม่ให้ระบบ Queue ควบคุมตอนถูกจับ
        if (isGrabbed || !isInQueue || !hasTarget)
            return;

        Vector3 direction =
            targetPosition - transform.position;

        direction.y = 0f;

        // เดินไปยังตำแหน่งเป้าหมาย
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        // หันหน้าไปตามทิศทาง
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
