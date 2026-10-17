using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Pet / fairy guide. Player ke shoulder ke paas float karta hai.
/// Har 'guideInterval' seconds baad agle waypoint (puzzle target) pe travel karta hai,
/// wahan rukta hai jab tak player paas na aaye, phir wapis shoulder pe aa jata hai.
/// </summary>
public class PetGuide : MonoBehaviour
{
    enum State { Following, Travelling, Waiting, Returning }

    [Header("Player")]
    public Transform player;
    [Tooltip("Player ke local space mein offset: x = right, y = up, z = forward")]
    public Vector3 shoulderOffset = new Vector3(0.6f, 1.7f, 0.3f);
    public float followSmooth = 8f;

    [Header("Bobbing")]
    public float bobAmplitude = 0.08f;
    public float bobSpeed = 2f;

    [Header("Guiding")]
    [Tooltip("OFF = shuru mein sirf saath chalega. PetGuideTrigger ya StartGuiding() se ON hoga.")]
    public bool guideOnStart = false;
    public float guideInterval = 5f;
    public float travelSpeed = 4f;
    public float returnSpeed = 7f;
    [Tooltip("Player itna qareeb aaye to pet wapis aa jata hai")]
    public float reachDistance = 2f;
    [Tooltip("Target ke upar kitni height pe ruke")]
    public float hoverHeight = 1.0f;
    public float turnSpeed = 6f;

    [Header("Targets")]
    [Tooltip("ON: puzzle manager se smart target (candle / door). OFF: neeche waypoints list order mein.")]
    public bool usePuzzleTargets = true;
    public CandlePuzzleManager puzzle;
    [Tooltip("Fallback / manual waypoints")]
    public List<Transform> waypoints = new List<Transform>();
    public bool loopWaypoints = true;

    [Header("Hint UI (optional)")]
    public TMP_Text hintText;

    State state = State.Following;
    float timer;
    bool guiding;
    Transform target;
    Vector3 targetPos;
    int waypointIndex;

    void Start()
    {
        if (!puzzle) puzzle = CandlePuzzleManager.Instance;
        guiding = guideOnStart;
        SnapToPlayer();
        SetHint("");
    }

    void Update()
    {
        if (!player) return;
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;

        switch (state)
        {
            case State.Following:
            {
                Vector3 goal = ShoulderPos + Vector3.up * bob;
                transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));
                FaceDirection(player.forward);

                if (!guiding) { timer = 0f; break; }   // abhi sirf saath chalo

                timer += Time.deltaTime;
                if (timer >= guideInterval)
                {
                    timer = 0f;
                    if (PickTarget()) state = State.Travelling;
                }
                break;
            }

            case State.Travelling:
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPos, travelSpeed * Time.deltaTime);
                FaceDirection(targetPos - transform.position);

                if (TargetInvalid() || PlayerNearTarget()) { StartReturn(); break; }
                if (Vector3.Distance(transform.position, targetPos) < 0.05f) state = State.Waiting;
                break;
            }

            case State.Waiting:
            {
                transform.position = targetPos + Vector3.up * bob;
                FaceDirection(player.position - transform.position);   // player ko dekhta rahe

                if (TargetInvalid() || PlayerNearTarget()) StartReturn();
                break;
            }

            case State.Returning:
            {
                Vector3 goal = ShoulderPos;
                transform.position = Vector3.MoveTowards(transform.position, goal, returnSpeed * Time.deltaTime);
                FaceDirection(goal - transform.position);

                if (Vector3.Distance(transform.position, goal) < 0.15f)
                {
                    state = State.Following;
                    timer = 0f;
                }
                break;
            }
        }
    }

    // ---------------- Helpers ----------------

    Vector3 ShoulderPos => player.TransformPoint(shoulderOffset);

    bool PickTarget()
    {
        Transform t = null;

        if (usePuzzleTargets && puzzle)
            t = puzzle.GetGuideTarget();

        if (!t && waypoints.Count > 0)
        {
            if (waypointIndex >= waypoints.Count)
            {
                if (!loopWaypoints) return false;
                waypointIndex = 0;
            }
            t = waypoints[waypointIndex++];
        }

        if (!t) return false;

        target = t;
        targetPos = t.position + Vector3.up * hoverHeight;

        // Player pehle se wahan hai to jaane ki zaroorat nahi
        if (PlayerNearTarget()) { target = null; return false; }

        SetHint(HintFor(t));
        return true;
    }

    // Target ab relevant nahi raha (candle jal gayi / grab ho gayi / target change)
    bool TargetInvalid()
    {
        if (!target) return true;
        if (usePuzzleTargets && puzzle && puzzle.GetGuideTarget() != target && !waypoints.Contains(target))
            return true;
        return false;
    }

    bool PlayerNearTarget()
    {
        Vector3 a = player.position; a.y = 0f;
        Vector3 b = targetPos;       b.y = 0f;
        return Vector3.Distance(a, b) <= reachDistance;
    }

    void StartReturn()
    {
        state = State.Returning;
        target = null;
        SetHint("");
    }

    void FaceDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion rot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, turnSpeed * Time.deltaTime);
    }

    string HintFor(Transform t)
    {
        var candle = t.GetComponent<Candle>();
        if (candle) return candle.canBeGrabbed ? "Grab this candle!" : "Light this candle!";
        if (t.GetComponentInParent<LockedDoor>() || (puzzle && puzzle.door && t == puzzle.door.GuidePoint))
            return "The door is open now!";
        return "This way!";
    }

    void SetHint(string s)
    {
        if (hintText) hintText.text = s;
    }

    public bool IsGuiding => guiding;

    /// Guiding shuru karo. delay = pehli dafa jaane se pehle kitne second ruke
    public void StartGuiding(float firstDelay = 1f)
    {
        guiding = true;
        timer = Mathf.Clamp(guideInterval - firstDelay, 0f, guideInterval);
    }

    /// Guiding band, pet wapis kandhe pe aa ke sirf saath chalega
    public void StopGuiding()
    {
        guiding = false;
        if (state != State.Following) StartReturn();
    }

    /// Teleport ke baad pet ko foran shoulder pe le aao
    public void SnapToPlayer()
    {
        if (!player) return;
        transform.position = ShoulderPos;
        state = State.Following;
        timer = 0f;
        target = null;
        SetHint("");
    }
}
