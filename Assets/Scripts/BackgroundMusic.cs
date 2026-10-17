using UnityEngine;

// Scene mein ek khali GameObject ("BackgroundMusic") par lagayen.
// Game shuru hote hi music loop mein chalega, aaram se fade-in ke sath.
[RequireComponent(typeof(AudioSource))]
public class BackgroundMusic : MonoBehaviour
{
    [Tooltip("Background music ki audio file yahan daalen")]
    public AudioClip musicClip;

    [Tooltip("Music ki awaaz. Footsteps se kam rakhen taake qadam sunai den")]
    [Range(0f, 1f)] public float volume = 0.3f;

    [Tooltip("Shuru mein kitne seconds mein awaaz poori ho")]
    public float fadeInTime = 2f;

    [Tooltip("ON = naya scene load hone par bhi music chalta rahe")]
    public bool keepBetweenScenes = false;

    private AudioSource audioSource;

    void Awake()
    {
        if (keepBetweenScenes)
            DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = musicClip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D: har jagah ek jaisi awaaz
        audioSource.volume = fadeInTime > 0f ? 0f : volume;

        if (musicClip != null)
            audioSource.Play();
    }

    void Update()
    {
        if (audioSource.volume < volume && fadeInTime > 0f)
            audioSource.volume = Mathf.MoveTowards(audioSource.volume, volume, volume / fadeInTime * Time.deltaTime);
    }
}
