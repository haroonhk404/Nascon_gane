// ===================== PUZZLE 2 =====================
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// PUZZLE 2 - Shadow event.
/// X candles lag jayein -> cutscene: shadow chal kar guzarta hai
/// -> phir 'fogTime' seconds mein fog + blur + dizziness badhti hai
/// -> waqt pe puzzle complete = fog saaf. Waqt khatam = player behosh -> scene restart.
/// </summary>
[DefaultExecutionOrder(10000)]   // camera script ke baad chale (dizzy wobble ke liye)
public class Puzzle2ShadowEvent : MonoBehaviour
{
    [Header("Trigger")]
    [Tooltip("Khali = khud dhoond lega")]
    public Puzzle2Manager puzzle;
    [Tooltip("Itni candles lagne pe event shuru")]
    public int triggerAtCandles = 6;

    [Header("Cutscene Camera")]
    [Tooltip("Shadow wala cutscene camera (Puzzle2 complete wale se alag). Hierarchy mein band rakho")]
    public Camera shadowCutsceneCam;
    public float cutsceneStartDelay = 0.5f;
    [Tooltip("Camera shadow ko follow kare (sirf ghoom kar dekhe, apni jagah se hile nahi)")]
    public bool cameraLooksAtShadow = true;
    [Tooltip("ON = shadow ke chalte chalte hi end fade ho jaye (rukta nazar nahi aayega)")]
    public bool endWhileWalking = true;
    [Tooltip("End wala fade kitna tez (seconds). Ye shadow ke pahunchne se itna pehle shuru hota hai")]
    public float endFadeTime = 0.4f;
    [Tooltip("Sirf jab 'End While Walking' OFF ho: shadow ruk jane ke baad camera kitni der aur rahe")]
    public float extraCutsceneTime = 1f;
    public bool fadeOnSwitch = true;
    public float switchFadeTime = 0.3f;
    [Tooltip("Cutscene ke dauran band: ThirdPersonController, ThirdPersonCamera, PlayerInteractor")]
    public MonoBehaviour[] disableDuringCutscene;

    [Header("Shadow")]
    [Tooltip("Shadow character (Hierarchy mein band rakho)")]
    public GameObject shadow;
    public Transform walkFrom;
    public Transform walkTo;
    public float walkDuration = 5f;
    [Tooltip("Animator ka bool parameter jo walk chalaye (khali = skip)")]
    public string walkBoolParameter = "";
    [Tooltip("Animator ka float parameter (jaise 'Speed'). Chalte waqt is mein Walk Speed Value jayegi")]
    public string walkSpeedParameter = "Speed";
    [Tooltip("Speed parameter ki value. CreepAnimator_Auto mein 4 se zyada = Run state (Crouch)")]
    public float walkSpeedValue = 6f;
    [Tooltip("Seedha ye state chalao (jaise 'Run' ya 'Creep|Crouch_Action'). Khali = parameters se")]
    public string walkStateName = "";
    public bool hideShadowAfterWalk = true;

