using UnityEngine;
using UnityEngine.Events;

public class LazerFreeze : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip("Open: Control all laser components in the entire subtree (suitable for ALLLAZER).")]
    [SerializeField] private bool controlEntireSubtree = true;

    [Header("Optional single targets（Only used when controlEntireSubtree is closed）")]
    [SerializeField] private LazerSweep sweep;
    [SerializeField] private LazerRotate rotate;
    [SerializeField] private ColliderSync colliderSync;
    [SerializeField] private Lazer laserDamage;

    [Header("Events")]
    [SerializeField] private UnityEvent onLaserFrozen;
    [SerializeField] private UnityEvent onLaserUnfrozen;

    private bool _frozen;

    private LazerSweep[] _sweeps;
    private LazerRotate[] _rotates;
    private ColliderSync[] _colliderSyncs;
    private Lazer[] _lazers;

    private bool[] _sweepWasEnabled;
    private bool[] _rotateWasEnabled;
    private bool[] _colliderSyncWasEnabled;
    private bool[] _lazerWasEnabled;

    public bool IsFrozen => _frozen;

    private void Reset() => AutoWire();

    private void Awake() => AutoWire();

    private void AutoWire()
    {
        if (controlEntireSubtree)
        {
            _sweeps = GetComponentsInChildren<LazerSweep>(true);
            _rotates = GetComponentsInChildren<LazerRotate>(true);
            _colliderSyncs = GetComponentsInChildren<ColliderSync>(true);
            _lazers = GetComponentsInChildren<Lazer>(true);
        }
        else
        {
            if (sweep == null) sweep = GetComponentInChildren<LazerSweep>(true);
            if (rotate == null) rotate = GetComponentInChildren<LazerRotate>(true);
            if (colliderSync == null) colliderSync = GetComponentInChildren<ColliderSync>(true);
            if (laserDamage == null) laserDamage = GetComponentInChildren<Lazer>(true);

            _sweeps = sweep != null ? new[] { sweep } : System.Array.Empty<LazerSweep>();
            _rotates = rotate != null ? new[] { rotate } : System.Array.Empty<LazerRotate>();
            _colliderSyncs = colliderSync != null ? new[] { colliderSync } : System.Array.Empty<ColliderSync>();
            _lazers = laserDamage != null ? new[] { laserDamage } : System.Array.Empty<Lazer>();
        }

        AllocateSnapshotBuffers();
    }

    private void AllocateSnapshotBuffers()
    {
        _sweepWasEnabled = new bool[_sweeps != null ? _sweeps.Length : 0];
        _rotateWasEnabled = new bool[_rotates != null ? _rotates.Length : 0];
        _colliderSyncWasEnabled = new bool[_colliderSyncs != null ? _colliderSyncs.Length : 0];
        _lazerWasEnabled = new bool[_lazers != null ? _lazers.Length : 0];
    }

    private void CaptureSnapshot()
    {
        Capture(_sweeps, _sweepWasEnabled);
        Capture(_rotates, _rotateWasEnabled);
        Capture(_colliderSyncs, _colliderSyncWasEnabled);
        Capture(_lazers, _lazerWasEnabled);
    }

    private static void Capture<T>(T[] components, bool[] wasEnabled) where T : Behaviour
    {
        if (components == null || wasEnabled == null) return;
        int n = Mathf.Min(components.Length, wasEnabled.Length);
        for (int i = 0; i < n; i++)
            wasEnabled[i] = components[i] != null && components[i].enabled;
    }

    private void ApplyAllEnabled(bool enabled)
    {
        SetEnabledArray(_sweeps, enabled);
        SetEnabledArray(_rotates, enabled);
        SetEnabledArray(_colliderSyncs, enabled);
        SetEnabledArray(_lazers, enabled);
    }

    private static void SetEnabledArray<T>(T[] components, bool enabled) where T : Behaviour
    {
        if (components == null) return;
        for (int i = 0; i < components.Length; i++)
            if (components[i] != null) components[i].enabled = enabled;
    }

    private void RestoreSnapshot()
    {
        Restore(_sweeps, _sweepWasEnabled);
        Restore(_rotates, _rotateWasEnabled);
        Restore(_colliderSyncs, _colliderSyncWasEnabled);
        Restore(_lazers, _lazerWasEnabled);
    }

    private static void Restore<T>(T[] components, bool[] wasEnabled) where T : Behaviour
    {
        if (components == null || wasEnabled == null) return;
        int n = Mathf.Min(components.Length, wasEnabled.Length);
        for (int i = 0; i < n; i++)
            if (components[i] != null) components[i].enabled = wasEnabled[i];
    }

    public void SetFrozen(bool frozen)
    {
        if (_frozen == frozen) return;

        if (frozen)
            CaptureSnapshot();

        _frozen = frozen;

        if (_frozen)
            ApplyAllEnabled(false);
        else
            RestoreSnapshot();

        if (_frozen)
            onLaserFrozen?.Invoke();
        else
            onLaserUnfrozen?.Invoke();
    }

    public void Freeze() => SetFrozen(true);

    public void Unfreeze() => SetFrozen(false);
}