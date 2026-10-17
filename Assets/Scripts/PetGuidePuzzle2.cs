// ===================== PUZZLE 2 =====================
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// PUZZLE 2 ka pet guide (purane PetGuide.cs se alag, dono project mein reh sakte hain).
///
/// LEAD mode (default):
///   Player candle uthaye -> pet foran agle socket pe ja kar ruk jata hai
///   -> candle snap hui -> pet agli candle pe le jata hai -> phir agla socket ... poora puzzle
///   -> complete ke baad portal pe.
/// INTERVAL mode: purana tareeqa (har X sec baad jao, player aaye to wapis kandhe pe).
/// </summary>
public class PetGuidePuzzle2 : MonoBehaviour
{
    public enum Mode { Lead, Interval }
    enum State { Following, Travelling, Waiting, Returning }

    [Header("Mode")]
    public Mode mode = Mode.Lead;

    [Header("Player")]
    public Transform player;
    [Tooltip("Player ke local space mein offset: x = right, y = up, z = forward")]
    public Vector3 shoulderOffset = new Vector3(0.5f, 1.9f, -0.2f);
    public float followSmooth = 8f;

    [Header("Bobbing")]
    public float bobAmplitude = 0.08f;
    public float bobSpeed = 2f;

    [Header("Start")]
    [Tooltip("ON = pehli candle uthane tak pet sirf kandhe pe rahega, phir guiding shuru")]
    public bool startOnFirstGrab = true;
    [Tooltip("OFF = scene shuru hote hi guide (startOnFirstGrab OFF ho tab)")]
    public bool guideOnStart = true;
    [Tooltip("Guide On Start ho to scene shuru ke kitne second baad pet pehli candle ki taraf jaye")]
    public float startDelay = 2f;

    [Header("Movement")]
    public float travelSpeed = 4f;
    public float returnSpeed = 7f;
    [Tooltip("Target ke upar kitni height pe ruke")]
    public float hoverHeight = 0.6f;
    public float turnSpeed = 6f;

    [Header("Interval mode only")]
    public float guideInterval = 5f;
    [Tooltip("Player itna qareeb aaye to pet wapis kandhe pe")]
    public float reachDistance = 2f;

    [Header("Targets")]
    [Tooltip("Khali = khud dhoond lega")]
    public Puzzle2Manager puzzle;
    [Tooltip("Optional: puzzle ka koi target na ho to ye points order mein")]
    public List<Transform> waypoints = new List<Transform>();
    public bool loopWaypoints = false;

    [Header("Hint UI (optional)")]
    public TMP_Text hintText;

    State state = State.Following;
    float timer;
    bool guiding;
    Transform target;
    Vector3 targetPos;
    int waypointIndex;
    PlayerInteractor interactor;

    void Start()
    {
        if (!puzzle) puzzle = FindAnyObjectByType<Puzzle2Manager>();
        if (player) interactor = player.GetComponentInParent<PlayerInteractor>();
        if (!interactor) interactor = FindAnyObjectByType<PlayerInteractor>();

        SnapToPlayer();
        guiding = false;
        startTimer = (!startOnFirstGrab && guideOnStart) ? startDelay : -1f;
    }

    float startTimer = -1f;

    void Update()
    {
        if (!player) return;

        // 'startDelay' sec baad guiding ON (pehle candle ki taraf, phir lead)
        if (!guiding && startTimer >= 0f)
        {
            startTimer -= Time.deltaTime;
            if (startTimer <= 0f) { guiding = true; startTimer = -1f; Debug.Log("[Pet] Guiding shuru - pehli candle ki taraf"); }
        }

        // Pehli candle uthate hi guiding ON
        if (!guiding && startOnFirstGrab && interactor && interactor.HeldCandle)
            guiding = true;

        if (mode == Mode.Lead) UpdateLead();
        else UpdateInterval();
    }

    // =============== LEAD MODE ===============
    void UpdateLead()
    {
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        Transform desired = guiding ? CurrentTarget() : null;

        // Target badal gaya -> foran naye target ki taraf
        if (desired != target)
        {
            target = desired;
            if (target)
            {
                state = State.Travelling;
                SetHint(HintFor(target));
            }
            else
            {
                state = State.Returning;
                SetHint("");
            }
        }

        if (target) targetPos = target.position + Vector3.up * hoverHeight;   // moving target (portal) bhi follow

        switch (state)
        {
            case State.Following:
                FollowShoulder(bob);
                break;

            case State.Travelling:
                transform.position = Vector3.MoveTowards(transform.position, targetPos, travelSpeed * Time.deltaTime);
                FaceDirection(targetPos - transform.position);
                if (Vector3.Distance(transform.position, targetPos) < 0.05f) state = State.Waiting;
                break;

            case State.Waiting:
                // Target pe ruka rahe jab tak kaam na ho jaye (snap / grab)
                transform.position = targetPos + Vector3.up * bob;
                FaceDirection(player.position - transform.position);
                break;

            case State.Returning:
                ReturnToShoulder();
                break;
        }
    }

