using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Puzzle room ke andar (GuidePath ke point 10 ke paas) ek invisible box pe lagao.
/// Player andar aaye -> pet guiding shuru (ya band) kar deta hai.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PetGuideTrigger : MonoBehaviour
{
    public PetGuide pet;

    public enum Action { StartGuiding, StopGuiding }
    public Action action = Action.StartGuiding;

    [Tooltip("Room mein aane ke kitni der baad pet pehli dafa guide kare")]
    public float firstGuideDelay = 1.5f;
    [Tooltip("Sirf ek baar chale")]
    public bool oneShot = true;

    [Tooltip("Optional: player andar aaye to aur kuch bhi karwana ho")]
    public UnityEvent onPlayerEnter;

    bool done;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        if (!pet) pet = FindAnyObjectByType<PetGuide>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (done && oneShot) return;
        if (!other.GetComponentInParent<PlayerInteractor>()) return;   // sirf player

        done = true;

        if (pet)
        {
            if (action == Action.StartGuiding) pet.StartGuiding(firstGuideDelay);
            else pet.StopGuiding();
        }

        onPlayerEnter?.Invoke();
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (!box) return;
        Gizmos.color = action == Action.StartGuiding ? new Color(1f, 0.5f, 0f, 0.25f) : new Color(0.3f, 0.5f, 1f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
    }
}
