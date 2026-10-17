using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main menu buttons. Canvas (ya kisi bhi object) pe lagao.
/// PLAY button -> OnClick -> MainMenu.PlayGame
/// QUIT button -> OnClick -> MainMenu.QuitGame
/// Play: UI gayab -> camera Point 1 se Point 2 -> black fade -> game scene.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Play")]
    [Tooltip("Play dabane pe ye scene load hoga (naam bilkul file jaisa)")]
    public string gameSceneName = "SampleScene";
    public float fadeTime = 1f;

    [Header("Camera Move (optional)")]
    [Tooltip("Main Camera wala MenuCameraDrift. Khali = seedha fade")]
    public MenuCameraDrift cameraMove;
    [Tooltip("Menu UI (buttons, title) - Play pe ye gayab hoga. Panel pe CanvasGroup lagao")]
    public CanvasGroup menuUI;
    public float uiFadeTime = 0.4f;
    [Tooltip("Camera ke pahunchne se kitna pehle black fade shuru ho (overlap = smooth)")]
    public float fadeStartsBeforeEnd = 0.8f;

    [Header("Audio (optional)")]
    public AudioSource clickSource;
    public AudioClip clickClip;
    [Tooltip("Menu music jo fade ke saath aahista band ho")]
    public AudioSource menuMusic;

    [Header("Cursor")]
    public bool showCursor = true;

    bool busy;

    void Start()
    {
        Time.timeScale = 1f;
        if (showCursor)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        if (menuUI) { menuUI.alpha = 1f; menuUI.interactable = true; menuUI.blocksRaycasts = true; }
    }

    public void PlayGame()
    {
        if (busy) return;
        busy = true;
        Click();
        StartCoroutine(PlayRoutine());
    }

    public void QuitGame()
    {
        if (busy) return;
        busy = true;
        Click();
        Debug.Log("[MainMenu] Quit");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator PlayRoutine()
    {
        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError($"[MainMenu] Scene '{gameSceneName}' Build Profiles ki Scene List mein nahi hai!");
            busy = false;
            yield break;
        }

        // 1) Menu UI gayab
        if (menuUI)
        {
            menuUI.interactable = false;
            menuUI.blocksRaycasts = false;
            float u = 0f;
            while (u < uiFadeTime)
            {
                u += Time.unscaledDeltaTime;
                menuUI.alpha = 1f - Mathf.Clamp01(u / uiFadeTime);
                yield return null;
            }
            menuUI.alpha = 0f;
        }

        // 2) Camera Point 1 -> Point 2 (aakhir ke qareeb black fade shuru)
        bool moving = cameraMove && cameraMove.playPoint;
        if (moving)
        {
            StartCoroutine(cameraMove.MoveToPlayPoint());
            float wait = Mathf.Max(0f, cameraMove.moveDuration - fadeStartsBeforeEnd);
            float w = 0f;
            while (w < wait) { w += Time.unscaledDeltaTime; yield return null; }
        }

        // 3) Black fade
        CanvasGroup overlay = ScreenFader.Instance ? null : CreateOverlay();
        if (ScreenFader.Instance) StartCoroutine(ScreenFader.Instance.FadeOut(fadeTime));

        float startVol = menuMusic ? menuMusic.volume : 0f;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeTime);
            if (overlay) overlay.alpha = k;
            if (menuMusic) menuMusic.volume = Mathf.Lerp(startVol, 0f, k);
            yield return null;
        }

        // Camera abhi bhi chal raha ho to khatam hone do
        while (moving && cameraMove.IsMoving) yield return null;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        SceneManager.LoadScene(gameSceneName);
    }

    void Click()
    {
        if (clickSource && clickClip) clickSource.PlayOneShot(clickClip);
    }

    CanvasGroup CreateOverlay()
    {
        var go = new GameObject("MenuFade");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        var group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = true;
        var img = new GameObject("Black").AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        img.color = Color.black;
        img.rectTransform.anchorMin = Vector2.zero;
        img.rectTransform.anchorMax = Vector2.one;
        img.rectTransform.offsetMin = Vector2.zero;
        img.rectTransform.offsetMax = Vector2.zero;
        return group;
    }
}
