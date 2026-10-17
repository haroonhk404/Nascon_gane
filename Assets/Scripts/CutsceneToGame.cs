using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Cutscene scene mein kisi object pe lagao.
/// Video (VideoPlayer) YA Timeline (PlayableDirector) chalata hai, khatam hone pe (ya Skip pe)
/// fade karke game scene load karta hai.
/// </summary>
public class CutsceneToGame : MonoBehaviour
{
    [Header("Next")]
    [Tooltip("Cutscene ke baad ye scene (game)")]
    public string nextScene = "SampleScene";

    [Header("Cutscene (inmein se ek)")]
    [Tooltip("Video cutscene: VideoPlayer (Video Clip ke saath)")]
    public VideoPlayer video;
    [Tooltip("In-game cutscene: Timeline wala PlayableDirector")]
    public PlayableDirector timeline;
    [Tooltip("Dono khali hon to itne second baad game")]
    public float fallbackDuration = 8f;

    [Header("Skip")]
    public bool allowSkip = true;
    [Tooltip("Shuru ke itne second skip band (galti se skip na ho)")]
    public float skipLockTime = 1f;
    [Tooltip("Optional: 'Press SPACE to skip' wala text/object")]
    public GameObject skipHint;

    [Header("Fade")]
    public float fadeInTime = 1f;
    public float fadeOutTime = 1f;

    bool ending;
    float elapsed;
    CanvasGroup fade;

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        fade = CreateOverlay();
        StartCoroutine(FadeTo(0f, fadeInTime));   // black se shuru

        if (skipHint) skipHint.SetActive(false);

        if (video)
        {
            video.isLooping = false;
            video.loopPointReached += _ => EndCutscene();
            video.errorReceived += (vp, msg) => { Debug.LogError("[Cutscene] Video error: " + msg); EndCutscene(); };
            video.Play();
            Debug.Log("[Cutscene] Video chal rahi hai");
        }
        else if (timeline)
        {
            timeline.extrapolationMode = DirectorWrapMode.None;
            timeline.stopped += _ => EndCutscene();
            timeline.Play();
            Debug.Log("[Cutscene] Timeline chal rahi hai");
        }
        else
        {
            Debug.Log($"[Cutscene] Video/Timeline nahi - {fallbackDuration} sec baad game");
        }
    }

    void Update()
    {
        if (ending) return;
        elapsed += Time.deltaTime;

        if (!video && !timeline && elapsed >= fallbackDuration) { EndCutscene(); return; }

        // Timeline kabhi kabhi 'stopped' nahi bhejti -> time se check
        if (timeline && timeline.duration > 0 && timeline.time >= timeline.duration - 0.02) { EndCutscene(); return; }

        if (allowSkip && elapsed >= skipLockTime)
        {
            if (skipHint && !skipHint.activeSelf) skipHint.SetActive(true);
            if (SkipPressed()) { Debug.Log("[Cutscene] Skip"); EndCutscene(); }
        }
    }

    public void EndCutscene()
    {
        if (ending) return;
        ending = true;
        if (skipHint) skipHint.SetActive(false);
        StartCoroutine(EndRoutine());
    }

    IEnumerator EndRoutine()
    {
        yield return FadeTo(1f, fadeOutTime);

        if (video) video.Stop();
        if (timeline) timeline.Stop();

        if (Application.CanStreamedLevelBeLoaded(nextScene))
            SceneManager.LoadScene(nextScene);
        else
            Debug.LogError($"[Cutscene] Scene '{nextScene}' Build Profiles ki Scene List mein nahi hai!");
    }

    bool SkipPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        var m = Mouse.current;
        return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
            || (m != null && m.leftButton.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0);
#endif
    }

    IEnumerator FadeTo(float target, float time)
    {
        float start = fade.alpha;
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            fade.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / time));
            yield return null;
        }
        fade.alpha = target;
    }

    CanvasGroup CreateOverlay()
    {
        var go = new GameObject("CutsceneFade");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        var g = go.AddComponent<CanvasGroup>();
        g.alpha = 1f;
        g.blocksRaycasts = false;
        var img = new GameObject("Black").AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        img.color = Color.black;
        img.raycastTarget = false;
        img.rectTransform.anchorMin = Vector2.zero;
        img.rectTransform.anchorMax = Vector2.one;
        img.rectTransform.offsetMin = Vector2.zero;
        img.rectTransform.offsetMax = Vector2.zero;
        return g;
    }
}
