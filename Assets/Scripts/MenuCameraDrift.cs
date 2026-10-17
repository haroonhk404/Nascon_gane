using System.Collections;
using UnityEngine;

/// <summary>
/// Main menu camera.
/// Menu mein 'cameraPoint' pe static khara rehta hai.
/// Play dabane pe (MainMenu se) 'playPoint' tak smoothly move karta hai.
/// Main Camera pe lagao.
/// </summary>
public class MenuCameraDrift : MonoBehaviour
{
    [Header("Menu")]
    [Tooltip("Menu mein camera yahan se dekhe (Point 1)")]
    public Transform cameraPoint;

    [Header("Play dabane pe")]
    [Tooltip("Play pe camera yahan tak jaye (Point 2). Khali = camera hile ga nahi")]
    public Transform playPoint;
    [Tooltip("Point 1 se Point 2 tak kitne second")]
    public float moveDuration = 3f;
    [Tooltip("Movement ka andaaz: shuru aahista, beech tez, aakhir aahista")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public bool IsMoving { get; private set; }

    void Start()
    {
        if (cameraPoint)
            transform.SetPositionAndRotation(cameraPoint.position, cameraPoint.rotation);
    }

    /// MainMenu Play pe chalata hai
    public IEnumerator MoveToPlayPoint()
    {
        if (!playPoint) yield break;
        IsMoving = true;

        Vector3 p0 = transform.position;
        Quaternion r0 = transform.rotation;
        float d = Mathf.Max(0.01f, moveDuration);
        float t = 0f;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float k = ease.Evaluate(Mathf.Clamp01(t / d));
            transform.SetPositionAndRotation(
                Vector3.LerpUnclamped(p0, playPoint.position, k),
                Quaternion.SlerpUnclamped(r0, playPoint.rotation, k));
            yield return null;
        }
        transform.SetPositionAndRotation(playPoint.position, playPoint.rotation);
        IsMoving = false;
    }

    void OnDrawGizmos()
    {
        if (cameraPoint && playPoint)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(cameraPoint.position, playPoint.position);
            Gizmos.DrawWireSphere(cameraPoint.position, 0.2f);
            Gizmos.DrawWireSphere(playPoint.position, 0.2f);
        }
    }
}
