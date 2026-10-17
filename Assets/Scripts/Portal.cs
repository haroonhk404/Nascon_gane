using System.Collections;
using UnityEngine;

/// On PortalEight: when the Player enters the trigger, fades to black, teleports the
/// player to 'destination' (position + rotation), then fades back in.
[RequireComponent(typeof(Collider))]
public class Portal : MonoBehaviour
{
    [Header("Destination")]
    public Transform destination;

    [Header("Timing")]
    public float fadeOutDuration = 0.5f;
    public float fadeInDuration = 0.5f;
    public float holdAtBlackDuration = 0.1f;

    bool isTeleporting;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (isTeleporting) return;
        if (!other.CompareTag("Player")) return;
        if (destination == null) return;

        isTeleporting = true;
        StartCoroutine(TeleportRoutine(other.gameObject));
    }

    IEnumerator TeleportRoutine(GameObject player)
    {
        ScreenFader fader = ScreenFader.Instance;
        if (fader != null)
            yield return fader.FadeOut(fadeOutDuration);

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.transform.SetPositionAndRotation(destination.position, destination.rotation);

        if (cc != null) cc.enabled = true;

        if (holdAtBlackDuration > 0f)
            yield return new WaitForSeconds(holdAtBlackDuration);

        if (fader != null)
            yield return fader.FadeIn(fadeInDuration);

        isTeleporting = false;
    }
}
