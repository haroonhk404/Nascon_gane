using System.Collections.Generic;
using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Player pe lagao. Camera se ray/sphere cast karta hai, prompt text dikhata hai,
/// E press pe Interact call karta hai, aur haath mein candle hold karta hai.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    [Tooltip("Camera ka child empty object - jahan candle haath mein dikhegi (right-bottom)")]
    public Transform handSocket;
    [Tooltip("Screen center-bottom wala text: '[E] Grab', 'Door is Locked' etc.")]
    public TMP_Text promptText;
    [Tooltip("Thori der ke liye aane wala message text (optional)")]
    public TMP_Text messageText;
    [Tooltip("Ye objects ray ko block nahi karenge (e.g. Pet)")]
    public List<Transform> ignoreObjects = new List<Transform>();

    [Header("Settings")]
    public float interactDistance = 4f;
    [Tooltip("Chhoti cheezon (candle) pe aim aasaan karne ke liye. 0 = simple ray")]
    public float aimRadius = 0.2f;
    public LayerMask interactMask = ~0;
    public KeyCode interactKey = KeyCode.E;
    public float messageDuration = 2f;

    [Header("Proximity (aim na bhi ho to paas wali cheez)")]
    public bool useProximity = true;
    [Tooltip("Player ke itne paas interactable ho to prompt aa jaye (upar latki candles ke liye bara rakho)")]
    public float proximityRadius = 2.5f;
    [Tooltip("Player ke center ki height jahan se proximity check hota hai")]
    public float proximityHeight = 1.2f;

    [Header("Debug")]
    [Tooltip("ON karo to Console mein dikhega ray kis object ko hit kar rahi hai")]
    public bool debugMode = true;

    public Candle HeldCandle { get; private set; }

    IInteractable current;
    float messageTimer;
    Collider lastDebugHit;

    void Awake()
    {
        if (!playerCamera) playerCamera = Camera.main;
        if (!playerCamera) Debug.LogError("PlayerInteractor: Camera assign nahi hai aur koi MainCamera tag wala camera bhi nahi mila!");
        if (!promptText) Debug.LogWarning("PlayerInteractor: Prompt Text assign nahi hai - text screen pe nahi aayega.");
        if (promptText) promptText.text = "";
        if (messageText) messageText.text = "";
    }

    void Update()
    {
        if (!playerCamera) return;

        current = FindInteractable(out Collider hitCol);

        // Aim se kuch na mila (ya mila magar uska prompt khali hai) -> paas wali cheez dhoondo
        if (useProximity && (current == null || string.IsNullOrEmpty(current.GetPrompt(this))))
        {
            var near = FindNearby(out Collider nearCol);
            if (near != null) { current = near; hitCol = nearCol; }
        }

        if (debugMode && hitCol != lastDebugHit)
        {
            lastDebugHit = hitCol;
            if (hitCol)
                Debug.Log($"[Interactor] Hit: {hitCol.name}  | Interactable: {(current != null ? ((MonoBehaviour)current).name : "NONE")}", hitCol);
            else
                Debug.Log("[Interactor] Hit: nothing");
        }

        string prompt = current != null ? current.GetPrompt(this) : "";
        if (promptText) promptText.text = prompt;

        if (current != null && InteractPressed())
            current.Interact(this);

        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f && messageText) messageText.text = "";
        }
    }

    IInteractable FindInteractable(out Collider firstHit)
    {
        firstHit = null;
        Vector3 origin = playerCamera.transform.position;
        Vector3 dir = playerCamera.transform.forward;

        if (debugMode) Debug.DrawRay(origin, dir * interactDistance, Color.yellow);

        RaycastHit[] hits = aimRadius > 0f
            ? Physics.SphereCastAll(origin, aimRadius, dir, interactDistance, interactMask, QueryTriggerInteraction.Collide)
            : Physics.RaycastAll(origin, dir, interactDistance, interactMask, QueryTriggerInteraction.Collide);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (ShouldIgnore(h.collider)) continue;

            if (!firstHit) firstHit = h.collider;

            var inter = h.collider.GetComponentInParent<IInteractable>();
            if (inter != null) return inter;

            // Solid (non-trigger) cheez beech mein aa gayi (wall) to ruk jao
            if (!h.collider.isTrigger) return null;
        }
        return null;
    }

    IInteractable FindNearby(out Collider bestCol)
    {
        bestCol = null;
        IInteractable best = null;
        float bestScore = float.MaxValue;

        Vector3 center = transform.position + Vector3.up * proximityHeight;
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();

        var cols = Physics.OverlapSphere(center, proximityRadius, interactMask, QueryTriggerInteraction.Collide);
        foreach (var c in cols)
        {
            if (ShouldIgnore(c)) continue;
            var inter = c.GetComponentInParent<IInteractable>();
            if (inter == null) continue;
            if (string.IsNullOrEmpty(inter.GetPrompt(this))) continue;   // jo already lit hai woh skip

            Vector3 to = c.bounds.center - center;
            float dist = to.magnitude;
            Vector3 flat = to; flat.y = 0f;
            float facing = flat.sqrMagnitude > 0.0001f ? Vector3.Dot(fwd, flat.normalized) : 1f;

            // Qareeb + saamne wali cheez ko priority
            float score = dist + (1f - facing) * 1.5f;
            if (score < bestScore) { bestScore = score; best = inter; bestCol = c; }
        }
        return best;
    }

    void OnDrawGizmosSelected()
    {
        if (!useProximity) return;
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * proximityHeight, proximityRadius);
    }

    bool ShouldIgnore(Collider c)
    {
        Transform t = c.transform;
        if (t.IsChildOf(transform)) return true;                 // player ka apna collider / haath wali candle
        if (t.IsChildOf(playerCamera.transform)) return true;
        foreach (var ig in ignoreObjects)
            if (ig && t.IsChildOf(ig)) return true;              // pet waghera
        return false;
    }

    // New Input System aur Old Input dono pe chalta hai
    bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            var key = Keyboard.current.FindKeyOnCurrentKeyboardLayout(interactKey.ToString());
            if (key != null) return key.wasPressedThisFrame;
            return Keyboard.current.eKey.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(interactKey);
#endif
    }

    public void ShowMessage(string msg)
    {
        if (!messageText) { Debug.Log(msg); return; }
        messageText.text = msg;
        messageTimer = messageDuration;
    }

    public void HoldCandle(Candle candle) => HeldCandle = candle;
}
