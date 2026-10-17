using System;
using UnityEngine;

/// <summary>
/// Har candle pe lagao. Starter candle: startLit = true, canBeGrabbed = true.
/// Baqi candles: startLit = false. Flame (Fire 001 particle) + Point Light child mein rakho.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Candle : MonoBehaviour, IInteractable
{
    [Header("State")]
    public bool startLit = false;
    [Tooltip("Sirf starter candle pe ON karo - isko player utha sakta hai")]
    public bool canBeGrabbed = false;

    [Header("Visuals")]
    [Tooltip("Flame particle object (e.g. Fire 001 prefab, scale chota kar ke)")]
    public GameObject flameObject;
    public Light candleLight;
    public float lightIntensity = 2f;
    public bool flicker = true;
    public float flickerAmount = 0.4f;
    public float flickerSpeed = 8f;

    [Header("Hold Pose (hand socket ke andar)")]
    public Vector3 holdLocalPosition = Vector3.zero;
    public Vector3 holdLocalEuler = Vector3.zero;

    [Header("Audio (optional)")]
    public AudioSource audioSource;
    public AudioClip igniteClip;
    public AudioClip grabClip;

    [Header("Prompts")]
    public string grabText = "[E] Grab Candle";
    public string lightText = "[E] Light Candle";

    public bool IsLit { get; private set; }
    public bool IsHeld { get; private set; }

    /// Puzzle manager isko subscribe karta hai
    public event Action<Candle> OnLit;

    Collider[] colliders;
    Rigidbody rb;
    float noiseSeed;

    void Awake()
    {
        colliders = GetComponentsInChildren<Collider>();
        rb = GetComponent<Rigidbody>();
        noiseSeed = UnityEngine.Random.value * 100f;
        SetLit(startLit, false);
    }

    void Update()
    {
        if (IsLit && flicker && candleLight)
        {
            float n = Mathf.PerlinNoise(noiseSeed, Time.time * flickerSpeed);
            candleLight.intensity = lightIntensity + (n - 0.5f) * 2f * flickerAmount;
        }
    }

    public void SetLit(bool lit, bool notify = true)
    {
        IsLit = lit;
        if (flameObject) flameObject.SetActive(lit);
        if (candleLight)
        {
            candleLight.enabled = lit;
            candleLight.intensity = lightIntensity;
        }

        if (lit && notify)
        {
            Play(igniteClip);
            OnLit?.Invoke(this);
        }
    }

    // ---------- IInteractable ----------
    public string GetPrompt(PlayerInteractor player)
    {
        if (IsHeld) return "";

        if (canBeGrabbed && IsLit && player.HeldCandle == null)
            return grabText;

        if (!IsLit)
            return (player.HeldCandle != null && player.HeldCandle.IsLit) ? lightText : "";

        return "";
    }

    public void Interact(PlayerInteractor player)
    {
        if (IsHeld) return;

        if (canBeGrabbed && IsLit && player.HeldCandle == null)
        {
            Grab(player);
            return;
        }

        if (!IsLit)
        {
            if (player.HeldCandle != null && player.HeldCandle.IsLit)
                SetLit(true);
        }
    }

    void Grab(PlayerInteractor player)
    {
        IsHeld = true;
        foreach (var c in colliders) c.enabled = false;   // raycast block na kare
        if (rb) { rb.isKinematic = true; rb.useGravity = false; }

        transform.SetParent(player.handSocket, false);
        transform.localPosition = holdLocalPosition;
        transform.localRotation = Quaternion.Euler(holdLocalEuler);

        player.HoldCandle(this);
        Play(grabClip);
    }

    void Play(AudioClip clip)
    {
        if (audioSource && clip) audioSource.PlayOneShot(clip);
    }
}
