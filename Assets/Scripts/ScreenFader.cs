using System.Collections;
using UnityEngine;

/// <summary>
/// Canvas ke andar full-screen black Image banao, us pe CanvasGroup + ye script.
/// Purane scripts (Portal.cs) ke liye bhi compatible: ScreenFader.Instance, FadeOut(duration), FadeIn(duration)
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    public float fadeDuration = 0.6f;
    public float holdBlack = 0.3f;

    CanvasGroup group;

    void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---- Default duration (LockedDoor use karta hai) ----
    public IEnumerator FadeOut()
    {
        yield return Fade(0f, 1f, fadeDuration);
        yield return new WaitForSecondsRealtime(holdBlack);
    }

    public IEnumerator FadeIn()
    {
        yield return Fade(1f, 0f, fadeDuration);
    }

    // ---- Custom duration (Portal.cs use karta hai) ----
    public IEnumerator FadeOut(float duration)
    {
        yield return Fade(0f, 1f, duration);
    }

    public IEnumerator FadeIn(float duration)
    {
        yield return Fade(1f, 0f, duration);
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        if (!group) group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = true;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, duration > 0f ? t / duration : 1f);
            yield return null;
        }
        group.alpha = to;
        group.blocksRaycasts = to > 0.01f;
    }
}
