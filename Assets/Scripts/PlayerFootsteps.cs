using UnityEngine;

// Player GameObject par lagayen. Chalne/bhaagne par footstep sounds play karta hai.
[RequireComponent(typeof(AudioSource))]
public class PlayerFootsteps : MonoBehaviour
{
    [Header("Footstep Sounds")]
    [Tooltip("2-4 alag footstep clips daalen taake awaaz repeat na lage")]
    public AudioClip[] footstepClips;

    [Header("Timing")]
    [Tooltip("Chalte waqt do qadmon ke beech ka waqt (seconds)")]
    public float walkStepInterval = 0.5f;
    [Tooltip("Bhaagte waqt do qadmon ke beech ka waqt (seconds)")]
    public float runStepInterval = 0.3f;
    [Tooltip("Is speed se upar running mana jayega (aapke blend tree mein Running 5.5 par hai)")]
    public float runSpeedThreshold = 4f;
    [Tooltip("Is speed se kam ho to player khara mana jayega")]
    public float minMoveSpeed = 0.2f;

    [Header("Variation")]
    [Tooltip("1 se upar = aur tez (3 tak)")]
    [Range(0f, 3f)] public float volume = 2f;
    [Tooltip("ON = awaaz hamesha poori tez (2D). OFF = door se halki (3D)")]
    public bool alwaysLoud = true;
    [Range(0f, 0.3f)] public float pitchVariation = 0.1f;

    private AudioSource audioSource;
    private CharacterController characterController;
    private Vector3 lastPosition;
    private float stepTimer;
    private int lastClipIndex = -1;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = 1f;
        audioSource.spatialBlend = alwaysLoud ? 0f : 1f; // 0 = 2D (poori awaaz), 1 = 3D
        characterController = GetComponent<CharacterController>();
        lastPosition = transform.position;
    }

    void Update()
    {
        // Sirf horizontal speed calculate karo (jump/girne ko ignore)
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPosition = transform.position;

        bool grounded = characterController == null || characterController.isGrounded;

        if (speed < minMoveSpeed || !grounded)
        {
            stepTimer = 0f; // ruk gaya to agla qadam foran bajega
            return;
        }

        float interval = speed >= runSpeedThreshold ? runStepInterval : walkStepInterval;

        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            PlayFootstep();
            stepTimer = interval;
        }
    }

    void PlayFootstep()
    {
        if (footstepClips == null || footstepClips.Length == 0) return;

        // Same clip do baar lagatar na bajay
        int index = Random.Range(0, footstepClips.Length);
        if (footstepClips.Length > 1 && index == lastClipIndex)
            index = (index + 1) % footstepClips.Length;
        lastClipIndex = index;

        audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.PlayOneShot(footstepClips[index], volume);
    }
}
