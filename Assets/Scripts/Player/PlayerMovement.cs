using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpForce = 5f;

    private Rigidbody rb;
    private Vector3 movement;
    private float currentSpeed;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // 1. Get WASD input
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        // 2. Calculate movement direction based on where the player is looking
        movement = (transform.right * moveX + transform.forward * moveZ).normalized;

        // 3. Sprinting Check: If holding Left Shift, use sprint speed, otherwise walk speed
        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed = sprintSpeed;
        }
        else
        {
            currentSpeed = walkSpeed;
        }

        // 4. Ground Check: Shoots an invisible laser 1.1 units down from the center of the capsule 
        // to see if it hits the floor (so you can't jump in mid-air)
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f);

        // 5. Jumping Check: If pressing Space AND the player is touching the ground
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            // Apply an instant upward physics force
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void FixedUpdate()
    {
        // Apply horizontal movement using the current speed (walk or sprint)
        rb.MovePosition(rb.position + movement * currentSpeed * Time.fixedDeltaTime);
    }
}