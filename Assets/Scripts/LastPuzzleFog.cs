using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Last Puzzle: scene shuru hote hi 'fogTime' (30) sec ka fog + blur + dizziness.
/// Player 'safePoint' tak pahunch jaye (ya StopFog() event se) -> fog saaf, bach gaya.
/// Waqt khatam -> player behosh -> scene restart.
/// Kisi empty object pe lagao (jaise 'FogEvent').
/// </summary>
[DefaultExecutionOrder(10000)]   // camera script ke baad chale (wobble ke liye)
public class LastPuzzleFog : MonoBehaviour
{
    [Header("Start")]
    [Tooltip("Scene shuru hone ke kitni der baad fog shuru")]
    public float startDelay = 1f;
    [Tooltip("OFF = sirf StartFog() event se shuru")]
    public bool startOnSceneStart = true;
    public string startMessage = "The fog is coming... follow the flame!";

    [Header("Fog")]
    public float fogTime = 30f;
    [Tooltip("Aakhir mein fog kitni ghani (0.3+ = kuch nazar nahi aata)")]
    public float fogEndDensity = 0.35f;
    public Color fogEndColor = new Color(0.45f, 0.48f, 0.52f);

    [Header("Bach nikalna")]
    [Tooltip("Player yahan pahunche to fog khatam (jaise pet ka aakhri waypoint / exit). Khali = sirf StopFog() se")]
    public Transform safePoint;
    public float safeRadius = 2f;
    public float clearTime = 3f;
    public string survivedMessage = "You made it...";
    public UnityEvent onSurvived;

    [Header("Dizziness")]
    [Tooltip("Khali chhodo = script khud blur wala Volume bana legi")]
    public Volume dizzyVolume;
    [Tooltip("ON = Dizzy Volume khali ho to script khud bana le")]
    public bool autoCreateDizzyVolume = true;
    [Tooltip("Kitna blur (Depth of Field: kitne meter se dhundla shuru)")]
    public float blurStart = 1f;
    public float blurEnd = 6f;
    [Range(-1f, 1f)] public float lensDistortion = -0.4f;
    [Range(0f, 1f)] public float chromatic = 1f;
    [Range(0f, 1f)] public float vignette = 0.5f;
    [Range(-100f, 0f)] public float desaturate = -40f;
    [Tooltip("Camera kitna dagmagaye (degree) jab fog poori ho")]
    public float wobbleAmount = 6f;
    public float wobbleSpeed = 0.6f;

    [Header("Timer UI (optional)")]
    [Tooltip("Alag TMP text (prompt wala nahi)")]
    public TMP_Text timerText;

    [Header("Behosh (waqt khatam)")]
    [Tooltip("ThirdPersonController, PlayerInteractor")]
    public MonoBehaviour[] disableOnFaint;
    public float faintFallTime = 2f;
    public float faintFadeTime = 2f;
    [Tooltip("Khali = isi scene ka restart")]
    public string sceneOnFaint = "";
    public UnityEvent onFaint;

    enum Phase { Waiting, Fog, Clearing, Done, Fainting }
    Phase phase = Phase.Waiting;

    Transform player;
    PlayerInteractor interactor;
    Camera cam;
    float timer;
    float dizzy, faintTilt;
    float startDensity;
    Color startColor;
    bool origFog; float origDensity; Color origColor; FogMode origMode;
    Quaternion lastOffset = Quaternion.identity;
    Camera offsetCam;

    void Start()
    {
        interactor = FindAnyObjectByType<PlayerInteractor>();
        if (interactor) player = interactor.transform;
        cam = Camera.main;

        origFog = RenderSettings.fog;
        origDensity = RenderSettings.fogDensity;
        origColor = RenderSettings.fogColor;
        origMode = RenderSettings.fogMode;

        if (!dizzyVolume && autoCreateDizzyVolume) CreateDizzyVolume();
        if (dizzyVolume) dizzyVolume.weight = 0f;

        // Camera pe post processing ON (warna blur dikhega hi nahi)
        if (cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data && !data.renderPostProcessing)
            {
                data.renderPostProcessing = true;
                Debug.Log("[LastFog] Main Camera pe Post Processing ON kiya");
            }
        }

        if (timerText) timerText.text = "";