    [Header("Cutscene Audio")]
    [Tooltip("Cutscene ki music / dar wali awaaz (2D, har jagah ek jaisi)")]
    public AudioClip cutsceneMusic;
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Tooltip("Cutscene khatam hone pe music kitni der mein band ho (0 = chalti rahe)")]
    public float musicFadeOut = 1f;
    [Tooltip("Shadow ki awaaz: qadam / guraana (3D, shadow se aaye). Loop hoti hai")]
    public AudioClip shadowSound;
    [Range(0f, 1f)] public float shadowSoundVolume = 1f;
    [Tooltip("Shadow ki awaaz kitni door tak sunai de (meter)")]
    public float shadowSoundRange = 25f;
    [Tooltip("ON = awaaz shadow ki jagah se aaye (door = dheemi). OFF = har jagah ek jaisi saaf")]
    public bool shadowSound3D = true;
    [Tooltip("Ek dafa bajne wali awaaz jab shadow nazar aaye (cheekh / jump scare)")]
    public AudioClip shadowAppearClip;

    [Header("Fog (30 sec)")]
    public float fogTime = 30f;
    [Tooltip("Aakhir mein fog kitni ghani (0.3+ = kuch nazar nahi aata)")]
    public float fogEndDensity = 0.35f;
    public Color fogEndColor = new Color(0.45f, 0.48f, 0.52f);

    [Header("Dizziness")]
    [Tooltip("Blur/distortion wala Volume (Global, weight 0 se shuru)")]
    public Volume dizzyVolume;
    [Tooltip("Camera kitna dagmagaye (degrees) jab fog poori ho")]
    public float wobbleAmount = 6f;
    public float wobbleSpeed = 0.6f;

    [Tooltip("Fog (30 sec timer) shuru hote hi - jaise Creep ko SetActive(true)")]
    public UnityEvent onFogStart;

    [Header("Timer UI (optional)")]
    public TMP_Text timerText;
    public string warningMessage = "Hurry... place the remaining candles!";

    [Header("Faint (waqt khatam)")]
    [Tooltip("Behosh hone pe band: ThirdPersonController, PlayerInteractor")]
    public MonoBehaviour[] disableOnFaint;
    [Tooltip("Optional: player ka Animator")]
    public Animator playerAnimator;
    [Tooltip("Optional: Animator trigger ka naam (jaise 'Faint'). Khali = sirf camera girega")]
    public string faintTrigger = "";
    public float faintFallTime = 2f;
    public float faintFadeTime = 2f;
    [Tooltip("ON = black ke baad yahi scene dobara shuru")]
    public bool reloadSceneOnFaint = true;
    [Tooltip("Ya koi aur scene load karo (khali = isi scene ka reload)")]
    public string sceneOnFaint = "";
    public UnityEvent onFaint;

    [Header("Bach gaya (waqt pe puzzle complete)")]
    public float clearTime = 3f;
    public UnityEvent onSurvived;

    [Header("TEST Key")]
    [Tooltip("J dabao = shadow cutscene chale (candles gine baghair). Final game mein OFF")]
    public bool enableTestKey = true;
    public KeyCode testKey = KeyCode.J;
    [Tooltip("ON = J sirf cutscene chalaye (fog/timer nahi), baar baar. OFF = J se poora event (cutscene + fog + timer)")]
    public bool testKeyCutsceneOnly = true;

    enum Phase { Waiting, Cutscene, Fog, Fainting, Cleared }
    Phase phase = Phase.Waiting;

    Camera mainCam;
    PlayerInteractor interactor;
    float fogTimer;
    float fogStartDensity;
    Color fogStartColor;
    bool origFog;
    float origDensity;
    Color origColor;
    FogMode origMode;

    float dizzy;        // 0..1
    float faintTilt;    // 0..1
    Quaternion lastOffset = Quaternion.identity;
    Camera offsetCam;

    void Start()
    {
        if (!puzzle) puzzle = FindAnyObjectByType<Puzzle2Manager>();
        if (puzzle) puzzle.onPuzzleComplete.AddListener(OnPuzzleSolved);
        interactor = FindAnyObjectByType<PlayerInteractor>();
        mainCam = Camera.main;

        origFog = RenderSettings.fog;
        origDensity = RenderSettings.fogDensity;
        origColor = RenderSettings.fogColor;
        origMode = RenderSettings.fogMode;

        if (shadow) shadow.SetActive(false);
        if (shadowCutsceneCam)
        {
            var l = shadowCutsceneCam.GetComponent<AudioListener>();
            if (l) l.enabled = false;
            shadowCutsceneCam.gameObject.SetActive(false);
        }
        if (dizzyVolume) dizzyVolume.weight = 0f;
        if (timerText) timerText.text = "";
    }

    void Update()
    {
        // Pichle frame ka camera wobble hatao (taake camera script ko saaf rotation mile)
        if (offsetCam)
        {
            offsetCam.transform.rotation = offsetCam.transform.rotation * Quaternion.Inverse(lastOffset);
            offsetCam = null;
            lastOffset = Quaternion.identity;
        }

        if (enableTestKey && phase == Phase.Waiting && !cutsceneRunning && TestKeyPressed())
        {
            Debug.Log(testKeyCutsceneOnly ? "[Puzzle2] J: sirf shadow cutscene (test)" : "[Puzzle2] J: poora shadow event (test)");
            StartCoroutine(CutsceneRoutine(!testKeyCutsceneOnly));
        }

        switch (phase)
        {
            case Phase.Waiting:
                if (!cutsceneRunning && puzzle && !puzzle.Solved && puzzle.FilledCount >= triggerAtCandles)
                    StartCoroutine(CutsceneRoutine(true));
                break;

            case Phase.Fog:
                UpdateFog();
                break;
        }

        if (dizzyVolume)
            dizzyVolume.weight = Mathf.Clamp01(dizzy * (0.85f + 0.15f * Mathf.Sin(Time.time * 2.3f)) + faintTilt);
    }

    void LateUpdate()
    {
        // Dizziness + behosh hone ka camera effect (camera script ke baad)
        if (dizzy <= 0.001f && faintTilt <= 0.001f) return;

        Camera cam = (mainCam && mainCam.enabled) ? mainCam : null;
        if (!cam) return;

        float t = Time.time * wobbleSpeed;
        float a = wobbleAmount * dizzy;
        float pitch = (Mathf.PerlinNoise(t, 1.3f) - 0.5f) * 2f * a + faintTilt * 35f;
        float yaw   = (Mathf.PerlinNoise(t, 7.1f) - 0.5f) * 2f * a;
        float roll  = (Mathf.PerlinNoise(t, 3.7f) - 0.5f) * 4f * a + faintTilt * 70f;

        lastOffset = Quaternion.Euler(pitch, yaw, roll);
        cam.transform.rotation = cam.transform.rotation * lastOffset;
        offsetCam = cam;
    }

    // ================= CUTSCENE =================
    bool cutsceneRunning;

    IEnumerator CutsceneRoutine(bool startFogAfter)
    {
        cutsceneRunning = true;
        if (startFogAfter) phase = Phase.Cutscene;   // test-only: phase Waiting hi rahe
        Debug.Log("[Puzzle2] Shadow cutscene shuru");
        SetControls(disableDuringCutscene, false);

        if (cutsceneStartDelay > 0f) yield return new WaitForSeconds(cutsceneStartDelay);

        var fader = fadeOnSwitch ? ScreenFader.Instance : null;
        if (!mainCam) mainCam = Camera.main;

        // Main -> shadow camera
        if (shadowCutsceneCam)
        {
            if (fader) yield return fader.FadeOut(switchFadeTime);
            shadowCutsceneCam.gameObject.SetActive(true);
            shadowCutsceneCam.enabled = true;
            if (mainCam && mainCam != shadowCutsceneCam) mainCam.enabled = false;
            if (fader) yield return fader.FadeIn(switchFadeTime);
        }

        // Audio shuru
        StartCutsceneAudio();

        // Shadow walk (alag coroutine mein chalta rahe, taake fade ke dauran bhi chalta dikhe)
        walkDone = false;
        Coroutine walk = shadow ? StartCoroutine(WalkShadow()) : null;
        if (!shadow) walkDone = true;

        float endFade = fader ? endFadeTime : 0f;

        if (endWhileWalking)
        {
            // Shadow ke pahunchne se 'endFade' pehle fade shuru -> chalte chalte black
            float wait = Mathf.Max(0f, walkDuration - endFade);
            float w = 0f;
            while (w < wait) { w += Time.deltaTime; yield return null; }
        }
        else
        {
            while (!walkDone) yield return null;
            float extra = 0f;
            while (extra < extraCutsceneTime)
            {
                extra += Time.deltaTime;
                if (shadow) LookAtShadow(shadow.transform);
                yield return null;
            }
        }

        // Shadow cam -> main (shadow abhi bhi chal raha hai jab tak screen black na ho)
        if (fader) yield return fader.FadeOut(endFade > 0f ? endFade : switchFadeTime);
        if (walk != null) StopCoroutine(walk);
        StopShadowSound();
        if (shadow && hideShadowAfterWalk) shadow.SetActive(false);
        RestoreShadowAI();
        if (musicFadeOut > 0f) StartCoroutine(FadeOutMusic(musicFadeOut));
        if (shadowCutsceneCam)
        {
            if (mainCam) mainCam.enabled = true;
            shadowCutsceneCam.gameObject.SetActive(false);
        }
        if (fader) yield return fader.FadeIn(switchFadeTime);

        SetControls(disableDuringCutscene, true);
        cutsceneRunning = false;

        if (startFogAfter) StartFog();
        else Debug.Log("[Puzzle2] Test cutscene khatam (fog nahi). J dobara dabao to phir chalega.");
    }

    bool walkDone;

    IEnumerator WalkShadow()
    {
        Transform st = shadow.transform;
        Vector3 from = walkFrom ? walkFrom.position : st.position;
        Vector3 to = walkTo ? walkTo.position : st.position + st.forward * 5f;

        st.position = from;
        Vector3 dir = to - from; dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) st.rotation = Quaternion.LookRotation(dir);
        shadow.SetActive(true);
        StartShadowAudio();

        Animator anim = shadow.GetComponentInChildren<Animator>();
        StartWalkAnim(anim);

        float d = Mathf.Max(0.01f, walkDuration);
        float t = 0f;
        while (t < d)
        {
            t += Time.deltaTime;
            st.position = Vector3.Lerp(from, to, t / d);
            if (anim && !string.IsNullOrEmpty(walkSpeedParameter) && HasParam(anim, walkSpeedParameter))
                anim.SetFloat(walkSpeedParameter, walkSpeedValue);   // har frame, taake koi aur script na badle
            LookAtShadow(st);
            yield return null;
        }
        st.position = to;

        // Agar cutscene abhi chal raha hai (endWhileWalking OFF) to walk band
        if (!endWhileWalking && anim)
        {
            if (!string.IsNullOrEmpty(walkBoolParameter) && HasParam(anim, walkBoolParameter)) anim.SetBool(walkBoolParameter, false);
            if (!string.IsNullOrEmpty(walkSpeedParameter) && HasParam(anim, walkSpeedParameter)) anim.SetFloat(walkSpeedParameter, 0f);
        }
        walkDone = true;
    }

    void StartWalkAnim(Animator anim)
    {
        if (!anim) { Debug.LogWarning("[Shadow] Shadow mein Animator nahi mila"); return; }
        anim.applyRootMotion = false;

        // Shadow pe AI scripts / NavMesh ho to cutscene ke dauran band (warna position aur animation chheen lete hain)
        pausedForWalk.Clear();
        foreach (var mb in shadow.GetComponentsInChildren<MonoBehaviour>())
        {
            string n = mb.GetType().Name;
            if ((n.Contains("CreepPatrolAI") || n.Contains("CreepAI")) && mb.enabled)
            {
                mb.enabled = false;
                pausedForWalk.Add(mb);
                Debug.Log($"[Shadow] '{n}' cutscene ke liye band kiya");
            }
        }
        pausedAgent = shadow.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>();
        if (pausedAgent && pausedAgent.enabled) pausedAgent.enabled = false;
        else pausedAgent = null;

        if (!string.IsNullOrEmpty(walkStateName))
        {
            anim.CrossFadeInFixedTime(walkStateName, 0.1f);
            Debug.Log($"[Shadow] State '{walkStateName}' chalayi");
        }
        if (!string.IsNullOrEmpty(walkBoolParameter) && HasParam(anim, walkBoolParameter)) anim.SetBool(walkBoolParameter, true);
        if (!string.IsNullOrEmpty(walkSpeedParameter) && HasParam(anim, walkSpeedParameter)) anim.SetFloat(walkSpeedParameter, walkSpeedValue);
    }

    // ================= AUDIO =================
    AudioSource musicSource;
    AudioSource shadowSource;
    bool shadowSourceCreated;

    void StartCutsceneAudio()
    {
        if (cutsceneMusic)
        {
            if (!musicSource)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.spatialBlend = 0f;   // 2D
            }
            musicSource.clip = cutsceneMusic;
            musicSource.volume = musicVolume;
            musicSource.loop = false;
            musicSource.Play();
        }
    }

    /// Shadow active hone ke baad chalao (band object pe awaaz nahi bajti)
    void StartShadowAudio()
    {
        if (shadow && (shadowSound || shadowAppearClip))
        {
            shadowSource = shadow.GetComponent<AudioSource>();
            if (!shadowSource)
            {
                shadowSource = shadow.AddComponent<AudioSource>();
                shadowSourceCreated = true;
            }
            shadowSource.playOnAwake = false;
            shadowSource.spatialBlend = shadowSound3D ? 1f : 0f;
            shadowSource.rolloffMode = AudioRolloffMode.Linear;
            shadowSource.minDistance = 2f;
            shadowSource.maxDistance = shadowSoundRange;
            shadowSource.volume = shadowSoundVolume;

            if (shadowAppearClip) shadowSource.PlayOneShot(shadowAppearClip, shadowSoundVolume);
            if (shadowSound)
            {
                shadowSource.clip = shadowSound;
                shadowSource.loop = true;
                shadowSource.Play();
            }
        }
    }

    void StopShadowSound()
    {
        if (!shadowSource) return;
        shadowSource.Stop();
        shadowSource.loop = false;
        if (shadowSourceCreated) { Destroy(shadowSource); shadowSourceCreated = false; }
        shadowSource = null;
    }

    IEnumerator FadeOutMusic(float time)
    {
        if (!musicSource || !musicSource.isPlaying) yield break;
        float start = musicSource.volume;
        float t = 0f;
        while (t < time && musicSource)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(start, 0f, t / time);
            yield return null;
        }
        if (musicSource) { musicSource.Stop(); musicSource.volume = musicVolume; }
    }

    readonly System.Collections.Generic.List<MonoBehaviour> pausedForWalk = new System.Collections.Generic.List<MonoBehaviour>();
    UnityEngine.AI.NavMeshAgent pausedAgent;

    /// Cutscene ke baad shadow ki AI wapis ON (agar wahi creep baad mein patrol kare)
    void RestoreShadowAI()
    {
        if (pausedAgent) pausedAgent.enabled = true;
        foreach (var mb in pausedForWalk) if (mb) mb.enabled = true;
        pausedForWalk.Clear();
        pausedAgent = null;
    }

    static bool HasParam(Animator a, string name)
    {
        foreach (var p in a.parameters) if (p.name == name) return true;
        return false;
    }

    void LookAtShadow(Transform st)
    {
        if (!cameraLooksAtShadow || !shadowCutsceneCam) return;
        Vector3 look = st.position + Vector3.up * 1.4f - shadowCutsceneCam.transform.position;
        if (look.sqrMagnitude < 0.001f) return;
        shadowCutsceneCam.transform.rotation = Quaternion.Slerp(
            shadowCutsceneCam.transform.rotation, Quaternion.LookRotation(look), 4f * Time.deltaTime);
    }

    // ================= FOG =================
    void StartFog()
    {
        if (puzzle && puzzle.Solved) { phase = Phase.Cleared; return; }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        fogStartDensity = origFog ? Mathf.Max(origDensity, 0.005f) : 0.005f;
        fogStartColor = RenderSettings.fogColor;
        fogTimer = 0f;
        phase = Phase.Fog;

        if (interactor && !string.IsNullOrEmpty(warningMessage)) interactor.ShowMessage(warningMessage);
        onFogStart?.Invoke();
    }

    void UpdateFog()
    {
        fogTimer += Time.deltaTime;
        float p = Mathf.Clamp01(fogTimer / fogTime);
        float eased = p * p * (3f - 2f * p);   // shuru mein aahista, beech mein tez

        RenderSettings.fogDensity = Mathf.Lerp(fogStartDensity, fogEndDensity, eased);
        RenderSettings.fogColor = Color.Lerp(fogStartColor, fogEndColor, eased);
        dizzy = eased;

        if (timerText)
        {
            int left = Mathf.CeilToInt(fogTime - fogTimer);
            timerText.text = left > 0 ? $"{left}" : "";
            timerText.color = Color.Lerp(Color.white, new Color(1f, 0.25f, 0.2f), p);
        }

        if (p >= 1f) StartCoroutine(FaintRoutine());
    }

    // ================= BACH GAYA =================
    void OnPuzzleSolved()
    {
        if (phase == Phase.Fog || phase == Phase.Cutscene)
            StartCoroutine(ClearRoutine());
    }

    IEnumerator ClearRoutine()
    {
        phase = Phase.Cleared;
        if (timerText) timerText.text = "";

        float d0 = RenderSettings.fogDensity;
        Color c0 = RenderSettings.fogColor;
        float z0 = dizzy;
        float t = 0f;
        float d = Mathf.Max(0.01f, clearTime);

        while (t < d)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / d);
            RenderSettings.fogDensity = Mathf.Lerp(d0, origDensity, k);
            RenderSettings.fogColor = Color.Lerp(c0, origColor, k);
            dizzy = Mathf.Lerp(z0, 0f, k);
            yield return null;
        }

        RenderSettings.fog = origFog;
        RenderSettings.fogMode = origMode;
        RenderSettings.fogDensity = origDensity;
        RenderSettings.fogColor = origColor;
        dizzy = 0f;
        onSurvived?.Invoke();
    }

    // ================= BEHOSH =================
    IEnumerator FaintRoutine()
    {
        if (phase == Phase.Fainting) yield break;
        phase = Phase.Fainting;
        Debug.Log("[Puzzle2] Waqt khatam - player behosh");

        if (timerText) timerText.text = "";
        SetControls(disableOnFaint, false);
        if (playerAnimator && !string.IsNullOrEmpty(faintTrigger)) playerAnimator.SetTrigger(faintTrigger);

        float t = 0f;
        float fall = Mathf.Max(0.01f, faintFallTime);
        while (t < fall)
        {
            t += Time.deltaTime;
            faintTilt = Mathf.SmoothStep(0f, 1f, t / fall);
            yield return null;
        }

        var fader = ScreenFader.Instance;
        if (fader) yield return fader.FadeOut(faintFadeTime);
        else yield return OverlayFade(faintFadeTime);

        onFaint?.Invoke();
        yield return new WaitForSecondsRealtime(1f);

        RenderSettings.fog = origFog;
        RenderSettings.fogDensity = origDensity;
        RenderSettings.fogColor = origColor;
        RenderSettings.fogMode = origMode;

        if (!string.IsNullOrEmpty(sceneOnFaint) && Application.CanStreamedLevelBeLoaded(sceneOnFaint))
            SceneManager.LoadScene(sceneOnFaint);
        else if (reloadSceneOnFaint)
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    IEnumerator OverlayFade(float duration)
    {
        var go = new GameObject("FaintFade");
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
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }
        group.alpha = 1f;
    }

    // ================= HELPERS =================
    static void SetControls(MonoBehaviour[] list, bool on)
    {
        if (list == null) return;
        foreach (var m in list) if (m) m.enabled = on;
    }

    bool TestKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        var key = Keyboard.current.FindKeyOnCurrentKeyboardLayout(testKey.ToString());
        return key != null ? key.wasPressedThisFrame : Keyboard.current.jKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(testKey);
#endif
    }

    void OnDestroy()
    {
        if (puzzle) puzzle.onPuzzleComplete.RemoveListener(OnPuzzleSolved);
    }
}
