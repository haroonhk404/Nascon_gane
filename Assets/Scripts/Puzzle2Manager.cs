// ===================== PUZZLE 2 =====================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// PUZZLE 2 manager. Saare Puzzle2Socket bhar jayein -> puzzle complete ->
/// 2 animations chalti hain (Portal + koi doosra object), script se XYZ position/scale/rotation.
/// Pet (PetGuidePuzzle2) isi se poochta hai ke kahan guide kare.
/// </summary>
public class Puzzle2Manager : MonoBehaviour
{
    [Serializable]
    public class Puzzle2Animation
    {
        public string label = "Portal";
        [Tooltip("Jis object ko animate karna hai")]
        public Transform target;
        [Tooltip("ON = puzzle complete hone tak object chhupa rahega")]
        public bool hideUntilComplete = true;

        [Header("Position (XYZ)")]
        [Tooltip("ON = local position (parent ke hisaab se), OFF = world position")]
        public bool useLocalPosition = true;
        public Vector3 startPosition;
        public Vector3 endPosition;

        [Header("Scale")]
        public bool animateScale = true;
        public Vector3 startScale = Vector3.zero;
        public Vector3 endScale = Vector3.one;

        [Header("Rotation")]
        [Tooltip("Animation ke dauran kitne degree ghoome (0 = nahi)")]
        public Vector3 rotateBy = Vector3.zero;
        [Tooltip("Animation khatam hone ke baad hamesha ghoomta rahe (degree/sec). Portal ke liye achha hai.")]
        public Vector3 idleSpin = Vector3.zero;

        [Header("Timing")]
        public float delay = 0f;
        public float duration = 2f;
        public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public UnityEvent onFinished;
    }

    public static Puzzle2Manager Instance { get; private set; }

    [Header("Puzzle")]
    public List<Puzzle2Socket> sockets = new List<Puzzle2Socket>();
    [Tooltip("Jo candles uthani hain. Khali = scene ki saari 'Can Be Grabbed' candles khud mil jayengi")]
    public List<Candle> pickupCandles = new List<Candle>();
    [Tooltip("0 = saare sockets bharne zaroori")]
    public int requiredCount = 0;

    [Header("Order")]
    [Tooltip("ON = pet sockets list ke order mein ek ek karke dikhaye (Socket_1, Socket_2...). OFF = sab se qareeb wala")]
    public bool socketsInOrder = true;
    [Tooltip("ON = sirf wahi socket candle lega jo abhi pet dikha raha hai (order zabardasti)")]
    public bool strictOrder = false;

    [Header("References")]
    public PlayerInteractor player;
    [Tooltip("Optional: 'Candles placed 3/9' text")]
    public TMP_Text progressText;
    public string completeMessage = "A portal has appeared!";

    [Header("On Complete: Portal Animation")]
    [Tooltip("Sirf Portal wali entry rakho (Size = 1)")]
    public List<Puzzle2Animation> animations = new List<Puzzle2Animation>
    {
        new Puzzle2Animation { label = "Portal" }
    };

    [Header("On Complete: Object 2 (sirf neeche jaye)")]
    public Transform object2;
    [Tooltip("Kitna neeche jaye (Y). -10 = 10 meter neeche")]
    public float object2MoveY = -10f;
    public float object2Duration = 2f;
    public float object2Delay = 0f;

    Vector3 object2StartPos;

    [Header("Cutscene Camera (optional)")]
    [Tooltip("Scene mein rakha hua camera jo animations dikhaye. Khali = koi cutscene nahi")]
    public Camera cutsceneCamera;
    [Tooltip("Cutscene camera kitni der dikhe (seconds)")]
    public float cutsceneDuration = 3f;
    [Tooltip("Puzzle complete hone ke kitni der baad camera switch ho")]
    public float cutsceneStartDelay = 0.3f;
    [Tooltip("Switch karte waqt halka sa black fade (scene mein ScreenFader ho to)")]
    public bool fadeOnSwitch = true;
    public float switchFadeTime = 0.25f;
    [Tooltip("Cutscene ke dauran disable: ThirdPersonController, ThirdPersonCamera, PlayerInteractor")]
    public MonoBehaviour[] disableDuringCutscene;
    [Tooltip("Animations cutscene camera ke on hone ke baad shuru hon (ON) ya foran (OFF)")]
    public bool startAnimationsWithCutscene = true;

