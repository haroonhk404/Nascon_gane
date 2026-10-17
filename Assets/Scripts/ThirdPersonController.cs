using UnityEngine;
using UnityEngine.InputSystem;

/// Third person movement: slow walk, walk, sprint, crouch, jump.
/// Controls: WASD move | Left Shift sprint | Left Alt slow walk | C crouch (toggle) | Space jump
/// Animator parameters needed: Speed (Float), Grounded (Bool), Crouch (Bool), Jump (Trigger)
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    public Animator animator;

    [Header("Speeds (m/s)")]
    public float slowWalkSpeed = 1.2f;
    public float walkSpeed = 2.5f;
    public float sprintSpeed = 5.5f;
    public float crouchSpeed = 1.5f;
    public float speedChangeRate = 10f;
    public float rotationSmoothTime = 0.12f;

    [Header("Jump & Gravity")]
    public float jumpHeight = 1.2f;
    public float gravity = -15f;
    [Tooltip("Small grace time so the grounded state doesn't flicker on slopes/steps")]
    public float coyoteTime = 0.15f;

    [Header("Crouch")]
    public float crouchHeight = 1.1f;

    CharacterController cc;
    float standHeight;
    Vector3 standCenter;

    float currentSpeed;
    float targetRotation;
    float rotationVelocity;
    float verticalVelocity;
    float lastGroundedTime;
    bool isCrouching;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int GroundedHash = Animator.StringToHash("Grounded");
    static readonly int CrouchHash = Animator.StringToHash("Crouch");
    static readonly int JumpHash = Animator.StringToHash("Jump");

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

        // Movement comes from this script, not from the animation clips
        if (animator != null) animator.applyRootMotion = false;

        standHeight = cc.height;
        standCenter = cc.center;
        targetRotation = transform.eulerAngles.y;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // ---- Input ----
        Vector2 input = new Vector2(
            (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
            (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
        bool hasInput = input.sqrMagnitude > 0.01f;
        bool sprint = kb.leftShiftKey.isPressed;
        bool slow = kb.leftAltKey.isPressed;

        if (kb.cKey.wasPressedThisFrame) ToggleCrouch();

        // ---- Ground & jump ----
        if (cc.isGrounded)
        {
            lastGroundedTime = Time.time;
            if (verticalVelocity < 0f) verticalVelocity = -2f; // keep snapped to ground
        }
        bool grounded = Time.time - lastGroundedTime <= coyoteTime;

        if (grounded && kb.spaceKey.wasPressedThisFrame)
        {
            if (isCrouching)
            {
                ToggleCrouch(); // Space while crouched = stand up
            }
            else
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                lastGroundedTime = -999f;
                grounded = false;
                if (animator) animator.SetTrigger(JumpHash);
            }
        }
        verticalVelocity += gravity * Time.deltaTime;

        // ---- Speed ----
        float targetSpeed = 0f;
        if (hasInput)
        {
            if (isCrouching) targetSpeed = crouchSpeed;
            else if (sprint) targetSpeed = sprintSpeed;
            else if (slow) targetSpeed = slowWalkSpeed;
            else targetSpeed = walkSpeed;
        }
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * speedChangeRate);
        if (Mathf.Abs(currentSpeed - targetSpeed) < 0.01f) currentSpeed = targetSpeed;

        // ---- Rotation (relative to camera) ----
        if (hasInput)
        {
            Vector3 inputDir = new Vector3(input.x, 0f, input.y).normalized;
            float camYaw = cameraTransform ? cameraTransform.eulerAngles.y : 0f;
            targetRotation = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + camYaw;
            float rot = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation,
                                              ref rotationVelocity, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, rot, 0f);
        }

        // ---- Move ----
        Vector3 forward = Quaternion.Euler(0f, targetRotation, 0f) * Vector3.forward;
        Vector3 velocity = forward * currentSpeed + Vector3.up * verticalVelocity;
        cc.Move(velocity * Time.deltaTime);

        // ---- Animator ----
        if (animator)
        {
            animator.SetFloat(SpeedHash, currentSpeed);
            animator.SetBool(GroundedHash, grounded);
            animator.SetBool(CrouchHash, isCrouching);
        }
    }

    void ToggleCrouch()
    {
        if (isCrouching)
        {
            // Only stand up if nothing is above the head
            float extra = standHeight - crouchHeight;
            Vector3 top = transform.position + Vector3.up * (crouchHeight - cc.radius);
            if (Physics.SphereCast(top, cc.radius * 0.9f, Vector3.up, out _, extra,
                                   ~0, QueryTriggerInteraction.Ignore))
                return;

            cc.height = standHeight;
            cc.center = standCenter;
            isCrouching = false;
        }
        else
        {
            cc.height = crouchHeight;
            cc.center = new Vector3(standCenter.x, crouchHeight / 2f, standCenter.z);
            isCrouching = true;
        }
    }
}
