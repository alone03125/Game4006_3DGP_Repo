using System.Collections.Generic;
using UnityEngine;

// 掛在可互動物件的「根」上。控制底下 Renderer 的 Layer 切換成 Outline。
// 支援兩種呼叫方式：
//   1) 匿名: AddHighlight() / RemoveHighlight()  (向後相容舊程式)
//   2) 具名: AddHighlight(requester) / RemoveHighlight(requester)
//      → 推薦；會自動踢除已被 Destroy / SetActive(false) 的 requester，
//         解決「Clone 消失後 outline 卡住」之類的殘影問題。
[DisallowMultipleComponent]
public class InteractableHighlight : MonoBehaviour
{
    [Header("Outline Layer")]
    [Tooltip("outline layer name")]
    [SerializeField] private string outlineLayerName = "Outline";

    [Header("Renderers")]
    [Tooltip("if empty, will automatically grab Renderers on this GameObject and its children")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("Safety Valve")]
    [Tooltip("每隔這段時間驗證一次登錄中的 requester，自動踢除已銷毀/停用的 (秒)")]
    [Range(0.05f, 5f)]
    [SerializeField] private float validateInterval = 0.25f;

    [Tooltip("超過這個匿名計數就視為異常，下一次驗證會自動歸零。0 = 不限制")]
    [SerializeField] private int maxAnonymousCount = 16;

    [Header("Debug (read only)")]
    [SerializeField] private bool outlineCurrentlyOn;
    [SerializeField] private int namedRequesterCount;
    [SerializeField] private int anonymousCount;

    private int _outlineLayer = -1;
    private int[] _originalLayers;

    private readonly HashSet<Object> _requesters = new();
    private readonly List<Object> _staleBuffer = new();
    private int _anonymousCount;

    private float _nextValidateTime;
    private bool _outlineApplied;

    private void Awake()
    {
        _outlineLayer = LayerMask.NameToLayer(outlineLayerName);
        if (_outlineLayer < 0)
            Debug.LogError($"[InteractableHighlight] Layer '{outlineLayerName}' not created, please create it in Tags and Layers.", this);

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        _originalLayers = new int[targetRenderers.Length];
        for (int i = 0; i < targetRenderers.Length; i++)
            if (targetRenderers[i] != null)
                _originalLayers[i] = targetRenderers[i].gameObject.layer;
    }

    private void OnDisable() => ForceClear();
    private void OnDestroy() => ForceClear();

    // 匿名版本 (舊程式相容)
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

    // 具名版本 (推薦)
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

    private void Update()
    {
        if (Time.unscaledTime < _nextValidateTime) return;
        _nextValidateTime = Time.unscaledTime + validateInterval;

        Validate();

        // 匿名計數異常保險絲 (防止被呼叫端呼叫次數不對稱)
        if (maxAnonymousCount > 0 && _anonymousCount > maxAnonymousCount)
        {
            Debug.LogWarning(
                $"[InteractableHighlight] Anonymous count exceeded {maxAnonymousCount} on '{name}', force reset.", this);
            _anonymousCount = 0;
            Refresh();
        }
    }

    // 踢除已銷毀 / 被停用的具名 requester
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
        ApplyOutlineLayer(shouldBeOn);
        outlineCurrentlyOn = _outlineApplied;
    }

    private void ApplyOutlineLayer(bool on)
    {
        if (_outlineLayer < 0 || targetRenderers == null) return;
        if (on == _outlineApplied) return;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var r = targetRenderers[i];
            if (r == null) continue;
            r.gameObject.layer = on ? _outlineLayer : _originalLayers[i];
        }

        _outlineApplied = on;
    }
}