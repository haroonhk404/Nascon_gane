using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Creep AI (standalone).
/// - Waypoints pe patrol (loop)
/// - Player radius mein aaye -> FORAN chase + attack (1 hit = maut)
/// - Radius mein player 'stillTimeToLose' sec (2) tak na chale -> creep chhod de, attack nahi, wapis patrol
/// - Ruka hua player radius mein phir chale -> creep phir chase
/// NavMeshAgent ho to NavMesh pe chalta hai, warna seedha.
/// </summary>
public class CreepPatrolAI : MonoBehaviour
{
    enum State { Patrol, Wait, Chase, Attack, Dead }

    [Header("Patrol")]
    public List<Transform> waypoints = new List<Transform>();
    public float walkSpeed = 1.8f;
    [Tooltip("Har waypoint pe kitni der ruke")]
    public float waitAtWaypoint = 1.5f;

    [Header("Detection")]
    [Tooltip("Player is radius mein aaye to attack")]
    public float detectRadius = 6f;
    [Tooltip("Radius mein itne second player na chale to creep chhod de")]
    public float stillTimeToLose = 2f;
    [Tooltip("Player is speed (m/s) se tez chale to 'chal raha hai'. Camera ghumana / halka hilna maaf")]
    public float moveThreshold = 0.3f;

    [Header("Chase / Attack")]
    public float runSpeed = 6f;
    public float attackRange = 1.6f;
    [Tooltip("Attack animation ke kitni der baad hit lage")]
    public float attackHitDelay = 0.4f;
    public float turnSpeed = 8f;

    [Header("Animator (optional, naam khali = skip)")]
    public Animator animator;
    [Tooltip("Float parameter jo speed le, jaise 'Speed'")]
    public string speedParam = "Speed";
    [Tooltip("Attack trigger, jaise 'Attack'")]
    public string attackTrigger = "Attack";

    [Header("Player")]
    [Tooltip("Khali = 'Player' tag wala ya PlayerInteractor wala object")]
    public Transform player;
    [Tooltip("Marne pe band: ThirdPersonController, ThirdPersonCamera, PlayerInteractor")]
    public MonoBehaviour[] disableOnDeath;

    [Header("Touch = Game Over")]
    [Tooltip("ON = creep ke jism (collider) ko chhoote hi game over")]
    public bool killOnTouch = true;
    [Tooltip("Collider se kitna qareeb = 'touch' (meter)")]
    public float touchPadding = 0.3f;

    [Header("Death")]
    public float deathFadeTime = 1.5f;
    [Tooltip("Khali = isi scene ka restart")]
    public string sceneOnDeath = "";
    [Tooltip("Optional: GAME OVER panel (Restart / Menu buttons wala). Khali = fade ke baad seedha restart")]
    public GameObject gameOverPanel;
    [Tooltip("Game Over panel ke 'Main Menu' button ke liye scene naam")]
    public string mainMenuScene = "main menu";
    public UnityEvent onPlayerKilled;

    [Header("Audio (optional)")]
    public AudioSource audioSource;
    public AudioClip chaseClip;
    public AudioClip attackClip;

    [Header("Debug")]
    public bool logs = true;

    State state = State.Patrol;
    NavMeshAgent agent;
    int wp;
    float waitTimer;
    float stillTimer;
    bool ignoringPlayer;     // ruk jaane ki wajah se creep ne chhod diya
    Vector3 lastPlayerPos;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        if (!player)
        {
            var tagged = GameObject.FindWithTag("Player");
            if (tagged) player = tagged.transform;
            else
            {
                var pi = FindAnyObjectByType<PlayerInteractor>();
                if (pi) player = pi.transform;
            }
        }
        if (!player) Debug.LogError("[Creep] Player nahi mila! Inspector mein Player assign karo.");
        else lastPlayerPos = player.position;

        if (waypoints.Count == 0) Debug.LogWarning("[Creep] Waypoints khali hain - creep sirf khara rahega.");
        else if (waypoints[0]) Teleport(waypoints[0].position);

        CheckAnimator();

