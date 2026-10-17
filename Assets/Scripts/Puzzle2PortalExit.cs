// ===================== PUZZLE 2 =====================
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// PUZZLE 2 - Portal pe lagao.
/// Portal nazar aane ke baad player 'radius' ke andar aaye -> screen black fade -> 'sceneToLoad' load.
/// Scene mein ScreenFader ho to wahi use hota hai, warna ye khud black screen bana leta hai.
/// </summary>
public class Puzzle2PortalExit : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Shuru wale cutscene scene ka naam (bilkul file ke naam jaisa)")]
    public string sceneToLoad = "";

    [Header("Trigger")]
    [Tooltip("Player portal ke itne meter ke andar aaye to chale")]
    public float radius = 1.5f;
    [Tooltip("Portal nazar aane ke kitni der baad kaam kare (cutscene ke dauran galti se na chale)")]
    public float armDelay = 1f;

    [Header("Fade")]
    public float fadeDuration = 1.2f;
    [Tooltip("Black screen pe kitni der ruke phir scene load ho")]
    public float holdBlack = 0.5f;

    [Header("Controls (optional)")]
    [Tooltip("Fade ke dauran band: ThirdPersonController, PlayerInteractor")]
    public MonoBehaviour[] disableOnEnter;

    [Header("Audio (optional)")]
    public AudioSource audioSource;
    public AudioClip enterClip;

    Transform player;
    float armTimer;
    bool used;

    void OnEnable()
    {
        armTimer = armDelay;
        if (!player)
        {
            var pi = FindAnyObjectByType<PlayerInteractor>();
            if (pi) player = pi.transform;
        }
    }

    void Update()
    {
        if (used || !player) return;

        if (armTimer > 0f) { armTimer -= Time.deltaTime; return; }

        Vector3 a = player.position;
        Vector3 b = transform.position;
        a.y = 0f; b.y = 0f;

        if ((a - b).sqrMagnitude <= radius * radius)
        {
            used = true;
            StartCoroutine(ExitRoutine());
        }
    }

    IEnumerator ExitRoutine()
    {
        Debug.Log($"[Puzzle2] Portal: '{sceneToLoad}' load kar raha hoon");

        foreach (var m in disableOnEnter) if (m) m.enabled = false;
        if (audioSource && enterClip) audioSource.PlayOneShot(enterClip);

        var fader = ScreenFader.Instance;
        if (fader)
        {
            yield return fader.FadeOut(fadeDuration);
        }
        else
        {
            // Scene mein ScreenFader nahi -> khud black screen banao
            var group = CreateBlackOverlay();
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(t / fadeDuration);
                yield return null;
            }
            group.alpha = 1f;
        }

        yield return new WaitForSecondsRealtime(holdBlack);

        if (!string.IsNullOrEmpty(sceneToLoad) && Application.CanStreamedLevelBeLoaded(sceneToLoad))
        {
            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            Debug.LogError($"[Puzzle2] Scene '{sceneToLoad}' load nahi ho sakta. Naam check karo aur File > Build Profiles > Scene List mein add karo.");
        }
    }

    CanvasGroup CreateBlackOverlay()
    {
        var go = new GameObject("PortalFade");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        var group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var img = new GameObject("Black").AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        img.color = Color.black;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return group;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
