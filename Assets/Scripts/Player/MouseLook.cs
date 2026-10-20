using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public float mouseSensitivity = 300f;
    public Transform playerBody; // This connects to your Capsule

    private float xRotation = 0f;

    void Start()
    {
        // Locks the mouse cursor to the center of the screen and hides it
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Gather mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Calculate up/down camera rotation and clamp it so you can't snap your neck
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Apply the up/down rotation to the camera
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Apply the left/right rotation to the player capsule
        playerBody.Rotate(Vector3.up * mouseX);
    }
}