        bodyColliders.Clear();
        foreach (var c in GetComponentsInChildren<Collider>()) bodyColliders.Add(c);
        if (killOnTouch && bodyColliders.Count == 0)
            Debug.LogWarning("[Creep] Creep pe koi Collider nahi - touch detect nahi hoga. Capsule/Box Collider lagao.");

        if (gameOverPanel) gameOverPanel.SetActive(false);
    }

    readonly List<Collider> bodyColliders = new List<Collider>();

    /// Player creep ke collider ko chhoo raha hai?
    bool PlayerTouching()
    {
        if (!killOnTouch || !player) return false;
        Vector3 feet = player.position + Vector3.up * 0.2f;
        Vector3 chest = player.position + Vector3.up * 1.2f;
        foreach (var c in bodyColliders)
        {
            if (!c || !c.enabled) continue;
            if ((c.ClosestPoint(feet) - feet).sqrMagnitude <= touchPadding * touchPadding) return true;
            if ((c.ClosestPoint(chest) - chest).sqrMagnitude <= touchPadding * touchPadding) return true;
        }
        return false;
    }

    /// Animator ki jaanch: kitne hain, controller laga hai, parameters sahi hain
    void CheckAnimator()
    {
        var all = GetComponentsInChildren<Animator>(true);
        if (all.Length > 1)
        {
            string names = "";
            foreach (var a in all)
                names += $"\n   - '{a.gameObject.name}'  controller: {(a.runtimeAnimatorController ? a.runtimeAnimatorController.name : "NONE")}";
            Debug.LogWarning($"[Creep] {all.Length} Animator mile! Sirf EK hona chahiye (jis object mein haddiyan/bones hain). Baqi Remove karo:{names}");
        }

        // Assigned animator pe controller nahi -> controller wala dhoondo
        if (!animator || !animator.runtimeAnimatorController)
        {
            foreach (var a in all)
                if (a.runtimeAnimatorController) { animator = a; break; }
        }

        if (!animator) { Debug.LogError("[Creep] Koi Animator nahi mila - animations nahi chalengi"); return; }
        if (!animator.runtimeAnimatorController) { Debug.LogError($"[Creep] '{animator.name}' ke Animator mein Controller NAHI laga"); return; }
        if (animator.applyRootMotion) { animator.applyRootMotion = false; Debug.Log("[Creep] Apply Root Motion OFF kar diya"); }

        Debug.Log($"[Creep] Animator: '{animator.gameObject.name}', Controller: '{animator.runtimeAnimatorController.name}'");

        CheckParam(speedParam, AnimatorControllerParameterType.Float);
        CheckParam(attackTrigger, AnimatorControllerParameterType.Trigger);
    }

    void CheckParam(string pName, AnimatorControllerParameterType type)
    {
        if (string.IsNullOrEmpty(pName)) return;
        string existing = "";
        foreach (var p in animator.parameters)
        {
            existing += $" '{p.name}'({p.type})";
            if (p.name == pName)
            {
                if (p.type != type) Debug.LogError($"[Creep] Parameter '{pName}' ka type {p.type} hai, {type} hona chahiye");
                return;
            }
        }
        Debug.LogError($"[Creep] Animator mein '{pName}' parameter NAHI hai. Mojood:{existing}");
    }

    void Update()
    {
        if (!player || state == State.Dead) { UpdateAnimator(); return; }

        // Chhoote hi game over (attack ke dauran bhi)
        if (PlayerTouching())
        {
            if (logs) Debug.Log("[Creep] Player ne creep ko chhua - GAME OVER");
            StopAllCoroutines();
            StartCoroutine(TouchKill());
            return;
        }

        if (state == State.Attack) { UpdateAnimator(); return; }

        // Player ki speed (zameen pe)
        Vector3 p = player.position;
        Vector3 d = p - lastPlayerPos; d.y = 0f;
        float playerSpeed = Time.deltaTime > 0f ? d.magnitude / Time.deltaTime : 0f;
        lastPlayerPos = p;
        bool moving = playerSpeed > moveThreshold;

        float dist = Flat(transform.position, p);
        bool inRadius = dist <= detectRadius;

        // Radius se bahar gaya ya phir chalne laga -> "ignore" khatam
        if (!inRadius || moving) ignoringPlayer = false;

        switch (state)
        {
            case State.Patrol:
            case State.Wait:
                if (inRadius && !ignoringPlayer) { StartChase(); break; }

                if (state == State.Patrol) Patrol();
                else
                {
                    waitTimer -= Time.deltaTime;
                    if (waitTimer <= 0f) { NextWaypoint(); state = State.Patrol; }
                }
                break;

            case State.Chase:
                // Ruka hua player -> 2 sec baad chhod do
                if (!moving) stillTimer += Time.deltaTime; else stillTimer = 0f;
                if (stillTimer >= stillTimeToLose)
                {
                    if (logs) Debug.Log($"[Creep] Player {stillTimeToLose} sec ruka raha - chhod diya, wapis patrol");
                    ignoringPlayer = true;
                    BackToPatrol();
                    break;
                }

                // Radius se bahut door nikal gaya -> chhod do
                if (dist > detectRadius * 2f)
                {
                    if (logs) Debug.Log("[Creep] Player door nikal gaya - wapis patrol");
                    BackToPatrol();
                    break;
                }

                MoveTo(p, runSpeed);
                Face(p);

                if (dist <= attackRange) StartCoroutine(AttackRoutine());
                break;
        }

        UpdateAnimator();
    }

    // ================= PATROL =================
    void Patrol()
    {
        if (waypoints.Count == 0) { Stop(); return; }
        Transform t = waypoints[wp];
        if (!t) { NextWaypoint(); return; }

        MoveTo(t.position, walkSpeed);
        Face(t.position);

        if (Flat(transform.position, t.position) < 0.5f)
        {
            Stop();
            state = State.Wait;
            waitTimer = waitAtWaypoint;
        }
    }

    void NextWaypoint()
    {
        if (waypoints.Count > 0) wp = (wp + 1) % waypoints.Count;
    }

    void BackToPatrol()
    {
        stillTimer = 0f;
        state = State.Patrol;
        // Sab se qareeb waypoint se patrol dobara shuru
        float best = float.MaxValue;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (!waypoints[i]) continue;
            float dd = Flat(transform.position, waypoints[i].position);
            if (dd < best) { best = dd; wp = i; }
        }
    }

    // ================= CHASE / ATTACK =================
    void StartChase()
    {
        if (logs) Debug.Log("[Creep] Player radius mein - ATTACK!");
        state = State.Chase;
        stillTimer = 0f;
        if (audioSource && chaseClip) audioSource.PlayOneShot(chaseClip);
    }

    IEnumerator AttackRoutine()
    {
        state = State.Attack;
        Stop();
        Face(player.position, true);
        if (animator && !string.IsNullOrEmpty(attackTrigger)) animator.SetTrigger(attackTrigger);
        if (audioSource && attackClip) audioSource.PlayOneShot(attackClip);

        yield return new WaitForSeconds(attackHitDelay);

        if (Flat(transform.position, player.position) <= attackRange + 0.8f)
        {
            yield return KillPlayer();
        }
        else
        {
            state = State.Chase;   // bach nikla -> chase jaari
        }
    }

    IEnumerator TouchKill()
    {
        state = State.Attack;
        Stop();
        Face(player.position, true);
        if (animator && !string.IsNullOrEmpty(attackTrigger)) animator.SetTrigger(attackTrigger);
        if (audioSource && attackClip) audioSource.PlayOneShot(attackClip);
        yield return KillPlayer();
    }

    IEnumerator KillPlayer()
    {
        if (state == State.Dead) yield break;
        state = State.Dead;
        Stop();
        if (logs) Debug.Log("[Creep] Player mara gaya");
        foreach (var m in disableOnDeath) if (m) m.enabled = false;
        onPlayerKilled?.Invoke();

        // Black fade
        // Game Over panel ho to apna overlay (panel ke neeche rakh sakte hain), warna ScreenFader
        var fader = gameOverPanel ? null : ScreenFader.Instance;
        CanvasGroup overlay = null;
        if (fader) yield return fader.FadeOut(deathFadeTime);
        else { overlay = CreateOverlay(); yield return FadeGroup(overlay, deathFadeTime); }

        yield return new WaitForSecondsRealtime(0.5f);

        // Game Over panel ho to dikhao, warna seedha restart
        if (gameOverPanel)
        {
            if (overlay)
            {
                overlay.alpha = 0.7f;   // halka kaala peeche rahe
                var panelCanvas = gameOverPanel.GetComponentInParent<Canvas>(true);
                if (panelCanvas) overlay.GetComponent<Canvas>().sortingOrder = panelCanvas.rootCanvas.sortingOrder - 1;
            }
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            yield break;
        }

        RestartLevel();
    }

    // ===== Game Over panel ke buttons (On Click mein lagao) =====
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(sceneOnDeath) && Application.CanStreamedLevelBeLoaded(sceneOnDeath))
            SceneManager.LoadScene(sceneOnDeath);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (Application.CanStreamedLevelBeLoaded(mainMenuScene)) SceneManager.LoadScene(mainMenuScene);
        else { Debug.LogError($"[Creep] Scene '{mainMenuScene}' Scene List mein nahi"); RestartLevel(); }
    }

    // ================= MOVEMENT =================
    void MoveTo(Vector3 target, float speed)
    {
        if (agent && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = speed;
            agent.SetDestination(target);
        }
        else
        {
            Vector3 t = target; t.y = transform.position.y;
            transform.position = Vector3.MoveTowards(transform.position, t, speed * Time.deltaTime);
        }
    }

    void Stop()
    {
        if (agent && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
    }

    void Teleport(Vector3 pos)
    {
        if (agent && agent.enabled && agent.isOnNavMesh) agent.Warp(pos);
        else transform.position = pos;
    }

    void Face(Vector3 target, bool instant = false)
    {
        Vector3 dir = target - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion r = Quaternion.LookRotation(dir);
        transform.rotation = instant ? r : Quaternion.Slerp(transform.rotation, r, turnSpeed * Time.deltaTime);
    }

    void UpdateAnimator()
    {
        if (!animator || string.IsNullOrEmpty(speedParam)) return;
        float s = state == State.Patrol ? walkSpeed : state == State.Chase ? runSpeed : 0f;
        animator.SetFloat(speedParam, s);
    }

    static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    IEnumerator FadeGroup(CanvasGroup g, float duration)
    {
        float t = 0f;
        while (t < duration) { t += Time.unscaledDeltaTime; g.alpha = Mathf.Clamp01(t / duration); yield return null; }
        g.alpha = 1f;
    }

    CanvasGroup CreateOverlay()
    {
        var go = new GameObject("DeathFade");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;   // Game Over panel (agar alag canvas pe sort order zyada) iske upar aa sake
        var group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        var img = new GameObject("Black").AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        img.color = Color.black;
        img.raycastTarget = false;
        img.rectTransform.anchorMin = Vector2.zero;
        img.rectTransform.anchorMax = Vector2.one;
        img.rectTransform.offsetMin = Vector2.zero;
        img.rectTransform.offsetMax = Vector2.zero;
        return group;
    }

    IEnumerator OverlayFade(float duration)
    {
        var go = new GameObject("DeathFade");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        var group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        var img = new GameObject("Black").AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        img.color = Color.black;
        img.rectTransform.anchorMin = Vector2.zero;
        img.rectTransform.anchorMax = Vector2.one;
        img.rectTransform.offsetMin = Vector2.zero;
        img.rectTransform.offsetMax = Vector2.zero;
        float t = 0f;
        while (t < duration) { t += Time.unscaledDeltaTime; group.alpha = Mathf.Clamp01(t / duration); yield return null; }
        group.alpha = 1f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, detectRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (!waypoints[i]) continue;
            Gizmos.DrawSphere(waypoints[i].position, 0.25f);
            var n = waypoints[(i + 1) % waypoints.Count];
            if (n) Gizmos.DrawLine(waypoints[i].position, n.position);
        }
    }
}