        if (startOnSceneStart) StartCoroutine(StartAfter(startDelay));
    }

    /// Blur + distortion + chromatic + vignette + kam rang wala Volume khud banao
    void CreateDizzyVolume()
    {
        var go = new GameObject("DizzyVolume (auto)");
        var v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = 10f;
        v.weight = 0f;

        var p = ScriptableObject.CreateInstance<VolumeProfile>();

        var dof = p.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(blurStart);
        dof.gaussianEnd.Override(blurEnd);
        dof.gaussianMaxRadius.Override(1.5f);
        dof.highQualitySampling.Override(true);

        var ld = p.Add<LensDistortion>(true);
        ld.intensity.Override(lensDistortion);

        var ca = p.Add<ChromaticAberration>(true);
        ca.intensity.Override(chromatic);

        var vg = p.Add<Vignette>(true);
        vg.intensity.Override(vignette);
        vg.smoothness.Override(0.5f);

        var adj = p.Add<ColorAdjustments>(true);
        adj.saturation.Override(desaturate);

        v.sharedProfile = p;
        dizzyVolume = v;
        Debug.Log("[LastFog] Dizzy Volume khud bana diya");
    }

    IEnumerator StartAfter(float d)
    {
        if (d > 0f) yield return new WaitForSeconds(d);
        StartFog();
    }

    /// Event se bhi shuru kar sakte ho
    public void StartFog()
    {
        if (phase != Phase.Waiting) return;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        startDensity = origFog ? Mathf.Max(origDensity, 0.005f) : 0.005f;
        startColor = RenderSettings.fogColor;
        timer = 0f;
        phase = Phase.Fog;
        Debug.Log($"[LastFog] Fog shuru: {fogTime} sec");
        if (interactor && !string.IsNullOrEmpty(startMessage)) interactor.ShowMessage(startMessage);
    }

    /// Event se fog rok do (jaise gate jal gaya)
    public void StopFog()
    {
        if (phase == Phase.Fog) StartCoroutine(ClearRoutine());
    }

    void Update()
    {
        // Pichle frame ka wobble hatao
        if (offsetCam)
        {
            offsetCam.transform.rotation *= Quaternion.Inverse(lastOffset);
            offsetCam = null;
            lastOffset = Quaternion.identity;
        }

        if (phase == Phase.Fog)
        {
            timer += Time.deltaTime;
            float p = Mathf.Clamp01(timer / fogTime);
            float e = p * p * (3f - 2f * p);

            RenderSettings.fogDensity = Mathf.Lerp(startDensity, fogEndDensity, e);
            RenderSettings.fogColor = Color.Lerp(startColor, fogEndColor, e);
            dizzy = e;

            if (timerText)
            {
                int left = Mathf.Max(0, Mathf.CeilToInt(fogTime - timer));
                timerText.text = left > 0 ? left.ToString() : "";
                timerText.color = Color.Lerp(Color.white, new Color(1f, 0.25f, 0.2f), p);
            }

            // Safe point pe pahunch gaya?
            if (safePoint && player)
            {
                Vector3 a = player.position; a.y = 0f;
                Vector3 b = safePoint.position; b.y = 0f;
                if (Vector3.Distance(a, b) <= safeRadius) { StartCoroutine(ClearRoutine()); return; }
            }

            if (p >= 1f) StartCoroutine(FaintRoutine());
        }

        if (dizzyVolume)
            dizzyVolume.weight = Mathf.Clamp01(dizzy * (0.85f + 0.15f * Mathf.Sin(Time.time * 2.3f)) + faintTilt);
    }

    void LateUpdate()
    {
        if (dizzy <= 0.001f && faintTilt <= 0.001f) return;
        if (!cam) cam = Camera.main;
        if (!cam || !cam.enabled) return;

        float t = Time.time * wobbleSpeed;
        float a = wobbleAmount * dizzy;
        float pitch = (Mathf.PerlinNoise(t, 1.3f) - 0.5f) * 2f * a + faintTilt * 35f;
        float yaw   = (Mathf.PerlinNoise(t, 7.1f) - 0.5f) * 2f * a;
        float roll  = (Mathf.PerlinNoise(t, 3.7f) - 0.5f) * 4f * a + faintTilt * 70f;

        lastOffset = Quaternion.Euler(pitch, yaw, roll);
        cam.transform.rotation *= lastOffset;
        offsetCam = cam;
    }

    IEnumerator ClearRoutine()
    {
        phase = Phase.Clearing;
        if (timerText) timerText.text = "";
        Debug.Log("[LastFog] Bach gaya - fog saaf");
        if (interactor && !string.IsNullOrEmpty(survivedMessage)) interactor.ShowMessage(survivedMessage);

        float d0 = RenderSettings.fogDensity;
        Color c0 = RenderSettings.fogColor;
        float z0 = dizzy;
        float t = 0f, d = Mathf.Max(0.01f, clearTime);
        while (t < d)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / d);
            RenderSettings.fogDensity = Mathf.Lerp(d0, origDensity, k);
            RenderSettings.fogColor = Color.Lerp(c0, origColor, k);
            dizzy = Mathf.Lerp(z0, 0f, k);
            yield return null;
        }
        RestoreFog();
        dizzy = 0f;
        phase = Phase.Done;
        onSurvived?.Invoke();
    }

    IEnumerator FaintRoutine()
    {
        if (phase == Phase.Fainting) yield break;
        phase = Phase.Fainting;
        Debug.Log("[LastFog] Waqt khatam - behosh");
        if (timerText) timerText.text = "";
        foreach (var m in disableOnFaint) if (m) m.enabled = false;

        float t = 0f, fall = Mathf.Max(0.01f, faintFallTime);
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
        RestoreFog();

        if (!string.IsNullOrEmpty(sceneOnFaint) && Application.CanStreamedLevelBeLoaded(sceneOnFaint))
            SceneManager.LoadScene(sceneOnFaint);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void RestoreFog()
    {
        RenderSettings.fog = origFog;
        RenderSettings.fogMode = origMode;
        RenderSettings.fogDensity = origDensity;
        RenderSettings.fogColor = origColor;
    }

    IEnumerator OverlayFade(float duration)
    {
        var go = new GameObject("FaintFade");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        var g = go.AddComponent<CanvasGroup>();
        g.alpha = 0f;
        var img = new GameObject("Black").AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        img.color = Color.black;
        img.rectTransform.anchorMin = Vector2.zero;
        img.rectTransform.anchorMax = Vector2.one;
        img.rectTransform.offsetMin = Vector2.zero;
        img.rectTransform.offsetMax = Vector2.zero;
        float t = 0f;
        while (t < duration) { t += Time.unscaledDeltaTime; g.alpha = Mathf.Clamp01(t / duration); yield return null; }
        g.alpha = 1f;
    }

    void OnDisable() { if (phase == Phase.Fog) RestoreFog(); }

    void OnDrawGizmosSelected()
    {
        if (!safePoint) return;
        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(safePoint.position, safeRadius);
    }
}
