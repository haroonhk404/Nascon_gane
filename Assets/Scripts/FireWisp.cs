using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Lives on the FireWisp prefab root. A gust of real fire: several small flame ParticleSystems
/// (children named "Streak_0", "Streak_1", ...) fly together on flowing S-curves toward a target,
/// each with its own height band/offset/launch delay, carrying a shared ember trail (child
/// "Embers") and a faint point Light that only lights up while the fire is actually moving -
/// nothing is left glowing at the target.
[RequireComponent(typeof(Light))]
[RequireComponent(typeof(AudioSource))]
public class FireWisp : MonoBehaviour
{
    [Header("Flame Streaks")]
    [Tooltip("Each gust uses a random number of flames in this range (needs that many 'Streak_N' children).")]
    [Range(1, 16)] public int minStreaks = 5;
    [Range(1, 16)] public int maxStreaks = 8;
    [Tooltip("Streaks within one gust launch over this many seconds, like a gust rather than a single line.")]
    public float launchSpreadTime = 0.35f;
    [Tooltip("Lateral (side-to-side) spread radius of flames around the group's flight path.")]
    public float gustSpreadRadius = 0.6f;
    [Tooltip("Secondary sine frequency multiplier, combined with the base wave for a flowing S-curve.")]
    public float sCurveFrequencyScale = 1.8f;

    [Header("Flame Height Mix")]
    [Tooltip("Some flames hug the ground, in this height range.")]
    public float groundHeightMin = 0.2f;
    public float groundHeightMax = 0.6f;
    [Tooltip("Some flames fly above head height, in this range.")]
    public float headHeightMin = 2f;
    public float headHeightMax = 3f;

    [Header("Embers")]
    public Color emberColor = new Color(1f, 0.5f, 0.1f);

    [Header("Glow")]
    [Tooltip("Faint, shadowless point light carried only while the fire is flying - it switches off the instant it arrives, so nothing glows at the waypoint.")]
    public float lightIntensity = 0.6f;
    public float lightRange = 5f;

    public bool IsBusy { get; private set; }

    class Streak
    {
        public ParticleSystem ps;
        public Transform t;
        public Vector3 spawnOffset;
        public Vector3 lastPos;
        public float phase;
        public float amplitudeScale;
        public float launchDelay;
        public bool arrived;
    }

    Light glowLight;
    AudioSource whooshSource;
    ParticleSystem embers;
    Transform embersTransform;
    readonly List<ParticleSystem> streakPool = new List<ParticleSystem>();
    readonly List<Streak> active = new List<Streak>();
    Coroutine routine;

    void Awake()
    {
        glowLight = GetComponent<Light>();
        glowLight.range = lightRange;
        glowLight.shadows = LightShadows.None;
        glowLight.intensity = 0f;

        whooshSource = GetComponent<AudioSource>();
        whooshSource.playOnAwake = false;
        whooshSource.loop = false;
        whooshSource.spatialBlend = 1f;

        Transform emb = transform.Find("Embers");
        if (emb != null)
        {
            embersTransform = emb;
            embers = emb.GetComponent<ParticleSystem>();
        }

        for (int i = 0; i < maxStreaks; i++)
        {
            Transform t = transform.Find("Streak_" + i);
            ParticleSystem ps = t != null ? t.GetComponent<ParticleSystem>() : null;
            if (ps != null) streakPool.Add(ps);
        }
    }

    public void BeginGuide(Vector3 spawnPos, Transform target, FireWispGuide cfg)
    {
        if (routine != null) StopCoroutine(routine);

        transform.position = spawnPos;
        gameObject.SetActive(true);
        IsBusy = true;
        glowLight.intensity = lightIntensity;

        if (embersTransform != null) embersTransform.position = spawnPos;
        if (embers != null) embers.Play(true);

        if (whooshSource.clip != null)
        {
            whooshSource.volume = 1f;
            whooshSource.Play();
        }

        SetupStreaks(spawnPos);

        routine = StartCoroutine(GuideRoutine(spawnPos, target, cfg));
    }

    float RandomHeight()
    {
        int band = Random.Range(0, 3);
        if (band == 0) return Random.Range(groundHeightMin, groundHeightMax);
        if (band == 1) return Random.Range(groundHeightMax, headHeightMin); // in between
        return Random.Range(headHeightMin, headHeightMax);
    }

