using UnityEngine;

[ExecuteAlways]
public class ColliderSync : MonoBehaviour
{
    [SerializeField] private LineRenderer line;
    [SerializeField] private Transform colliderPivot; // 這個物件上放 BoxCollider
    [SerializeField] private BoxCollider box;
    [SerializeField] private float thickness = 0.2f;  // 雷射粗細(碰撞厚度)
    [SerializeField] private bool updateEveryFrame = true;

    void Reset()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        if (colliderPivot != null && box == null) box = colliderPivot.GetComponent<BoxCollider>();
    }

    void OnValidate()
    {
        SyncCollider();
    }

    void LateUpdate()
    {
        if (updateEveryFrame) SyncCollider();
    }

    [ContextMenu("Sync Collider Now")]
    public void SyncCollider()
    {
        if (line == null || colliderPivot == null || box == null) return;
        if (line.positionCount < 2) return;

        Vector3 p0 = GetWorldPoint(0);
        Vector3 p1 = GetWorldPoint(line.positionCount - 1);

        Vector3 dir = p1 - p0;
        float len = dir.magnitude;
        if (len < 0.0001f) return;

        colliderPivot.position = (p0 + p1) * 0.5f;
        colliderPivot.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        box.center = Vector3.zero;
        box.size = new Vector3(thickness, thickness, len);
    }

    Vector3 GetWorldPoint(int i)
    {
        Vector3 p = line.GetPosition(i);
        return line.useWorldSpace ? p : line.transform.TransformPoint(p);
    }
}