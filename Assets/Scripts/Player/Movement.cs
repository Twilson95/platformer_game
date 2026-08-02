using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlatformerGame.Player
{
public class Movement : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 5f;
    public float jumpForce = 7f;

    [Header("Dash")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 0.5f;
    
    [Header("Attack")]
    public float attackCooldown = 0.1f;
    public GameObject sword;
    [SerializeField, Min(0.01f)] private float swingDuration = 0.2f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundRadius = 0.2f;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dashAction;
    private InputAction attackAction;

    private bool jumpQueued;
    private bool isGrounded;

    public bool IsDashing { get; private set; }
    private bool canDash = true;
    private bool canAttack = true;

    private float facingDirection = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];
        dashAction = playerInput.actions["Dash"];
        attackAction = playerInput.actions["Attack"];
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

        if (attackAction.WasPressedThisFrame() && canAttack)
        {
            StartCoroutine(Attack());
        }
    }

    void FixedUpdate()
    {
        if (IsDashing)
            return;

        float moveX = moveAction.ReadValue<Vector2>().x;

        rb.linearVelocity = new Vector2(
            moveX * speed,
            rb.linearVelocity.y);
        if (moveX != 0)
        {
            transform.rotation = Quaternion.Euler(0, moveX > 0 ? 0 : 180, 0);
        }

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
    IEnumerator Attack()
    {
        canAttack = false;

        if (sword != null)
        {
            sword.SetActive(true);
            yield return new WaitForSeconds(swingDuration);
            sword.SetActive(false);
        }

        yield return new WaitForSeconds(attackCooldown);

        canAttack = true;
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
}
