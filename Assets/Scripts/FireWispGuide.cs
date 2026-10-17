using System.Collections.Generic;
using UnityEngine;

/// Finds Cube1, Cube2, ... (until one is missing) plus a final target, and periodically
/// sends a pooled FireWisp to fly ahead and show the player where to go next.
[DisallowMultipleComponent]
public class FireWispGuide : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Player transform. Auto-found by tag 'Player' if left empty.")]
    public Transform player;
    [Tooltip("FireWisp prefab. Root must have a FireWisp component.")]
    public GameObject wispPrefab;

    [Header("Waypoint Path")]
    [Tooltip("Waypoints are found in the scene as <prefix>1, <prefix>2, ... until a number is missing.")]
    public string waypointPrefix = "Cube";
    [Tooltip("Name of the final destination object guided to after the last numbered waypoint.")]
    public string finalTargetName = "PortalEight";

    [Header("Progress")]
    [Tooltip("Distance to a waypoint that counts as 'reached'. Progress never goes backwards.")]
    public float arrivalRadius = 3f;

    [Header("Guide Timing")]
    [Tooltip("Send a wisp at least this often while a waypoint is still ahead.")]
    public float guideInterval = 4f;
    [Tooltip("If the player stands still this long, send a wisp immediately.")]
    public float idleTriggerTime = 2f;
    [Tooltip("Player speed (m/s) below this counts as 'standing still'.")]
    public float idleSpeedThreshold = 0.15f;

    [Header("Wisp Pool")]
    [Tooltip("Pooled wisp instances (no runtime Instantiate/Destroy once warmed up).")]
    public int poolSize = 2;

    [Header("Wisp Flight")]
    [Tooltip("Gusts spawn ahead of the player, toward the next waypoint, between these distances.")]
    public float spawnDistanceMin = 1.5f;
    public float spawnDistanceMax = 2.5f;
    public float flySpeed = 6f;
    public float approachSlowdownRadius = 2.5f;
    public float minApproachSpeed = 1.5f;
    public float arriveThreshold = 0.35f;
    [Tooltip("Side-to-side sway amplitude, like wind.")]
    public float waveAmplitude = 0.6f;
    [Tooltip("Side-to-side sway frequency.")]
    public float waveFrequency = 2f;
    [Tooltip("How long the wisp lingers at the target before fading out.")]
    public float lingerDuration = 1f;
    public float fadeDuration = 0.6f;

    readonly List<Transform> waypoints = new List<Transform>();
    readonly List<FireWisp> pool = new List<FireWisp>();

    int nextWaypointIndex;
    float periodicTimer;
    float idleTimer;
    Vector3 lastPlayerPos;
    bool hasFinished;

    void Awake()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        BuildWaypointList();

        if (player != null) lastPlayerPos = player.position;

        for (int i = 0; i < poolSize; i++)
        {
            FireWisp member = SpawnPoolMember();
            if (member != null) pool.Add(member);
        }
    }

    void BuildWaypointList()
    {
        waypoints.Clear();

        int i = 1;
        while (true)
        {
            GameObject go = GameObject.Find(waypointPrefix + i);
            if (go == null) break;
            waypoints.Add(go.transform);
            i++;
        }

        if (!string.IsNullOrEmpty(finalTargetName))
        {
            GameObject finalGo = GameObject.Find(finalTargetName);
            if (finalGo != null) waypoints.Add(finalGo.transform);
        }
    }

    FireWisp SpawnPoolMember()
    {
        if (wispPrefab == null) return null;
        GameObject go = Instantiate(wispPrefab, transform.position, Quaternion.identity, transform);
        go.SetActive(false);
        return go.GetComponent<FireWisp>();
    }

    void Update()
    {
        if (player == null || waypoints.Count == 0) return;

        CheckArrival();
        if (hasFinished) return;

        float dt = Time.deltaTime;
        float movedSqr = (player.position - lastPlayerPos).sqrMagnitude;
        float speed = Mathf.Sqrt(movedSqr) / Mathf.Max(dt, 0.0001f);
        lastPlayerPos = player.position;

        idleTimer = speed <= idleSpeedThreshold ? idleTimer + dt : 0f;
        periodicTimer += dt;

        bool wantGuide = idleTimer >= idleTriggerTime || periodicTimer >= guideInterval;
        if (wantGuide && TrySendGuide())
        {
            periodicTimer = 0f;
            idleTimer = 0f;
        }
    }

    void CheckArrival()
    {
        if (nextWaypointIndex >= waypoints.Count)
        {
            hasFinished = true;
            return;
        }

        Transform target = waypoints[nextWaypointIndex];
        if (target == null)
        {
            nextWaypointIndex++;
            return;
        }

        float dist = Vector3.Distance(player.position, target.position);
        if (dist <= arrivalRadius)
            nextWaypointIndex++; // monotonic: progress never goes backwards

        if (nextWaypointIndex >= waypoints.Count)
            hasFinished = true;
    }

    bool TrySendGuide()
    {
        if (nextWaypointIndex >= waypoints.Count) return false;

        FireWisp wisp = GetFreeWisp();
        if (wisp == null) return false;

        Transform target = waypoints[nextWaypointIndex];

        Vector3 toTarget = target.position - player.position;
        toTarget.y = 0f;
        Vector3 dir = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : player.forward;
        float spawnDistance = Random.Range(spawnDistanceMin, spawnDistanceMax);
        Vector3 spawnPos = player.position + dir * spawnDistance;

        wisp.BeginGuide(spawnPos, target, this);
        return true;
    }

    FireWisp GetFreeWisp()
    {
        for (int i = 0; i < pool.Count; i++)
            if (pool[i] != null && !pool[i].IsBusy) return pool[i];
        return null;
    }
}
