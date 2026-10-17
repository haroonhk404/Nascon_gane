using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// Created automatically by FireLineGuide. Do NOT add this by hand.
/// One gust = several thin fire trails flying on wavy paths to a target.
public class FireLineGust : MonoBehaviour
{
    class Line
    {
        public Transform tr;
        public TrailRenderer trail;
        public Vector3 start, end, side;
        public float delay, duration, phase, elapsed, waveScale;
        public bool flying, done;
        public ParticleSystem[] fx; // optional burn effect on the line head
    }

    readonly List<Line> lines = new List<Line>();
    FireLineGuide s;

    public bool Busy { get; private set; }

    public void Init(FireLineGuide settings) { s = settings; }

    public void Launch(Vector3 center, Vector3 target)
    {
        gameObject.SetActive(true);
        Busy = true;

        int count = Random.Range(s.minLines, s.maxLines + 1);
        while (lines.Count < count) lines.Add(CreateLine());

        Vector3 dir = target - center;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, dir);

        for (int i = 0; i < lines.Count; i++)
        {
            Line l = lines[i];
            bool use = i < count;
            l.tr.gameObject.SetActive(use);
            l.done = !use;
            if (!use) continue;

            float h = s.RandomHeight();
            float lateral = Random.Range(-s.lateralSpread, s.lateralSpread);
            l.side = side;
            l.phase = Random.Range(0f, Mathf.PI * 2f);

            // Try the random path first; if a wall is in the way, make it
            // narrower, lower and straighter. If still blocked, skip this line.
            bool found = false;
            float[] shrink = { 1f, 0.6f, 0.3f, 0f };
            foreach (float f in shrink)
            {
                float hh = Mathf.Lerp(1.2f, h, f); // pull toward chest height
                l.start = center + side * (lateral * f) + Vector3.up * hh;
                l.end = target + side * (lateral * 0.3f * f) + Vector3.up * hh;
                l.waveScale = f;
                if (!s.avoidWalls || PathIsClear(l)) { found = true; break; }
            }
            if (!found)
            {
                l.done = true;
                l.tr.gameObject.SetActive(false);
                continue;
            }

            l.delay = Random.Range(0f, s.launchSpread);
            l.duration = Mathf.Max(0.2f, Vector3.Distance(l.start, l.end) / Mathf.Max(0.1f, s.flySpeed));
            l.elapsed = 0f;
            l.flying = false;

            ApplyTrailSettings(l.trail);
            l.trail.emitting = false;
            l.tr.position = l.start;
            l.trail.Clear();
            StopFx(l, true);
        }
    }

    void Update()
    {
        if (!Busy) return;

        bool anyAlive = false;
        float dt = Time.deltaTime;

        foreach (Line l in lines)
        {
            if (l.done) continue;
            anyAlive = true;

            l.elapsed += dt;
            float flyTime = l.elapsed - l.delay;
            if (flyTime < 0f) continue; // waiting for its delay

            if (!l.flying)
            {
                l.flying = true;
                l.tr.position = l.start;
                l.trail.Clear();
                l.trail.emitting = true;
                PlayFx(l);
            }

            float t = flyTime / l.duration;
            if (t < 1f)
            {
                float eased = 1f - (1f - t) * (1f - t); // slows down near the target
                l.tr.position = PathPoint(l, eased);
            }
            else
            {
                // Arrived: stop drawing, the tail shrinks away within trailTime
                if (l.trail.emitting)
                {
                    l.tr.position = l.end;
                    l.trail.emitting = false;
                    StopFx(l, false); // stop new flames, let existing ones fade
                }
                if (flyTime - l.duration >= l.trail.time)
                {
                    l.trail.Clear();
                    l.done = true;
                }
            }
        }

        if (!anyAlive)
        {
            Busy = false;
            gameObject.SetActive(false);
        }
    }

    Vector3 PathPoint(Line l, float t)
    {
        Vector3 p = Vector3.Lerp(l.start, l.end, t);
        float envelope = Mathf.Sin(t * Mathf.PI) * l.waveScale; // no wave at start and end
        float w = Mathf.Sin(t * Mathf.PI * 2f * s.waveFrequency + l.phase);
        float v = Mathf.Cos(t * Mathf.PI * 2f * s.waveFrequency * 0.7f + l.phase);
        p += l.side * (w * s.waveAmplitude * envelope);
        p += Vector3.up * (v * s.waveAmplitude * 0.4f * envelope);
        return p;
    }

    // Samples the wavy path and checks each piece for walls
    bool PathIsClear(Line l)
    {
        Vector3 prev = l.start;
        for (int i = 1; i <= s.pathSamples; i++)
        {
            Vector3 p = PathPoint(l, i / (float)s.pathSamples);
            if (Physics.Linecast(prev, p, s.obstacleLayers, QueryTriggerInteraction.Ignore))
                return false;
            prev = p;
        }
        return true;
    }

    Line CreateLine()
    {
        var go = new GameObject("FireLine");
        go.transform.SetParent(transform, false);
        var trail = go.AddComponent<TrailRenderer>();
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.minVertexDistance = 0.05f;
        trail.numCapVertices = 2;
        trail.emitting = false;

        var line = new Line { tr = go.transform, trail = trail };

        // Optional small fire riding on the head of the line
        if (s.headFlamePrefab != null)
        {
            GameObject fxGo = Instantiate(s.headFlamePrefab, go.transform);
            fxGo.transform.localPosition = Vector3.zero;
            fxGo.transform.localRotation = Quaternion.identity;
            fxGo.transform.localScale = Vector3.one * s.headFlameScale;

            if (s.stripLightsAndSound)
            {
                foreach (var lt in fxGo.GetComponentsInChildren<Light>(true)) lt.enabled = false;
                foreach (var au in fxGo.GetComponentsInChildren<AudioSource>(true)) au.enabled = false;
            }

            line.fx = fxGo.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in line.fx)
            {
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World; // flames trail behind
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;     // respects the small scale
                main.playOnAwake = false;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
        return line;
    }

    static void PlayFx(Line l)
    {
        if (l.fx == null) return;
        foreach (var ps in l.fx) ps.Play(true);
    }

    static void StopFx(Line l, bool clear)
    {
        if (l.fx == null) return;
        var mode = clear ? ParticleSystemStopBehavior.StopEmittingAndClear
                         : ParticleSystemStopBehavior.StopEmitting;
        foreach (var ps in l.fx) ps.Stop(true, mode);
    }

    void ApplyTrailSettings(TrailRenderer trail)
    {
        trail.time = s.trailTime;
        trail.widthMultiplier = 1f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, s.lineWidth), new Keyframe(1f, 0f));
        trail.colorGradient = WithOpacity(s.lineColor, s.opacity);
        trail.material = s.lineMaterial;
    }

    static Gradient WithOpacity(Gradient src, float opacity)
    {
        var alphas = src.alphaKeys;
        for (int i = 0; i < alphas.Length; i++) alphas[i].alpha *= opacity;
        var g = new Gradient();
        g.SetKeys(src.colorKeys, alphas);
        return g;
    }
}