    [Header("TEST Key (setup ke liye)")]
    [Tooltip("ON = K dabao to cutscene + animations chalein (baar baar). Game final karte waqt OFF kar dena")]
    public bool enableTestKey = true;
    [Tooltip("Pehli dafa K = puzzle complete. Agli dafa K = sirf cutscene + animations dobara (shuru se)")]
    public KeyCode testKey = KeyCode.K;

    [Header("Pet")]
    [Tooltip("Complete hone ke baad pet kahan le jaye (khali = pehli animation ka object, yani Portal)")]
    public Transform portalGuidePoint;

    [Header("Events")]
    public UnityEvent onPuzzleComplete;

    public bool Solved { get; private set; }
    public int Required => requiredCount > 0 ? Mathf.Min(requiredCount, sockets.Count) : sockets.Count;

    public int FilledCount
    {
        get
        {
            int n = 0;
            foreach (var s in sockets) if (s && s.IsFilled) n++;
            return n;
        }
    }

    readonly Dictionary<Candle, Vector3> originalScales = new Dictionary<Candle, Vector3>();
    readonly HashSet<Candle> placedCandles = new HashSet<Candle>();

    Camera mainCam;

    void Awake()
    {
        Instance = this;
        foreach (var s in sockets) if (s) s.manager = this;

        mainCam = Camera.main;
        if (cutsceneCamera)
        {
            if (cutsceneCamera == mainCam) Debug.LogWarning("Puzzle2: Cutscene Camera ko 'MainCamera' tag mat do, alag camera rakho");
            var listener = cutsceneCamera.GetComponent<AudioListener>();
            if (listener) listener.enabled = false;   // do AudioListener ka warning na aaye
            cutsceneCamera.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        if (!player) player = FindAnyObjectByType<PlayerInteractor>();

        if (pickupCandles.Count == 0)
            foreach (var c in FindObjectsByType<Candle>(FindObjectsSortMode.None))
                if (c.canBeGrabbed) pickupCandles.Add(c);

        foreach (var c in pickupCandles)
            if (c && !originalScales.ContainsKey(c)) originalScales[c] = c.transform.lossyScale;

        foreach (var a in animations)
            if (a.target && a.hideUntilComplete && a.target != transform)
                a.target.gameObject.SetActive(false);

        if (object2) object2StartPos = object2.localPosition;

        UpdateUI();
    }

    bool cutscenePlaying;

    void Update()
    {
        if (enableTestKey && TestKeyPressed())
        {
            if (!Solved)
            {
                Debug.Log("[Puzzle2] K: puzzle complete (test)");
                Complete();
            }
            else if (!cutscenePlaying)
            {
                Debug.Log("[Puzzle2] K: cutscene + animations dobara");
                Replay();
            }
        }
    }

    bool TestKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        var key = Keyboard.current.FindKeyOnCurrentKeyboardLayout(testKey.ToString());
        return key != null ? key.wasPressedThisFrame : Keyboard.current.kKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(testKey);
#endif
    }

    // Test ke liye: animations aur cutscene shuru se dobara
    void Replay()
    {
        StopAllCoroutines();   // purani animations / portal spin roko
        if (mainCam) mainCam.enabled = true;
        if (cutsceneCamera) cutsceneCamera.gameObject.SetActive(false);
        SetControls(true);

        if (cutsceneCamera) StartCoroutine(CutsceneRoutine());
        else StartAllAnimations();
    }

    public Vector3 GetOriginalScale(Candle c)
    {
        return originalScales.TryGetValue(c, out var s) ? s : c.transform.lossyScale;
    }

    public void NotifyFilled(Puzzle2Socket s)
    {
        if (s && s.PlacedCandle) placedCandles.Add(s.PlacedCandle);
        UpdateUI();
        if (!Solved && FilledCount >= Required) Complete();
    }

