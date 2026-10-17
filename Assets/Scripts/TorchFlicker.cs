using UnityEngine;

/// Subtle Perlin-noise flicker for a torch Point Light.
[RequireComponent(typeof(Light))]
public class TorchFlicker : MonoBehaviour
{
    public float baseIntensity = 2.5f;
    [Range(0f, 1f)] public float flickerAmount = 0.6f;
    public float flickerSpeed = 9f;

    Light torchLight;
    float seed;

    void Awake()
    {
        torchLight = GetComponent<Light>();
        seed = Random.value * 100f;
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * flickerSpeed);
        torchLight.intensity = baseIntensity * (1f - flickerAmount * 0.5f + n * flickerAmount);
    }
}
