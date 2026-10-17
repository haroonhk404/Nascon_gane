using System.Collections;
using UnityEngine;

/// Full-screen black CanvasGroup used to fade the screen out/in (e.g. for teleports).
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

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

    public IEnumerator FadeOut(float duration)
    {
        yield return Fade(group.alpha, 1f, duration);
        group.blocksRaycasts = true;
    }

    public IEnumerator FadeIn(float duration)
    {
        group.blocksRaycasts = false;
        yield return Fade(group.alpha, 0f, duration);
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }

        group.alpha = to;
    }
}
