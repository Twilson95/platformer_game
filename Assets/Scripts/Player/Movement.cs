using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Movement : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 5f;
    public float jumpForce = 7f;

    [Header("Dash")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 0.5f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundRadius = 0.2f;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dashAction;

    private bool jumpQueued;
    private bool isGrounded;

    public bool IsDashing { get; private set; }
    private bool canDash = true;

    private float facingDirection = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];
        dashAction = playerInput.actions["Dash"];
    }

    void Update()
    {
        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundRadius,
            groundLayer);

        float move = moveAction.ReadValue<Vector2>().x;

        // Remember which direction we're facing
        if (move > 0)
            facingDirection = 1;
        else if (move < 0)
            facingDirection = -1;

        if (jumpAction.IsPressed() && isGrounded && !IsDashing)
        {
            jumpQueued = true;
        }

        if (dashAction.WasPressedThisFrame() && canDash && !IsDashing)
        {
            StartCoroutine(Dash());
        }
    }

    void FixedUpdate()
    {
        if (IsDashing)
            return;

        float move = moveAction.ReadValue<Vector2>().x;

        rb.linearVelocity = new Vector2(
            move * speed,
            rb.linearVelocity.y);

        if (jumpQueued)
        {
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x,
                jumpForce);

            jumpQueued = false;
        }
    }

    IEnumerator Dash()
    {
        canDash = false;
        IsDashing = true;

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;

        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);

        try
        {
            rb.linearVelocity = new Vector2(facingDirection * dashSpeed, 0);

            yield return new WaitForSeconds(dashDuration);
        }
        finally
        {
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, false);

            rb.gravityScale = originalGravity;
            IsDashing = false;
        }

        yield return new WaitForSeconds(dashCooldown);

        canDash = true;
    }

    void OnDisable()
    {
        // Safety: always restore collisions if this object is disabled
        Physics2D.IgnoreLayerCollision(
            LayerMask.NameToLayer("Player"),
            LayerMask.NameToLayer("Enemy"),
            false);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }
}
