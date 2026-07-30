using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public Transform target;
    public Rigidbody2D playerRb;

    [Header("Offset")]
    public Vector3 offset = new Vector3(0, -1, -10);

    [Header("Dead Zones")]
    public float horizontalDeadZone = 2f;
    public float verticalDeadZone = 1.5f;

    [Header("Normal Follow")]
    public float smoothTime = 0.2f;

    [Header("Fall Follow")]
    public float fallSpeedThreshold = -8f;

    private Vector3 velocity;

    void LateUpdate()
    {
        if (target == null || playerRb == null)
            return;

        // ------------------------
        // Falling Mode
        // ------------------------
        if (playerRb.linearVelocity.y < fallSpeedThreshold)
        {
            // Match the player's vertical speed exactly.
            transform.position += Vector3.up * playerRb.linearVelocity.y * Time.deltaTime;

            // Never let the camera get below the player.
            float targetY = target.position.y + offset.y;

            if (transform.position.y < targetY)
            {
                transform.position = new Vector3(
                    transform.position.x,
                    targetY,
                    offset.z
                );
            }

            // Keep horizontal movement smooth.
            float x = transform.position.x;

            float xDiff = target.position.x - transform.position.x;

            if (Mathf.Abs(xDiff) > horizontalDeadZone)
            {
                float desiredX = target.position.x - Mathf.Sign(xDiff) * horizontalDeadZone;

                x = Mathf.SmoothDamp(
                    transform.position.x,
                    desiredX,
                    ref velocity.x,
                    smoothTime
                );
            }

            transform.position = new Vector3(x, transform.position.y, offset.z);

            return;
        }

        // ------------------------
        // Normal Camera
        // ------------------------
        Vector3 desiredPosition = transform.position;

        float dx = target.position.x - transform.position.x;
        if (Mathf.Abs(dx) > horizontalDeadZone)
        {
            desiredPosition.x = target.position.x - Mathf.Sign(dx) * horizontalDeadZone;
        }

        float dy = target.position.y - transform.position.y;
        if (Mathf.Abs(dy) > verticalDeadZone)
        {
            desiredPosition.y = target.position.y - Mathf.Sign(dy) * verticalDeadZone;
        }

        desiredPosition += new Vector3(offset.x, offset.y, 0);
        desiredPosition.z = offset.z;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            smoothTime
        );
    }
}