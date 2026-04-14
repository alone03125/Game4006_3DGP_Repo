using UnityEngine;

/// <summary>
/// Shorten the last point of LineRenderer by occlusion, so that ColliderSync can synchronize the length of the Laser Trigger.
/// Suggest: The Collider of the obstacle should be non-Trigger and placed in the Layer specified by blockMask.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class LaserLineOccluder : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LineRenderer line;

    /// <summary>
    /// If specified: Use "Point 0 -> Reference Point" as the maximum ray end point each frame (suitable for Laser root objects that rotate/move).
    /// If not specified: Use the last point snapped in OnEnable as the maximum end point.
    /// </summary>
    [SerializeField] private Transform beamEndReference;

    [Header("Raycast")]
    [SerializeField] private LayerMask blockMask;
    [SerializeField] private QueryTriggerInteraction hitTriggers = QueryTriggerInteraction.Ignore;

    [Tooltip("After hitting, shorten the ray inward a little to avoid face jitter/penetration judgment")]
    [SerializeField] private float skin = 0.02f;

    [Tooltip("Add a little extra distance to avoid the error of float difference between maxDistance and segment length, causing it to be missed")]
    [SerializeField] private float castEpsilon = 0.02f;

    private Vector3 _snapLocalEnd;   // useWorldSpace == false
    private Vector3 _snapWorldEnd; // useWorldSpace == true
    private bool _snapped;

    private void Reset()
    {
        line = GetComponent<LineRenderer>();
    }

    private void OnEnable()
    {
        SnapshotMaxEndFromLineRenderer();
    }

    [ContextMenu("Recapture Max Beam End (From LineRenderer)")]
    public void SnapshotMaxEndFromLineRenderer()
    {
        if (line == null || line.positionCount < 2)
        {
            _snapped = false;
            return;
        }

        int last = line.positionCount - 1;

        if (line.useWorldSpace)
        {
            _snapWorldEnd = line.GetPosition(last);
            _snapped = true;
        }
        else
        {
            _snapLocalEnd = line.GetPosition(last);
            _snapped = true;
        }
    }

    private void LateUpdate()
    {
        if (line == null || line.positionCount < 2) return;

        Vector3 p0w = GetWorldPoint(0);
        Vector3 p1MaxW = GetMaxEndWorld();

        Vector3 dir = p1MaxW - p0w;
        float len = dir.magnitude;
        if (len < 0.0001f) return;

        dir /= len;

        float castDist = len + castEpsilon;

        Vector3 endW = p1MaxW;

        if (Physics.Raycast(p0w, dir, out RaycastHit hit, castDist, blockMask, hitTriggers))
        {
            float d = Mathf.Clamp(hit.distance - skin, 0f, len);
            endW = p0w + dir * d;
        }

        SetWorldEndPoint(endW);
    }

    private Vector3 GetWorldPoint(int index)
    {
        Vector3 p = line.GetPosition(index);
        return line.useWorldSpace ? p : line.transform.TransformPoint(p);
    }

    private Vector3 GetMaxEndWorld()
    {
        if (beamEndReference != null)
            return beamEndReference.position;

        if (!_snapped)
            SnapshotMaxEndFromLineRenderer();

        if (!_snapped)
            return GetWorldPoint(line.positionCount - 1);

        return line.useWorldSpace ? _snapWorldEnd : line.transform.TransformPoint(_snapLocalEnd);
    }

    private void SetWorldEndPoint(Vector3 endW)
    {
        int last = line.positionCount - 1;

        if (line.useWorldSpace)
        {
            line.SetPosition(last, endW);
        }
        else
        {
            line.SetPosition(last, line.transform.InverseTransformPoint(endW));
        }
    }
}