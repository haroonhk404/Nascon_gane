using UnityEngine;
using UnityEngine.InputSystem;

/// Simple orbit camera for third person. Put this on the Main Camera.
/// Mouse to look around | Esc to unlock the cursor | click in Game view to lock again
public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;                         // the player
    public Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);
    public float distance = 4f;
    public float minDistance = 0.6f;
    public float sensitivity = 0.15f;
    public float minPitch = -30f;
    public float maxPitch = 70f;

    [Header("Collision (camera won't go through walls)")]
    public LayerMask collisionLayers = ~0;           // tip: exclude the Player layer here
    public float collisionRadius = 0.2f;
    public float smooth = 15f;

    float yaw;
    float pitch = 15f;
    float currentDistance;

    void Start()
    {
        yaw = transform.eulerAngles.y;
        currentDistance = distance;
        LockCursor(true);
    }

    void LateUpdate()
    {
        if (target == null) return;

        var mouse = Mouse.current;
        var kb = Keyboard.current;

        if (kb != null && kb.escapeKey.wasPressedThisFrame) LockCursor(false);
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) LockCursor(true);

        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * sensitivity;
            pitch -= delta.y * sensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + targetOffset;
        Vector3 back = rot * Vector3.back;

        float desired = distance;
        if (Physics.SphereCast(pivot, collisionRadius, back, out RaycastHit hit, distance,
                               collisionLayers, QueryTriggerInteraction.Ignore))
            desired = Mathf.Max(minDistance, hit.distance);

        currentDistance = Mathf.Lerp(currentDistance, desired, Time.deltaTime * smooth);

        transform.position = pivot + back * currentDistance;
        transform.rotation = rot;
    }

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
