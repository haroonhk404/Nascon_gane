using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Pause menu. Ek ALAG object (jaise 'PauseManager') pe lagao, pause panel ke andar NAHI.
/// Esc / P -> pause UI + game ruk jaye. Esc dobara ya RESUME -> wapis.
/// Buttons khud naam se dhoond kar jud jaate hain (Resume / Restart / Quit),
/// ya Inspector mein khud assign kar do. On Click mein kuch lagane ki zaroorat nahi.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Pause wala panel (RESUME / RESTART / QUIT buttons wala). Shuru mein band rahega")]
    public GameObject pausePanel;

    [Header("Buttons (khali = naam se khud dhoond lega)")]
    public Button resumeButton;
    public Button restartButton;
    public Button quitButton;

    [Header("Pause ke dauran band")]
    [Tooltip("ThirdPersonController, ThirdPersonCamera (Main Camera wala), PlayerInteractor")]
    public MonoBehaviour[] disableWhilePaused;

    [Tooltip("ON = pause pe camera/mouse-look scripts (naam mein Camera, Look, Cursor) khud band ho jayein")]
    public bool autoDisableCameraScripts = true;

    [Header("Quit")]
    [Tooltip("Khali = game band (Application.Quit). Scene naam likho (jaise 'main menu') to wahan jaye")]
    public string quitToScene = "";

    [Header("Options")]
    [Tooltip("Pause pe saari awaaz ruk jaye")]
    public bool pauseAudio = true;
    public KeyCode pauseKey = KeyCode.Escape;
    [Tooltip("Backup key (Editor mein Esc kabhi kabhi Unity khud le leti hai)")]
    public KeyCode altPauseKey = KeyCode.P;
    [Tooltip("ON = pause mein click karne pe Console mein dikhe mouse ke neeche kya hai")]
    public bool debugClicks = true;

    public static bool IsPaused { get; private set; }

    CursorLockMode prevLock;
    bool prevVisible;

    void Start()
    {
        if (!pausePanel)
            Debug.LogError("[PauseMenu] Pause Panel assign nahi hai!");
        else if (transform.IsChildOf(pausePanel.transform))
            Debug.LogError("[PauseMenu] Ye script Pause Panel ke ANDAR lagi hai! Script ko alag object pe lagao.");

        EnsureClickable();
        HookButtons();

        if (pausePanel) pausePanel.SetActive(false);
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        Debug.Log($"[PauseMenu] Ready on '{name}'. {pauseKey} ya {altPauseKey} se pause.");
    }

    void Update()
    {
        if (PausePressed())
        {
            Debug.Log($"[PauseMenu] Pause key dabi -> {(IsPaused ? "Resume" : "Pause")}");
            if (IsPaused) Resume();
            else Pause();
        }

        if (IsPaused)
        {
            KeepCursorFree();
            if (debugClicks && LeftClickPressed()) LogWhatIsUnderMouse();
        }
    }

    void LateUpdate()
    {
        // Doosri scripts ke baad bhi check (unka LateUpdate lock kar sakta hai)
        if (IsPaused) KeepCursorFree();
    }

    Vector2 lastFreeMousePos;
    bool haveMousePos;

    void KeepCursorFree()
    {
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
#if ENABLE_INPUT_SYSTEM
            // Lock ne cursor center pe phenk diya -> wapis purani jagah
            if (haveMousePos && Mouse.current != null) Mouse.current.WarpCursorPosition(lastFreeMousePos);
#endif
            if (!warnedLock)
            {
                warnedLock = true;
                Debug.LogWarning("[PauseMenu] Koi script pause ke dauran mouse LOCK kar rahi hai (cursor center pe jaata hai). Us script ko 'Disable While Paused' mein daalo.");
            }
            return;
        }
        if (!Cursor.visible) Cursor.visible = true;
        lastFreeMousePos = MousePos();
        haveMousePos = true;
    }

    bool warnedLock;
    readonly List<MonoBehaviour> autoDisabled = new List<MonoBehaviour>();

    static readonly string[] cameraKeywords = { "camera", "look", "cursor", "orbit", "freelook" };

    void AutoDisableCameraScripts(bool disable)
    {
        if (!disable)
        {
            foreach (var m in autoDisabled) if (m) m.enabled = true;
            autoDisabled.Clear();
            return;
        }

        foreach (var m in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (!m || !m.enabled || m == this) continue;
            if (m is UIBehaviour) continue;                          // UI components nahi
            string n = m.GetType().Name.ToLower();
            if (n.Contains("pause") || n.Contains("menu") || n.Contains("cutscene")) continue;
            if (m.GetType().Namespace != null && m.GetType().Namespace.StartsWith("UnityEngine")) continue;

            foreach (var k in cameraKeywords)
            {
                if (n.Contains(k))
                {
                    m.enabled = false;
                    autoDisabled.Add(m);
                    Debug.Log($"[PauseMenu] Pause: '{m.GetType().Name}' ({m.name}) band kiya");
                    break;
                }
            }
        }
    }

    // ================= BUTTONS =================
    void HookButtons()
    {
        if (!pausePanel) return;

        var all = pausePanel.GetComponentsInChildren<Button>(true);
        if (!resumeButton) resumeButton = FindButton(all, "resume", "continue", "play");
        if (!restartButton) restartButton = FindButton(all, "restart", "retry", "again");
        if (!quitButton) quitButton = FindButton(all, "quit", "exit", "menu");

        Hook(resumeButton, Resume, "Resume");
        Hook(restartButton, Restart, "Restart");
        Hook(quitButton, Quit, "Quit");

        // Clicks sirf button ki apni image pe lagein - texts / sajawat / bare boxes clicks na churayein
        var targets = new HashSet<Graphic>();
        foreach (var b in all) if (b.targetGraphic) targets.Add(b.targetGraphic);
        int off = 0;
        foreach (var g in pausePanel.GetComponentsInChildren<Graphic>(true))
        {
            if (targets.Contains(g)) { g.raycastTarget = true; continue; }
            if (g.raycastTarget) { g.raycastTarget = false; off++; }
        }
        if (off > 0) Debug.Log($"[PauseMenu] {off} texts/images ke clicks band kiye (sirf buttons click honge)");

        // Overlap check: kisi button ka click area dusre se takra raha hai?
        for (int i = 0; i < all.Length; i++)
            for (int j = i + 1; j < all.Length; j++)
                if (Overlaps(all[i], all[j]))
                    Debug.LogWarning($"[PauseMenu] '{all[i].name}' aur '{all[j].name}' ke click area OVERLAP kar rahe hain! Scale 1 karke Width/Height aur Pos Y theek karo.");

        if (all.Length == 0)
            Debug.LogError("[PauseMenu] Pause panel mein koi Unity 'Button' component nahi mila! Package ke buttons shayad sirf Image hain - un pe Add Component > Button lagao.");
    }

    static bool Overlaps(Button a, Button b)
    {
        var ra = a.targetGraphic ? a.targetGraphic.rectTransform : a.transform as RectTransform;
        var rb = b.targetGraphic ? b.targetGraphic.rectTransform : b.transform as RectTransform;
        if (!ra || !rb) return false;
        return WorldRect(ra).Overlaps(WorldRect(rb));
    }

    static Rect WorldRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    static Button FindButton(Button[] all, params string[] keys)
    {
        foreach (var b in all)
        {
            string n = b.name.ToLower();
            var tmp = b.GetComponentInChildren<TMP_Text>(true);
            var txt = b.GetComponentInChildren<Text>(true);
            string label = ((tmp ? tmp.text : "") + " " + (txt ? txt.text : "")).ToLower();
            foreach (var k in keys)
                if (n.Contains(k) || label.Contains(k)) return b;
        }
        return null;
    }

    void Hook(Button b, UnityEngine.Events.UnityAction action, string label)
    {
        if (!b) { Debug.LogWarning($"[PauseMenu] {label} button nahi mila - Inspector mein '{label} Button' khud assign karo"); return; }
        b.onClick.RemoveListener(action);
        b.onClick.AddListener(action);
        b.interactable = true;
        Debug.Log($"[PauseMenu] {label} -> '{b.name}' se jud gaya");
    }

    // ================= CLICKABLE FIXES =================
    void EnsureClickable()
    {
        var es = FindAnyObjectByType<EventSystem>();
        if (!es)
        {
            es = new GameObject("EventSystem").AddComponent<EventSystem>();
            Debug.Log("[PauseMenu] EventSystem nahi tha - bana diya");
        }

#if ENABLE_INPUT_SYSTEM
        var old = es.GetComponent<StandaloneInputModule>();
        if (old) { DestroyImmediate(old); Debug.Log("[PauseMenu] StandaloneInputModule hata diya"); }
        if (!es.GetComponent<InputSystemUIInputModule>()) es.gameObject.AddComponent<InputSystemUIInputModule>();
#else
        if (!es.GetComponent<StandaloneInputModule>()) es.gameObject.AddComponent<StandaloneInputModule>();
#endif

        if (!pausePanel) return;

        // Panel ke upar wala canvas + panel ke andar ke saare canvases pe raycaster
        var canvases = new List<Canvas>(pausePanel.GetComponentsInChildren<Canvas>(true));
        var parentCanvas = pausePanel.GetComponentInParent<Canvas>(true);
        if (parentCanvas) { canvases.Add(parentCanvas); canvases.Add(parentCanvas.rootCanvas); }
        else Debug.LogError("[PauseMenu] Pause Panel kisi Canvas ke andar nahi hai!");

        foreach (var c in canvases)
        {
            if (c && !c.GetComponent<GraphicRaycaster>())
            {
                c.gameObject.AddComponent<GraphicRaycaster>();
                Debug.Log($"[PauseMenu] '{c.name}' pe Graphic Raycaster laga diya");
            }
        }

        // Package ki animations pause (timeScale 0) mein bhi chalein
        foreach (var a in pausePanel.GetComponentsInChildren<Animator>(true))
            a.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    /// Panel ke CanvasGroups (package ka fade) clickable rahein
    void ForcePanelInteractable()
    {
        if (!pausePanel) return;
        Transform t = pausePanel.transform;
        while (t)
        {
            var g = t.GetComponent<CanvasGroup>();
            if (g) { g.interactable = true; g.blocksRaycasts = true; if (g.alpha < 0.01f) g.alpha = 1f; }
            t = t.parent;
        }
        foreach (var g in pausePanel.GetComponentsInChildren<CanvasGroup>(true))
        {
            g.interactable = true;
            g.blocksRaycasts = true;
        }
    }

    // ================= PAUSE / RESUME =================
    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        if (pausePanel)
        {
            pausePanel.SetActive(true);
            pausePanel.transform.SetAsLastSibling();   // baqi UI ke upar
            ForcePanelInteractable();
        }
        Time.timeScale = 0f;
        if (pauseAudio) AudioListener.pause = true;
        SetControls(false);
        if (autoDisableCameraScripts) AutoDisableCameraScripts(true);

        prevLock = Cursor.lockState;
        prevVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        haveMousePos = false;
    }

    public void Resume()
    {
        if (!IsPaused) return;
        Debug.Log("[PauseMenu] Resume");
        IsPaused = false;

        if (pausePanel) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SetControls(true);
        AutoDisableCameraScripts(false);

        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        Cursor.lockState = prevLock;
        Cursor.visible = prevVisible;
    }

    public void Restart()
    {
        Debug.Log("[PauseMenu] Restart");
        ResetState();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void Quit()
    {
        Debug.Log("[PauseMenu] Quit");
        ResetState();

        if (!string.IsNullOrEmpty(quitToScene))
        {
            if (Application.CanStreamedLevelBeLoaded(quitToScene))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SceneManager.LoadScene(quitToScene);
                return;
            }
            Debug.LogError($"[PauseMenu] Scene '{quitToScene}' Scene List mein nahi hai. Game band kar raha hoon.");
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ResetState()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void SetControls(bool on)
    {
        if (disableWhilePaused == null) return;
        foreach (var m in disableWhilePaused) if (m) m.enabled = on;
    }

    // ================= DEBUG =================
    void LogWhatIsUnderMouse()
    {
        if (!EventSystem.current) { Debug.LogError("[PauseMenu] Click: EventSystem.current NULL hai"); return; }

        var data = new PointerEventData(EventSystem.current) { position = MousePos() };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);

        if (hits.Count == 0)
        {
            Debug.LogWarning("[PauseMenu] Click: mouse ke neeche koi UI nahi mila (Graphic Raycaster / Raycast Target check karo)");
            return;
        }

        string list = "";
        for (int i = 0; i < Mathf.Min(4, hits.Count); i++) list += $"\n   {i + 1}. {hits[i].gameObject.name}  (Canvas: {hits[i].module.name})";
        var btn = hits[0].gameObject.GetComponentInParent<Button>();
        Debug.Log($"[PauseMenu] Click ke neeche (upar wala pehle):{list}\n   -> Button: {(btn ? btn.name + (btn.interactable ? "" : " (interactable OFF!)") : "KOI NAHI - koi cheez button ko dhak rahi hai")}");
    }

    Vector2 MousePos()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    bool LeftClickPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    bool PausePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        return IsDown(kb, pauseKey) || IsDown(kb, altPauseKey);
#else
        return Input.GetKeyDown(pauseKey) || Input.GetKeyDown(altPauseKey);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    static bool IsDown(Keyboard kb, KeyCode code)
    {
        switch (code)
        {
            case KeyCode.None: return false;
            case KeyCode.Escape: return kb.escapeKey.wasPressedThisFrame;
            case KeyCode.P: return kb.pKey.wasPressedThisFrame;
            case KeyCode.Tab: return kb.tabKey.wasPressedThisFrame;
            case KeyCode.Backspace: return kb.backspaceKey.wasPressedThisFrame;
        }
        var key = kb.FindKeyOnCurrentKeyboardLayout(code.ToString());
        return key != null && key.wasPressedThisFrame;
    }
#endif

    void OnDestroy()
    {
        if (IsPaused) ResetState();
    }
}
