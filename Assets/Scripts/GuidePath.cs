using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// Put this on an empty parent object (e.g. "GuidePath").
/// Every CHILD empty object is a point on the path.
/// Order = order in the Hierarchy (top child = first point). Names don't matter.
public class GuidePath : MonoBehaviour
{
    public Color gizmoColor = new Color(1f, 0.5f, 0.1f, 1f);
    public float gizmoSize = 0.3f;

    public int Count => transform.childCount;
    public Vector3 GetPoint(int i) => transform.GetChild(i).position;

    // Draws the path in the Scene view (not visible in the game)
    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        for (int i = 0; i < transform.childCount; i++)
        {
            Vector3 p = transform.GetChild(i).position;
            Gizmos.DrawSphere(p, gizmoSize);
            if (i > 0) Gizmos.DrawLine(transform.GetChild(i - 1).position, p);
#if UNITY_EDITOR
            Handles.Label(p + Vector3.up * (gizmoSize + 0.3f), (i + 1).ToString());
#endif
        }
    }
}
