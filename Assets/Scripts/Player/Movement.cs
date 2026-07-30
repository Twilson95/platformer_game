using UnityEngine;
using UnityEngine.InputSystem;

public class Movement : MonoBehaviour
{
    public float speed = 5f;
    public float jumpForce = 7f;

    public Transform groundCheck;
    public float groundRadius = 0.2f;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;

    private bool jumpQueued;
    private bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];
    }

    void Update()
    {
        // Check if player is on the ground
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayer);

        // Queue a jump while Space is held
        if (jumpAction.IsPressed() && isGrounded)
        {
            jumpQueued = true;
        }
    }

    void FixedUpdate()
    {
        // Move left/right
        float move = moveAction.ReadValue<Vector2>().x;
        rb.linearVelocity = new Vector2(move * speed, rb.linearVelocity.y);

        // Jump
        if (jumpQueued)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpQueued = false;
        }
    }

    // Draw the ground check in the Scene view
    void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }
}
