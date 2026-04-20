using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 掛在可互動物件的「根」上。啟用時 spawn 幽靈 renderer 到 Outline layer，不改動任何 Collider / 物理層。
// 支援 AddHighlight() 匿名與 AddHighlight(Object) 具名 (建議具名)。
[DisallowMultipleComponent]
public class InteractableHighlight : MonoBehaviour
{
    [Header("Outline Layer")]
    [SerializeField] private string outlineLayerName = "Outline";

    [Header("Renderers")]
    [Tooltip("留空會自動抓本物件與子物件的 MeshRenderer / SkinnedMeshRenderer")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("Safety Valve")]
    [Range(0.05f, 5f)]
    [SerializeField] private float validateInterval = 0.25f;
    [SerializeField] private int maxAnonymousCount = 16;

    [Header("Debug (read only)")]
    [SerializeField] private bool outlineCurrentlyOn;
    [SerializeField] private int namedRequesterCount;
    [SerializeField] private int anonymousCount;

    private int _outlineLayer = -1;

    private readonly HashSet<Object> _requesters = new();
    private readonly List<Object> _staleBuffer = new();
    private int _anonymousCount;

    private readonly List<GameObject> _ghosts = new();

    private float _nextValidateTime;
    private bool _outlineApplied;

    private void Awake()
    {
        _outlineLayer = LayerMask.NameToLayer(outlineLayerName);
        if (_outlineLayer < 0)
        {
            Debug.LogError($"[InteractableHighlight] Layer '{outlineLayerName}' not created.", this);
            return;
        }

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        BuildGhosts();
    }

    private void OnDisable() => ForceClear();

    private void OnDestroy()
    {
        for (int i = 0; i < _ghosts.Count; i++)
            if (_ghosts[i] != null) Destroy(_ghosts[i]);
        _ghosts.Clear();
    }

    // --- Public API ---
    public void AddHighlight()
    {
        _anonymousCount++;
        Refresh();
    }

    public void RemoveHighlight()
    {
        _anonymousCount = Mathf.Max(0, _anonymousCount - 1);
        Refresh();
    }

    public void AddHighlight(Object requester)
    {
        if (requester == null) { AddHighlight(); return; }
        if (_requesters.Add(requester)) Refresh();
    }

    public void RemoveHighlight(Object requester)
    {
        if (requester == null) { RemoveHighlight(); return; }
        if (_requesters.Remove(requester)) Refresh();
    }

    [ContextMenu("Force Clear")]
    public void ForceClear()
    {
        _requesters.Clear();
        _staleBuffer.Clear();
        _anonymousCount = 0;
        Refresh();
    }

    // --- Internals ---
    private void Update()
    {
        if (Time.unscaledTime < _nextValidateTime) return;
        _nextValidateTime = Time.unscaledTime + validateInterval;

        Validate();

        if (maxAnonymousCount > 0 && _anonymousCount > maxAnonymousCount)
        {
            Debug.LogWarning($"[InteractableHighlight] Anonymous count exceeded {maxAnonymousCount} on '{name}', reset.", this);
            _anonymousCount = 0;
            Refresh();
        }
    }

    private void Validate()
    {
        if (_requesters.Count == 0) return;

        _staleBuffer.Clear();
        foreach (var r in _requesters)
        {
            if (r == null) { _staleBuffer.Add(r); continue; }

            if (r is Component comp)
            {
                if (comp == null || !comp.gameObject.activeInHierarchy || !comp.gameObject.scene.IsValid())
                    _staleBuffer.Add(r);
            }
            else if (r is GameObject go)
            {
                if (go == null || !go.activeInHierarchy || !go.scene.IsValid())
                    _staleBuffer.Add(r);
            }
        }

        if (_staleBuffer.Count == 0) return;

        for (int i = 0; i < _staleBuffer.Count; i++)
            _requesters.Remove(_staleBuffer[i]);

        _staleBuffer.Clear();
        Refresh();
    }

    private void Refresh()
    {
        namedRequesterCount = _requesters.Count;
        anonymousCount = _anonymousCount;

        bool shouldBeOn = namedRequesterCount > 0 || anonymousCount > 0;
        ApplyOutline(shouldBeOn);
        outlineCurrentlyOn = _outlineApplied;
    }

    private void ApplyOutline(bool on)
    {
        if (on == _outlineApplied) return;

        for (int i = 0; i < _ghosts.Count; i++)
            if (_ghosts[i] != null) _ghosts[i].SetActive(on);

        _outlineApplied = on;
    }

    // --- Ghost renderer construction ---
    private void BuildGhosts()
    {
        if (targetRenderers == null) return;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var r = targetRenderers[i];
            if (r == null) continue;

            GameObject ghost = null;

            if (r is MeshRenderer mr)
            {
                var filter = mr.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                ghost = new GameObject($"__Outline_{mr.name}");
                ghost.transform.SetParent(mr.transform, false);
                ghost.layer = _outlineLayer;

                var gf = ghost.AddComponent<MeshFilter>();
                gf.sharedMesh = filter.sharedMesh;

                var gr = ghost.AddComponent<MeshRenderer>();
                CopyBasicSettings(mr, gr);
            }
            else if (r is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh == null) continue;

                Transform parent = smr.transform.parent != null ? smr.transform.parent : smr.transform;
                ghost = new GameObject($"__Outline_{smr.name}");
                ghost.transform.SetParent(parent, false);
                ghost.transform.localPosition = smr.transform.localPosition;
                ghost.transform.localRotation = smr.transform.localRotation;
                ghost.transform.localScale = smr.transform.localScale;
                ghost.layer = _outlineLayer;

                var gsm = ghost.AddComponent<SkinnedMeshRenderer>();
                gsm.sharedMesh = smr.sharedMesh;
                gsm.bones = smr.bones;
                gsm.rootBone = smr.rootBone;
                CopyBasicSettings(smr, gsm);
            }

            if (ghost != null)
            {
                ghost.SetActive(false);
                _ghosts.Add(ghost);
            }
        }
    }

    private static void CopyBasicSettings(Renderer src, Renderer dst)
    {
        dst.sharedMaterials = src.sharedMaterials; // 會被 Render Objects Override Material 覆蓋
        dst.shadowCastingMode = ShadowCastingMode.Off;
        dst.receiveShadows = false;
        dst.lightProbeUsage = LightProbeUsage.Off;
        dst.reflectionProbeUsage = ReflectionProbeUsage.Off;
        dst.allowOcclusionWhenDynamic = false;
    }
}