    Transform CurrentTarget()
    {
        Transform t = puzzle ? puzzle.GetGuideTarget() : null;
        if (t && !t.gameObject.activeInHierarchy) t = null;

        if (!t && waypoints.Count > 0 && waypointIndex < waypoints.Count)
        {
            t = waypoints[waypointIndex];
            // Lead mode: player waypoint pe pahuncha -> agla
            if (t && FlatDistance(player.position, t.position) <= reachDistance)
            {
                waypointIndex++;
                if (waypointIndex >= waypoints.Count && loopWaypoints) waypointIndex = 0;
                t = waypointIndex < waypoints.Count ? waypoints[waypointIndex] : null;
            }
        }
        return t;
    }

    // =============== INTERVAL MODE ===============
    void UpdateInterval()
    {
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;

        switch (state)
        {
            case State.Following:
                FollowShoulder(bob);
                if (!guiding) { timer = 0f; break; }
                timer += Time.deltaTime;
                if (timer >= guideInterval)
                {
                    timer = 0f;
                    if (PickIntervalTarget()) state = State.Travelling;
                }
                break;

            case State.Travelling:
                transform.position = Vector3.MoveTowards(transform.position, targetPos, travelSpeed * Time.deltaTime);
                FaceDirection(targetPos - transform.position);
                if (IntervalTargetInvalid() || PlayerNear(targetPos)) { BeginReturn(); break; }
                if (Vector3.Distance(transform.position, targetPos) < 0.05f) state = State.Waiting;
                break;

            case State.Waiting:
                transform.position = targetPos + Vector3.up * bob;
                FaceDirection(player.position - transform.position);
                if (IntervalTargetInvalid() || PlayerNear(targetPos)) BeginReturn();
                break;

            case State.Returning:
                ReturnToShoulder();
                break;
        }
    }

    bool PickIntervalTarget()
    {
        Transform t = puzzle ? puzzle.GetGuideTarget() : null;

        if (!t && waypoints.Count > 0)
        {
            if (waypointIndex >= waypoints.Count)
            {
                if (!loopWaypoints) return false;
                waypointIndex = 0;
            }
            t = waypoints[waypointIndex++];
        }

        if (!t || !t.gameObject.activeInHierarchy) return false;

        target = t;
        targetPos = t.position + Vector3.up * hoverHeight;
        if (PlayerNear(targetPos)) { target = null; return false; }

        SetHint(HintFor(t));
        return true;
    }

    bool IntervalTargetInvalid()
    {
        if (!target) return true;
        return puzzle && !waypoints.Contains(target) && puzzle.GetGuideTarget() != target;
    }

    // =============== SHARED ===============
    Vector3 ShoulderPos => player.TransformPoint(shoulderOffset);

    void FollowShoulder(float bob)
    {
        Vector3 goal = ShoulderPos + Vector3.up * bob;
        transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));
        FaceDirection(player.forward);
    }

    void ReturnToShoulder()
    {
        Vector3 goal = ShoulderPos;
        transform.position = Vector3.MoveTowards(transform.position, goal, returnSpeed * Time.deltaTime);
        FaceDirection(goal - transform.position);
        if (Vector3.Distance(transform.position, goal) < 0.15f)
        {
            state = State.Following;
            timer = 0f;
        }
    }

    void BeginReturn()
    {
        state = State.Returning;
        target = null;
        SetHint("");
    }

    bool PlayerNear(Vector3 p) => FlatDistance(player.position, p) <= reachDistance;

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void FaceDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
    }

    string HintFor(Transform t)
    {
        if (puzzle && !waypoints.Contains(t))
        {
            string h = puzzle.GetGuideHint(t);
            if (!string.IsNullOrEmpty(h)) return h;
        }
        return "This way!";
    }

    void SetHint(string s)
    {
        if (hintText) hintText.text = s;
    }

    // =============== PUBLIC ===============
    public void StartGuiding(float firstDelay = 1f)
    {
        guiding = true;
        timer = Mathf.Clamp(guideInterval - firstDelay, 0f, guideInterval);
    }

    public void StopGuiding()
    {
        guiding = false;
        BeginReturn();
    }

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
