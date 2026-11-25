using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpForce = 5f;

    private Rigidbody rb;
    private MouseLook mouseLook;
    private Vector3 movement;
    private float currentSpeed;
    private bool isGrounded, jumpRequested;
    public bool IsMoving => movement.sqrMagnitude > .01f;
    public bool IsSprinting => IsMoving && Input.GetKey(KeyCode.LeftShift);
    public Vector3 Heading => mouseLook ? mouseLook.Heading : transform.forward;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        mouseLook = GetComponentInChildren<MouseLook>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            movement = Vector3.zero;
            return;
        }
        Vector2 axes = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector3 forward = Heading;
        movement = (Vector3.Cross(Vector3.up, forward) * axes.x + forward * axes.y).normalized;
        currentSpeed = IsSprinting ? sprintSpeed : walkSpeed;
        jumpRequested |= Input.GetKeyDown(KeyCode.Space);
    }

    void FixedUpdate()
    {
        isGrounded = Physics.Raycast(rb.position, Vector3.down, 1.1f, ~0, QueryTriggerInteraction.Ignore);
        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontal = Vector3.MoveTowards(new Vector3(velocity.x, 0, velocity.z), movement * currentSpeed,
            (isGrounded ? 38 : 12) * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        if (jumpRequested && isGrounded) rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        jumpRequested = false;
    }
}
