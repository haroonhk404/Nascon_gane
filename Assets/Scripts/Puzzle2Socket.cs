// ===================== PUZZLE 2 =====================
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// PUZZLE 2 - Attachment point.
/// Player haath mein candle le kar 'radius' ke andar aaye to candle khud yahan lag jati hai.
/// Purana Candle.cs hi use hota hai, us mein koi change nahi chahiye.
/// Empty object pe lagao: is ki position/rotation = candle kahan aur kaise khari hogi.
/// </summary>
public class Puzzle2Socket : MonoBehaviour
{
    [Header("Attach")]
    [Tooltip("Player itne meter ke andar aaye to candle khud lag jaye")]
    public float radius = 1.2f;
    [Tooltip("Candle exactly kahan lagegi (khali = yehi object)")]
    public Transform snapPoint;
    public float snapDuration = 0.35f;
    [Tooltip("Lagte hi candle jal jaye (agar pehle se nahi jal rahi)")]
    public bool lightOnAttach = true;

    [Header("Visual (optional)")]
    [Tooltip("Jab tak khali hai ye dikhega (glow, particle waghera)")]
    public GameObject emptyIndicator;

    [Header("Audio (optional)")]
    public AudioSource audioSource;
    public AudioClip attachClip;

    [Header("Events")]
    public UnityEvent onFilled;

    [HideInInspector] public Puzzle2Manager manager;

    public Candle PlacedCandle { get; private set; }
    public bool IsFilled => PlacedCandle != null;
    public Transform SnapPoint => snapPoint ? snapPoint : transform;

    PlayerInteractor player;

    void Awake()
    {
        player = FindAnyObjectByType<PlayerInteractor>();
        if (emptyIndicator) emptyIndicator.SetActive(true);
    }

    void Update()
    {
        if (IsFilled || !player) return;
        if (manager && manager.Solved) return;
        if (manager && manager.strictOrder && manager.CurrentSocket != this) return;   // order zabardasti

        Candle held = player.HeldCandle;
        if (!held) return;

        Vector3 a = player.transform.position;
        Vector3 b = SnapPoint.position;
        a.y = 0f; b.y = 0f;

        if ((a - b).sqrMagnitude <= radius * radius)
            Attach(held);
    }

    void Attach(Candle candle)
    {
        PlacedCandle = candle;

        // Haath khali karo (purane Candle.cs ka public function)
        player.HoldCandle(null);

        // Asal size yaad rakho (haath ki bone ka scale alag hota hai)
        Vector3 worldScale = manager ? manager.GetOriginalScale(candle) : candle.transform.lossyScale;

        candle.transform.SetParent(SnapPoint, true);
        Vector3 ps = SnapPoint.lossyScale;
        if (ps.x != 0f && ps.y != 0f && ps.z != 0f)
            candle.transform.localScale = new Vector3(worldScale.x / ps.x, worldScale.y / ps.y, worldScale.z / ps.z);

        StartCoroutine(SnapRoutine(candle));

        if (emptyIndicator) emptyIndicator.SetActive(false);
        if (audioSource && attachClip) audioSource.PlayOneShot(attachClip);

        onFilled?.Invoke();
        if (manager) manager.NotifyFilled(this);
    }

    IEnumerator SnapRoutine(Candle candle)
    {
        Transform t = candle.transform;
        Vector3 p0 = t.localPosition;
        Quaternion r0 = t.localRotation;
        float time = 0f;
        float d = Mathf.Max(0.01f, snapDuration);

        while (time < d)
        {
            time += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, time / d);
            t.localPosition = Vector3.Lerp(p0, Vector3.zero, k);
            t.localRotation = Quaternion.Slerp(r0, Quaternion.identity, k);
            yield return null;
        }
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;

        if (lightOnAttach && !candle.IsLit) candle.SetLit(true);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = IsFilled ? new Color(0.3f, 1f, 0.3f, 0.7f) : new Color(1f, 0.7f, 0.1f, 0.7f);
        Vector3 c = SnapPoint.position;
        Gizmos.DrawWireSphere(c, 0.1f);

        const int seg = 32;
        Vector3 prev = c + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float ang = i * Mathf.PI * 2f / seg;
            Vector3 next = c + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