    void SetupStreaks(Vector3 spawnPos)
    {
        active.Clear();
        int count = Mathf.Clamp(Random.Range(minStreaks, maxStreaks + 1), 1, streakPool.Count);

        for (int i = 0; i < streakPool.Count; i++)
        {
            ParticleSystem ps = streakPool[i];
            bool inUse = i < count;
            ps.gameObject.SetActive(inUse);
            if (!inUse) continue;

            Vector2 side = Random.insideUnitCircle * gustSpreadRadius;
            Streak s = new Streak
            {
                ps = ps,
                t = ps.transform,
                spawnOffset = new Vector3(side.x, RandomHeight(), side.y),
                phase = Random.Range(0f, Mathf.PI * 2f),
                amplitudeScale = Random.Range(0.7f, 1.3f),
                launchDelay = Random.Range(0f, launchSpreadTime),
                arrived = false
            };

            Vector3 startPos = spawnPos + s.spawnOffset;
            s.t.position = startPos;
            s.lastPos = startPos;

            ps.Clear(true);
            ps.Play(true);
            active.Add(s);
        }
    }

    IEnumerator GuideRoutine(Vector3 spawnPos, Transform target, FireWispGuide cfg)
    {
        Vector3 dir = target.position - spawnPos;
        Vector3 forward = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        right.Normalize();

        float elapsed = 0f;
        bool allArrived = false;

        while (!allArrived)
        {
            elapsed += Time.deltaTime;
            allArrived = true;

            float distRemaining = Vector3.Distance(transform.position, target.position);
            float speed = cfg.flySpeed;
            if (distRemaining < cfg.approachSlowdownRadius)
                speed = Mathf.Lerp(cfg.minApproachSpeed, cfg.flySpeed, distRemaining / cfg.approachSlowdownRadius);

            transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

            Vector3 sum = Vector3.zero;
            int activeCount = 0;

            for (int i = 0; i < active.Count; i++)
            {
                Streak s = active[i];
                if (s.ps == null) continue;

                float t = elapsed - s.launchDelay;
                if (t < 0f)
                {
                    allArrived = false;
                    continue; // not launched yet; stays parked at its spawn point
                }

                if (s.arrived)
                {
                    sum += s.t.position;
                    activeCount++;
                    continue;
                }

                float distToTarget = Vector3.Distance(s.t.position, target.position);
                if (distToTarget <= cfg.arriveThreshold)
                {
                    s.arrived = true;
                    s.ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    sum += s.t.position;
                    activeCount++;
                    continue;
                }

                allArrived = false;

                float wave = Mathf.Sin(t * cfg.waveFrequency + s.phase) * cfg.waveAmplitude * s.amplitudeScale;
                float waveS = Mathf.Sin(t * cfg.waveFrequency * sCurveFrequencyScale + s.phase * 0.5f) * cfg.waveAmplitude * 0.4f * s.amplitudeScale;
                float waveUp = Mathf.Sin(t * cfg.waveFrequency * 0.6f + s.phase) * cfg.waveAmplitude * 0.3f * s.amplitudeScale;

                float settle = Mathf.Clamp01(t / Mathf.Max(launchSpreadTime, 0.01f));
                Vector3 offset = Vector3.Lerp(s.spawnOffset, Vector3.zero, settle * 0.6f);

                Vector3 pos = transform.position + offset + right * (wave + waveS) + Vector3.up * waveUp;

                Vector3 moveDir = pos - s.lastPos;
                if (moveDir.sqrMagnitude > 0.0001f)
                    s.t.rotation = Quaternion.LookRotation(moveDir.normalized, Vector3.up);
                s.t.position = pos;
                s.lastPos = pos;

                sum += pos;
                activeCount++;
            }

            if (activeCount > 0 && embersTransform != null)
                embersTransform.position = sum / activeCount;

            yield return null;
        }

        // Fire has arrived: cut the moving light immediately - nothing should glow at the waypoint.
        glowLight.intensity = 0f;
        if (embers != null) embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        yield return new WaitForSeconds(cfg.lingerDuration + cfg.fadeDuration);

        IsBusy = false;
        for (int i = 0; i < streakPool.Count; i++) streakPool[i].gameObject.SetActive(false);
        gameObject.SetActive(false);
    }
}
