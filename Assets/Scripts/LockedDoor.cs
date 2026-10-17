using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Door pe lagao (collider zaroori). Locked hai jab tak puzzle solve na ho.
/// Unlock hone ke baad E press -> fade -> player teleportTarget pe chala jata hai.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LockedDoor : MonoBehaviour, IInteractable
{
    public bool isLocked = true;

    [Header("Next Scene (optional)")]
    [Tooltip("Agar yahan scene ka naam likha ho to teleport ki jagah ye scene load hoga. Khali = teleport")]
    public string nextSceneName = "";

    [Header("Teleport")]
    [Tooltip("Dosri position - empty object jahan player jayega (rotation bhi use hoti hai)")]
    public Transform teleportTarget;
    public ScreenFader fader;
    public PetGuide pet;
    [Tooltip("Fade ke dauran disable (e.g. FirstPersonController, PlayerInteractor)")]
    public MonoBehaviour[] disableDuringFade;

    [Header("Guide")]
    [Tooltip("Pet door ke saamne kahan ruke (optional)")]
    public Transform guidePoint;

    [Header("Prompts")]
    public string lockedText = "Door is Locked";
    public string unlockText = "[E] Unlock & Open Door";

    [Header("Audio (optional)")]
    public AudioSource audioSource;
    public AudioClip lockedClip;
    public AudioClip openClip;

    bool used;

    public Transform GuidePoint => guidePoint ? guidePoint : transform;
    public bool Used => used;

    public void Unlock() => isLocked = false;

    int progressCurrent, progressRequired;
    string progressLabel = "";

    /// Puzzle manager progress batata hai: "Door is Locked (3/9 candles placed)"
    public void SetProgress(int current, int required, string label)
    {
        progressCurrent = current;
        progressRequired = required;
        progressLabel = label;
    }

    public string GetPrompt(PlayerInteractor player)
    {
        if (used) return "";
        if (isLocked)
            return progressRequired > 0
                ? $"{lockedText}  ({progressCurrent}/{progressRequired} {progressLabel})"
                : lockedText;
        return unlockText;
    }

    public void Interact(PlayerInteractor player)
    {
        if (used) return;

        if (isLocked)
        {
            Play(lockedClip);
            player.ShowMessage("Door is Locked - light the candles");
            return;
        }

        used = true;
        StartCoroutine(OpenRoutine(player));
    }

    IEnumerator OpenRoutine(PlayerInteractor player)
    {
        Play(openClip);
        SetControls(false);

        if (!fader) fader = ScreenFader.Instance;   // scene wala fader khud le lo

        if (fader) yield return fader.FadeOut();

        // Naya scene load karna hai?
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            if (Application.CanStreamedLevelBeLoaded(nextSceneName))
            {
                SceneManager.LoadScene(nextSceneName);
                yield break;
            }
            Debug.LogError($"LockedDoor: scene '{nextSceneName}' Build Profiles ki Scene List mein nahi hai! Teleport kar raha hoon.");
        }

        Teleport(player.transform);
        if (pet) pet.SnapToPlayer();

        if (fader) yield return fader.FadeIn();

        SetControls(true);
    }

    void Teleport(Transform playerT)
    {
        if (!teleportTarget) { Debug.LogWarning("LockedDoor: teleportTarget set nahi hai"); return; }

        // CharacterController position override karta hai, isliye pehle disable
        var cc = playerT.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;

        playerT.SetPositionAndRotation(teleportTarget.position,
            Quaternion.Euler(0f, teleportTarget.eulerAngles.y, 0f));

        var rb = playerT.GetComponent<Rigidbody>();
        if (rb) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

        if (cc) cc.enabled = true;
    }

    void SetControls(bool on)
    {
        foreach (var m in disableDuringFade) if (m) m.enabled = on;
    }

    void Play(AudioClip clip)
    {
        if (audioSource && clip) audioSource.PlayOneShot(clip);
    }
}
