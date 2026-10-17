using System.Collections.Generic;
using UnityEngine;

/// Put this on an empty manager object (e.g. "FireLineGuideManager").
/// Sends thin fire lines from just ahead of the player toward the next path point.
/// Lines vanish as soon as they reach the point.
public class FireLineGuide : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GuidePath path;
    [Tooltip("Where to guide after the last point (e.g. the portal). Optional.")]
    public Transform finalTarget;

    [Header("Progress")]
    [Tooltip("Player within this distance of a point = point reached")]
    public float arrivalRadius = 4f;

    [Header("Timing")]
    public float guideInterval = 4f;
    [Tooltip("If the player stands still this long, send a gust right away")]
    public float idleTriggerTime = 2f;
    public float idleSpeedThreshold = 0.15f;

    [Header("Spawn (relative to player)")]
    public float spawnDistanceMin = 1.5f;
    public float spawnDistanceMax = 2.5f;
    public float lateralSpread = 1.2f;
    public Vector2 lowHeight = new Vector2(0.2f, 0.6f);
    public Vector2 midHeight = new Vector2(0.9f, 1.6f);
    public Vector2 highHeight = new Vector2(2f, 3f);
    [Range(0f, 1f)] public float lowChance = 0.4f;
    [Range(0f, 1f)] public float highChance = 0.35f;

    [Header("Fire Lines")]
    public int minLines = 4;
    public int maxLines = 6;
    public float flySpeed = 7f;
    public float waveAmplitude = 0.6f;
    public float waveFrequency = 1.5f;
    [Tooltip("Lines in one gust start with a small random delay")]
    public float launchSpread = 0.4f;
    public float lineWidth = 0.04f;
    [Tooltip("Tail length in seconds. Also how fast a line disappears after arriving.")]
    public float trailTime = 0.4f;
    public Gradient lineColor;
    [Tooltip("Use a URP Particles/Unlit material, Transparent + Additive")]
    public Material lineMaterial;
    [Tooltip("Overall brightness/visibility of the lines")]
    [Range(0f, 1f)] public float opacity = 1f;

    [Header("Burn Effect (optional)")]
    [Tooltip("Small fire prefab that rides on the head of each line, e.g. Vefects 'Fire Small Simple'")]
    public GameObject headFlamePrefab;
    [Tooltip("Size of that fire. Keep it small for a subtle burn.")]
    public float headFlameScale = 0.15f;
    [Tooltip("Turn off lights/sounds inside the fire prefab (better performance)")]
    public bool stripLightsAndSound = true;

    [Header("Avoid Walls")]
    [Tooltip("Lines check their path and won't fly through walls")]
    public bool avoidWalls = true;
    [Tooltip("Layers the fire must not pass through. Uncheck the Player layer here.")]
    public LayerMask obstacleLayers = ~0;
    [Tooltip("How many checks along each line's path (more = more accurate)")]
    [Range(4, 16)] public int pathSamples = 8;

    [Header("Pool")]
    public int poolSize = 4;

    [Header("Debug (read only)")]
    [SerializeField] int nextPointIndex;

    readonly List<FireLineGust> pool = new List<FireLineGust>();
    float gustTimer, idleTimer, sinceLastGust;
    Vector3 lastPlayerPos;

    void Reset() { lineColor = DefaultGradient(); }

    void Start()
    {
        if (lineColor == null) lineColor = DefaultGradient();
        if (lineMaterial == null) lineMaterial = CreateFallbackMaterial();
        if (player) lastPlayerPos = player.position;
        gustTimer = guideInterval * 0.5f; // first gust comes a bit sooner
    }

    void Update()
    {
        if (player == null || path == null) return;

        UpdateProgress();
        if (!TryGetTarget(out Vector3 target)) return;

        // Player reached the final target: stop guiding
        if (nextPointIndex >= path.Count && finalTarget != null &&
            IsNear(player.position, finalTarget.position)) return;

        // Idle detection
        float speed = (player.position - lastPlayerPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPlayerPos = player.position;
        idleTimer = speed < idleSpeedThreshold ? idleTimer + Time.deltaTime : 0f;

        gustTimer += Time.deltaTime;
        sinceLastGust += Time.deltaTime;

        bool timeForGust = gustTimer >= guideInterval;
        bool idleGust = idleTimer >= idleTriggerTime && sinceLastGust >= idleTriggerTime;

        if (timeForGust || idleGust) LaunchGust(target);
    }

    void UpdateProgress()
    {
        // Check from the furthest point back, so skipping ahead also counts
        for (int i = path.Count - 1; i >= nextPointIndex; i--)
        {
            if (IsNear(player.position, path.GetPoint(i)))
            {
                nextPointIndex = i + 1;
                break;
            }
        }
    }

    bool TryGetTarget(out Vector3 target)
    {
        if (nextPointIndex < path.Count) { target = path.GetPoint(nextPointIndex); return true; }
        if (finalTarget != null) { target = finalTarget.position; return true; }
        target = Vector3.zero;
        return false;
    }

    bool IsNear(Vector3 a, Vector3 b)
    {
        Vector3 d = a - b;
        if (Mathf.Abs(d.y) > 3f) return false; // different floor
        d.y = 0f;
        return d.magnitude <= arrivalRadius;
    }

    void LaunchGust(Vector3 target)
    {
        FireLineGust gust = GetGust();
        if (gust == null) return;

        Vector3 dir = target - player.position;
        dir.y = 0f;
        float dist = dir.magnitude;
        if (dist < 0.01f) return;
        dir /= dist;

        float ahead = Mathf.Min(Random.Range(spawnDistanceMin, spawnDistanceMax), dist * 0.5f);
        Vector3 center = player.position + dir * ahead;

        // Don't start the gust inside/behind a wall in front of the player
        if (avoidWalls)
        {
            Vector3 chest = player.position + Vector3.up * 1.2f;
            if (Physics.Linecast(chest, center + Vector3.up * 1.2f, out RaycastHit hit,
                                 obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                center = hit.point - dir * 0.3f;
                center.y = player.position.y;
            }
        }

        gust.Launch(center, target);
        gustTimer = 0f;
        sinceLastGust = 0f;
    }

    FireLineGust GetGust()
    {
        foreach (var g in pool) if (!g.Busy) return g;
        if (pool.Count >= poolSize) return null;

        var go = new GameObject("FireLineGust");
        go.transform.SetParent(transform, false);
        var gust = go.AddComponent<FireLineGust>();
        gust.Init(this);
        pool.Add(gust);
        return gust;
    }

    public float RandomHeight()
    {
        float r = Random.value;
        if (r < lowChance) return Random.Range(lowHeight.x, lowHeight.y);
        if (r < lowChance + highChance) return Random.Range(highHeight.x, highHeight.y);
        return Random.Range(midHeight.x, midHeight.y);
    }

    static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f),   // bright yellow head
                new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.4f), // orange
                new GradientColorKey(new Color(0.8f, 0.1f, 0.05f), 1f) // red tail
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        return g;
    }

    static Material CreateFallbackMaterial()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null) s = Shader.Find("Sprites/Default");
        var m = new Material(s);
        if (s != null && s.name.Contains("Particles"))
        {
            m.SetFloat("_Surface", 1f); // Transparent
            m.SetFloat("_Blend", 2f);   // Additive
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }
        return m;
    }

    // Shows arrival circles in the Scene view when this object is selected
    void OnDrawGizmosSelected()
    {
        if (path == null) return;
        for (int i = 0; i < path.Count; i++)
        {
            Gizmos.color = (Application.isPlaying && i == nextPointIndex) ? Color.yellow : new Color(1f, 0.5f, 0.1f, 0.4f);
            Gizmos.DrawWireSphere(path.GetPoint(i), arrivalRadius);
        }
    }
}
