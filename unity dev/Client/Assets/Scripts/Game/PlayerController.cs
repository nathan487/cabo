using UnityEngine;

/// <summary>
/// Camera-relative WASD movement controller with physics-based motion.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float jumpForce = 6f;

    [Header("Ground Detection")]
    public float groundCheckDistance = 2.5f;
    public LayerMask groundLayer = ~0;

    private Rigidbody rb;
    private Transform camTransform;
    private bool isGrounded;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        camTransform = Camera.main != null ? Camera.main.transform : transform;
    }

    private void Update()
    {
        // Ground check: raycast from cube center downward
        float halfHeight = transform.localScale.y * 0.5f;
        float rayOriginY = transform.position.y;
        isGrounded = Physics.Raycast(transform.position, Vector3.down, halfHeight + groundCheckDistance, groundLayer);

        HandleMovement();
        HandleJump();
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal"); // A / D
        float vertical   = Input.GetAxis("Vertical");   // W / S

        if (Mathf.Approximately(horizontal, 0f) && Mathf.Approximately(vertical, 0f))
            return;

        // Camera-relative direction (horizontal plane only)
        Vector3 forward = camTransform.forward;
        Vector3 right   = camTransform.right;
        forward.y = 0f;
        right.y   = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = (forward * vertical + right * horizontal).normalized;

        // Velocity-based movement (preserve vertical velocity for gravity/jump)
        Vector3 targetVelocity = moveDir * moveSpeed;
        rb.velocity = new Vector3(targetVelocity.x, rb.velocity.y, targetVelocity.z);
    }

    private void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }
}
