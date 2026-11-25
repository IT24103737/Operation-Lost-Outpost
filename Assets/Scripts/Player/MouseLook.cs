using UnityEngine;

[DefaultExecutionOrder(-100)]
public class MouseLook : MonoBehaviour
{
    public float mouseSensitivity = 300f;
    [SerializeField, Min(.001f)] private float smoothingTime = .022f;

    private float yaw, pitch, targetYaw, targetPitch;
    public float AimSensitivity { get; set; } = 1f;

    public Vector3 Heading
    {
        get
        {
            Vector3 forward = transform.parent ? transform.parent.TransformDirection(Quaternion.Euler(0, yaw, 0) * Vector3.forward)
                : Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            forward.y = 0;
            return forward.normalized;
        }
    }

    public void ResetLook()
    {
        yaw = pitch = targetYaw = targetPitch = 0;
        transform.localRotation = Quaternion.identity;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        // Unity's mouse axes already represent movement accumulated this frame.
        Vector2 delta = new(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        ApplyLook(delta, Time.unscaledDeltaTime);
    }

    private void ApplyLook(Vector2 delta, float step)
    {
        float sensitivity = mouseSensitivity * .01f * AimSensitivity;
        targetYaw = Mathf.Repeat(targetYaw + delta.x * sensitivity + 180, 360) - 180;
        targetPitch = Mathf.Clamp(targetPitch - delta.y * sensitivity, -88, 88);
        float follow = 1 - Mathf.Exp(-step / smoothingTime);
        yaw = Mathf.Repeat(Mathf.LerpAngle(yaw, targetYaw, follow) + 180, 360) - 180;
        pitch = Mathf.Lerp(pitch, targetPitch, follow);
        transform.localRotation = Quaternion.Euler(pitch, yaw, 0);
    }
}
