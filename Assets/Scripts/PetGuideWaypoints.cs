using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Simple pet guide (koi puzzle nahi).
/// Pet kandhe ke paas -> pehle waypoint pe ja kar rukta hai -> player paas aaye -> agle waypoint pe
/// -> aakhri ke baad wapis kandhe pe.
/// Pet object pe lagao.
/// </summary>
public class PetGuideWaypoints : MonoBehaviour
{
    enum State { Shoulder, Going, Waiting, Returning }

    [Header("Player")]
    public Transform player;
    [Tooltip("Player ke local space mein: x = right, y = up, z = forward")]
    public Vector3 shoulderOffset = new Vector3(0.5f, 1.9f, -0.2f);
    public float followSmooth = 8f;

    [Header("Waypoints")]
    [Tooltip("Order mein: pehle WP_1, phir WP_2 ...")]
    public List<Transform> waypoints = new List<Transform>();
    [Tooltip("Player itna qareeb aaye to pet agle point pe chala jaye")]
    public float reachDistance = 2f;
    [Tooltip("Scene shuru hone ke kitni der baad guide kare")]
    public float startDelay = 1.5f;
    [Tooltip("Aakhri point ke baad dobara pehle se shuru")]
    public bool loop = false;

    [Header("Movement")]
    public float travelSpeed = 4f;
    public float returnSpeed = 7f;
    [Tooltip("Point ke kitna upar tairay")]
    public float hoverHeight = 0.8f;
    public float turnSpeed = 6f;

    [Header("Bobbing")]
    public float bobAmplitude = 0.08f;
    public float bobSpeed = 2f;

    [Header("Hint (optional)")]
    public TMP_Text hintText;
    public string hint = "Follow the flame...";
    public string doneHint = "";

    State state = State.Shoulder;
    int index;
    float startTimer;
    bool finished;

    void Start()
    {
        if (!player)
        {
            var pi = FindAnyObjectByType<PlayerInteractor>();
            if (pi) player = pi.transform;
            else { var p = GameObject.FindWithTag("Player"); if (p) player = p.transform; }
        }
        if (!player) { Debug.LogError("[PetGuide] Player assign nahi hai!"); return; }

        transform.position = ShoulderPos;
        startTimer = startDelay;
        SetHint("");
    }

    void Update()
    {
        if (!player) return;
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;

        switch (state)
        {
            case State.Shoulder:
                FollowShoulder(bob);
                if (finished || waypoints.Count == 0) break;
                startTimer -= Time.deltaTime;
                if (startTimer <= 0f) GoToCurrent();
                break;

            case State.Going:
            {
                Vector3 target = PointPos(index);
                transform.position = Vector3.MoveTowards(transform.position, target, travelSpeed * Time.deltaTime);
                Face(target - transform.position);
                if (Vector3.Distance(transform.position, target) < 0.05f) state = State.Waiting;
                if (PlayerNear(index)) Next();
                break;
            }

            case State.Waiting:
                transform.position = PointPos(index) + Vector3.up * bob;
                Face(player.position - transform.position);
                if (PlayerNear(index)) Next();
                break;

            case State.Returning:
            {
                Vector3 goal = ShoulderPos;
                transform.position = Vector3.MoveTowards(transform.position, goal, returnSpeed * Time.deltaTime);
                Face(goal - transform.position);
                if (Vector3.Distance(transform.position, goal) < 0.15f) state = State.Shoulder;
                break;
            }
        }
    }

    // ================= FLOW =================
    void GoToCurrent()
    {
        // khali points skip
        while (index < waypoints.Count && !waypoints[index]) index++;
        if (index >= waypoints.Count) { Finish(); return; }

        state = State.Going;
        SetHint(hint);
        Debug.Log($"[PetGuide] Waypoint {index + 1}/{waypoints.Count} ki taraf");
    }

    void Next()
    {
        index++;
        if (index >= waypoints.Count)
        {
            if (loop) { index = 0; GoToCurrent(); }
            else Finish();
            return;
        }
        GoToCurrent();
    }

    void Finish()
    {
        finished = true;
        state = State.Returning;
        SetHint(doneHint);
        Debug.Log("[PetGuide] Saare waypoints ho gaye - wapis kandhe pe");
    }

    /// Event se guide dobara shuru karna ho
    public void Restart()
    {
        index = 0;
        finished = false;
        startTimer = 0f;
        state = State.Shoulder;
    }

    // ================= HELPERS =================
    Vector3 ShoulderPos => player.TransformPoint(shoulderOffset);

    Vector3 PointPos(int i) => waypoints[i].position + Vector3.up * hoverHeight;

    bool PlayerNear(int i)
    {
        Vector3 a = player.position; a.y = 0f;
        Vector3 b = waypoints[i].position; b.y = 0f;
        return Vector3.Distance(a, b) <= reachDistance;
    }

    void FollowShoulder(float bob)
    {
        Vector3 goal = ShoulderPos + Vector3.up * bob;
        transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));
        Face(player.forward);
    }

    void Face(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
    }

    void SetHint(string s)
    {
        if (hintText) hintText.text = s;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (!waypoints[i]) continue;
            Gizmos.DrawWireSphere(waypoints[i].position, reachDistance);
            if (i + 1 < waypoints.Count && waypoints[i + 1])
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }
    }
}