    void Complete()
    {
        Solved = true;
        UpdateUI();
        if (player && !string.IsNullOrEmpty(completeMessage)) player.ShowMessage(completeMessage);

        if (cutsceneCamera)
        {
            Debug.Log($"[Puzzle2] Complete! Cutscene camera '{cutsceneCamera.name}' pe switch kar raha hoon");
            StartCoroutine(CutsceneRoutine());
        }
        else
        {
            Debug.LogWarning("[Puzzle2] Cutscene Camera field KHALI hai - camera switch nahi hoga. Puzzle2Manager mein CutsceneCam assign karo.");
            StartAllAnimations();
        }

        onPuzzleComplete?.Invoke();
    }

    // ---------------- Cutscene ----------------
    IEnumerator CutsceneRoutine()
    {
        cutscenePlaying = true;
        SetControls(false);
        if (!startAnimationsWithCutscene)
            StartAllAnimations();

        if (cutsceneStartDelay > 0f) yield return new WaitForSeconds(cutsceneStartDelay);

        var fader = fadeOnSwitch ? ScreenFader.Instance : null;

        // Main -> Cutscene
        if (fader) yield return fader.FadeOut(switchFadeTime);
        if (!mainCam) mainCam = Camera.main;
        cutsceneCamera.gameObject.SetActive(true);
        cutsceneCamera.enabled = true;
        if (!cutsceneCamera.gameObject.activeInHierarchy)
            Debug.LogError($"[Puzzle2] '{cutsceneCamera.name}' ka PARENT object band (inactive) hai, is liye camera on nahi ho sakta. CutsceneCam ko kisi band object ke andar mat rakho (jaise Portal).");
        if (mainCam && mainCam != cutsceneCamera) mainCam.enabled = false;
        Debug.Log($"[Puzzle2] Main camera: {(mainCam ? mainCam.name : "NULL")} OFF, cutscene camera ON");
        if (fader) yield return fader.FadeIn(switchFadeTime);

        if (startAnimationsWithCutscene)
            StartAllAnimations();

        yield return new WaitForSeconds(cutsceneDuration);

        // Cutscene -> Main
        if (fader) yield return fader.FadeOut(switchFadeTime);
        if (mainCam) mainCam.enabled = true;
        cutsceneCamera.gameObject.SetActive(false);
        if (fader) yield return fader.FadeIn(switchFadeTime);

        SetControls(true);
        cutscenePlaying = false;
    }

    void SetControls(bool on)
    {
        if (disableDuringCutscene == null) return;
        foreach (var m in disableDuringCutscene) if (m) m.enabled = on;
    }

    void UpdateUI()
    {
        if (progressText)
            progressText.text = Solved ? completeMessage : $"Candles placed: {FilledCount}/{Required}";
    }

    // ---------------- Animation ----------------
    void StartAllAnimations()
    {
        foreach (var a in animations) StartCoroutine(PlayAnimation(a));
        if (object2) StartCoroutine(MoveObject2());
    }

    IEnumerator MoveObject2()
    {
        if (object2Delay > 0f) yield return new WaitForSeconds(object2Delay);

        Vector3 from = object2StartPos;
        Vector3 to = from + Vector3.up * object2MoveY;
        object2.localPosition = from;

        float d = Mathf.Max(0.01f, object2Duration);
        float t = 0f;
        while (t < d)
        {
            t += Time.deltaTime;
            object2.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / d));
            yield return null;
        }
        object2.localPosition = to;
    }

    IEnumerator PlayAnimation(Puzzle2Animation a)
    {
        if (a == null || !a.target) yield break;
        if (a.delay > 0f) yield return new WaitForSeconds(a.delay);

        Transform t = a.target;
        Quaternion rot0 = t.localRotation;

        SetPosition(a, a.startPosition);
        if (a.animateScale) t.localScale = a.startScale;
        t.gameObject.SetActive(true);

        float d = Mathf.Max(0.01f, a.duration);
        float time = 0f;
        while (time < d)
        {
            time += Time.deltaTime;
            float k = a.ease.Evaluate(Mathf.Clamp01(time / d));

            SetPosition(a, Vector3.LerpUnclamped(a.startPosition, a.endPosition, k));
            if (a.animateScale) t.localScale = Vector3.LerpUnclamped(a.startScale, a.endScale, k);
            if (a.rotateBy != Vector3.zero) t.localRotation = rot0 * Quaternion.Euler(a.rotateBy * k);
            yield return null;
        }

        SetPosition(a, a.endPosition);
        if (a.animateScale) t.localScale = a.endScale;
        if (a.rotateBy != Vector3.zero) t.localRotation = rot0 * Quaternion.Euler(a.rotateBy);

        a.onFinished?.Invoke();

        // Hamesha ghoomta rahe (portal)
        if (a.idleSpin != Vector3.zero)
        {
            while (t)
            {
                t.Rotate(a.idleSpin * Time.deltaTime, Space.Self);
                yield return null;
            }
        }
    }

    static void SetPosition(Puzzle2Animation a, Vector3 p)
    {
        if (a.useLocalPosition) a.target.localPosition = p;
        else a.target.position = p;
    }

    // ---------------- Pet ke liye ----------------
    public Transform GetGuideTarget()
    {
        if (Solved)
        {
            if (portalGuidePoint) return portalGuidePoint;
            return (animations.Count > 0 && animations[0].target) ? animations[0].target : null;
        }

        Vector3 from = player ? player.transform.position : transform.position;

        // Haath mein candle -> agla khali socket
        if (player && player.HeldCandle)
        {
            var s = CurrentSocket;
            return s ? s.SnapPoint : null;
        }

        // Haath khali -> qareeb wali uthane wali candle
        Candle bestC = null; float bestCD = float.MaxValue;
        foreach (var c in pickupCandles)
        {
            if (!c || c.IsHeld || placedCandles.Contains(c) || !c.canBeGrabbed) continue;
            float d = (c.transform.position - from).sqrMagnitude;
            if (d < bestCD) { bestCD = d; bestC = c; }
        }
        return bestC ? bestC.transform : null;
    }

    /// Abhi kaunsa socket bharna hai (order ON = list mein pehla khali, OFF = player ke qareeb wala)
    public Puzzle2Socket CurrentSocket
    {
        get
        {
            if (socketsInOrder)
            {
                foreach (var s in sockets) if (s && !s.IsFilled) return s;
                return null;
            }

            Vector3 from = player ? player.transform.position : transform.position;
            Puzzle2Socket best = null; float bestD = float.MaxValue;
            foreach (var s in sockets)
            {
                if (!s || s.IsFilled) continue;
                float d = (s.SnapPoint.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }
    }

    public string GetGuideHint(Transform target)
    {
        if (!target) return "";
        if (Solved) return "Go through the portal!";
        foreach (var s in sockets) if (s && s.SnapPoint == target) return "Place the candle here!";
        if (target.GetComponent<Candle>()) return "Grab this candle!";
        return "This way!";
    }

    // ---------------- Editor helpers (component ke ⋮ menu se) ----------------
    [ContextMenu("Anim 1 (Portal): START = abhi wali position")]
    void A1Start() => CopyCurrent(0, true);
    [ContextMenu("Anim 1 (Portal): END = abhi wali position + scale")]
    void A1End() => CopyCurrent(0, false);

    [ContextMenu("TEST: Puzzle complete karo (Play mode)")]
    void TestComplete()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Pehle Play dabao"); return; }
        if (!Solved) Complete();
    }

    void CopyCurrent(int index, bool start)
    {
        if (index >= animations.Count || !animations[index].target)
        {
            Debug.LogWarning($"Animation {index + 1} ka target set nahi hai");
            return;
        }
        var a = animations[index];
        Vector3 p = a.useLocalPosition ? a.target.localPosition : a.target.position;
        if (start) a.startPosition = p;
        else { a.endPosition = p; a.endScale = a.target.localScale; }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log($"{a.label}: {(start ? "START" : "END")} = {p}");
    }
}
